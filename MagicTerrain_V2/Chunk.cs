using MagicTerrain_V2.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Chunk
	{
		public float[] LocalTerrainMap { get;  set; }
		public float[] UnEditedLocalTerrainMap { get; set; }

		public int[] ChunkTriangles { get;  set; }
		public Vector3[] ChunkVertices { get;  set; }
		public Mesh[] Meshes { get; private set; }
		public bool EditsHaveBeenApplied { get; set; }
		public bool Hasdata => LocalTerrainMap != null;

		public Dictionary<int, float> EditedPoints { get; set; }= new();
		public int ChunkSize { get;  set; }
		public ChunkCore ChunkCore { get; set; }
		public bool WasEdited { get; set; }
		
		public bool ForceCompletion { get; private set; }

		private JobHandler jobHandler;
		private Node node;
		private float terrainSurface;
		private bool smoothTerrain;
		private bool flatShaded;

		public Chunk(float terrainSurface, bool smoothTerrain, bool flatShaded)
		{
			this.terrainSurface = terrainSurface;
			this.smoothTerrain = smoothTerrain;
			this.flatShaded = flatShaded;
			jobHandler = new JobHandler();
		}

		public void AssignNode(Node node)
		{
			this.node = node;
		}

		public void BuildMesh()
		{
			Meshes ??= new Mesh[1];
			Meshes[0] ??= new Mesh();
			Meshes[0].Clear();
			Meshes[0].vertices = ChunkVertices;
			Meshes[0].triangles = ChunkTriangles;
			Meshes[0].RecalculateNormals();
		}

		public void CompressChunkData()
		{
		}

		public void UncompressChunkData()
		{
		}

		public void ApplyChunkEdits()
		{
			if (EditsHaveBeenApplied) return;
			foreach (var editedPoint in EditedPoints)
			{
				if (editedPoint.Key >= 0 && editedPoint.Key < LocalTerrainMap.Length)
					LocalTerrainMap[editedPoint.Key] = editedPoint.Value;
				else
				{
					Debug.Log(editedPoint.Key);
				}
			}
			EditsHaveBeenApplied = true;
		}

		/// <summary>
		/// returns true when the mesh job has completed
		/// </summary>
		/// <returns></returns>
		public bool CheckJobComplete()
		{
			if (ForceCompletion)
			{
				ForceCompletion = false;
				return true;
			}
			if (jobHandler.CheckCompletion())
			{
				switch (jobHandler.ChunkJob)
				{
					case EditTerrainMapJob:
						CompleteEditTerrainMapJob(jobHandler.ChunkJob);
						break;
					case TerrainMapJob:
						CompleteTerrainMapJob(jobHandler.ChunkJob);
						break;
					case MeshDataJob:
						CompleteMeshDataJob(jobHandler.ChunkJob);
						return true;
				}
			}

			return false;
		}

		public void CreateAndQueueEditTerrainMapJob(Vector3Int differenceInPosition, NativeArray<EditedNodePointValue> editedNodePointValues, bool add)
		{
			var arrayLength = editedNodePointValues.Length;
			var terrainMapEditJob = new EditTerrainMapJob()
			{
				diferenceInPosition = differenceInPosition,
				points = new NativeArray<EditedNodePointValue>(editedNodePointValues, Allocator.TempJob),
				add = add,
				chunkSize = ChunkSize + 1,
				terrainMap = new NativeArray<float>(LocalTerrainMap, Allocator.TempJob),
				wasEdited = new NativeArray<bool>(1, Allocator.TempJob)
			};
			jobHandler.StartJob(terrainMapEditJob.Schedule(arrayLength, 244), terrainMapEditJob);
		}

		public void CompleteEditTerrainMapJob(IChunkJob jobHandlerChunkJob)
		{
			var editTerrainMapJob = (EditTerrainMapJob)jobHandlerChunkJob;
			var wasEdited = editTerrainMapJob.wasEdited[0];
			if (wasEdited)
			{
				LocalTerrainMap = editTerrainMapJob.terrainMap.ToArray();
				CreateAndQueueMeshDataJob();
			}
			else
			{
				ForceCompletion = true;
			}

			editTerrainMapJob.terrainMap.Dispose();
			editTerrainMapJob.wasEdited.Dispose();
			editTerrainMapJob.points.Dispose();
		}

		public void CreateAndQueueTerrainMapJob(
			Vector3 chunkPosition, 
			float planetSize, 
			int octaves, 
			float weightedStrength, 
			float lacunarity, 
			float gain,
			int octavesCaves,
			float weightedStrengthCaves,
			float lacunarityCaves,
			float gainCaves,
			float domainWarpAmp,
			int terrainMapSize,
			int seed)
		{
			var terrainMapJob = new TerrainMapJob
			{
				chunkSize = ChunkSize + 1,
				chunkPosition = chunkPosition,
				planetSize = planetSize,
				octaves = octaves,
				weightedStrength = weightedStrength,
				lacunarity = lacunarity,
				gain = gain,
				octavesCaves = octavesCaves,
				weightedStrengthCaves = weightedStrengthCaves,
				lacunarityCaves = lacunarityCaves,
				gainCaves = gainCaves,
				domainWarpAmp = domainWarpAmp,
				terrainMap = new NativeArray<float>(terrainMapSize, Allocator.TempJob),
				seed = seed
			};
			jobHandler.StartJob(terrainMapJob.Schedule(ChunkSize + 1, 244), terrainMapJob);
		}

		public void CompleteTerrainMapJob(IChunkJob jobHandlerChunkJob)
		{
			var terrainMapJob = (TerrainMapJob)jobHandlerChunkJob;
			LocalTerrainMap = terrainMapJob.terrainMap.ToArray();
			UnEditedLocalTerrainMap ??= LocalTerrainMap.ToArray();
			ApplyChunkEdits();
			CreateAndQueueMeshDataJob();

			terrainMapJob.terrainMap.Dispose();
		}

		public void CreateAndQueueMeshDataJob()
		{
			var meshDataJob = new MeshDataJob
			{
				chunkSize = ChunkSize + 1,
				terrainMap = new NativeArray<float>(LocalTerrainMap, Allocator.TempJob),
				terrainSurface = terrainSurface,
				vertices = new NativeArray<Vector3>(900000, Allocator.TempJob),
				triangles = new NativeArray<int>(900000, Allocator.TempJob),
				cube = new NativeArray<float>(8, Allocator.TempJob),
				smoothTerrain = smoothTerrain,
				flatShaded = !smoothTerrain || flatShaded,
				triCount = new NativeArray<int>(1, Allocator.TempJob),
				vertCount = new NativeArray<int>(1, Allocator.TempJob)
			};
			jobHandler.StartJob(meshDataJob.Schedule(), meshDataJob);
		}

		public void CompleteMeshDataJob(IChunkJob jobHandlerChunkJob)
		{
			var meshDataJob = (MeshDataJob)jobHandlerChunkJob;
			
			//TODO: investigate Null ref here
			var tCount = meshDataJob.triCount[0];
			ChunkTriangles = new int[tCount];

			for (var i = 0; i < tCount; i++)
			{
				ChunkTriangles[i] = meshDataJob.triangles[i];
			}

			var vCount = meshDataJob.vertCount[0];
			ChunkVertices = new Vector3[vCount];
			for (var i = 0; i < vCount; i++)
			{
				ChunkVertices[i] = meshDataJob.vertices[i];
			}

			BuildMesh();

			meshDataJob.vertices.Dispose();
			meshDataJob.triangles.Dispose();
			meshDataJob.cube.Dispose();
			meshDataJob.triCount.Dispose();
			meshDataJob.vertCount.Dispose();
			meshDataJob.terrainMap.Dispose();
			
			node.CreateChunkMesh();

			if (node.IsDisabled)
			{
				node.ReturnChunk();
			}
		}
	}
}
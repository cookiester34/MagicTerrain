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
		private bool flatShaded;

		private JobHandler jobHandler;
		private Node node;
		private bool smoothTerrain;
		private float terrainSurface;

		public Chunk(float terrainSurface, bool smoothTerrain, bool flatShaded)
		{
			this.terrainSurface = terrainSurface;
			this.smoothTerrain = smoothTerrain;
			this.flatShaded = flatShaded;
			jobHandler = new JobHandler();

			MeshDataSets = new MeshData[5];
			for (var i = 0; i < MeshDataSets.Length; i++) MeshDataSets[i] = new MeshData();
		}

		public Chunk()
		{
			MeshDataSets = new MeshData[5];
			for (var i = 0; i < MeshDataSets.Length; i++) MeshDataSets[i] = new MeshData();
		}

		public MeshData[] MeshDataSets { get; set; }
		public float[] LocalTerrainMap { get; set; }
		public float[] UnEditedLocalTerrainMap { get; set; }
		public Mesh[] Meshes { get; private set; }
		public bool EditsHaveBeenApplied { get; set; }
		public bool Hasdata => LocalTerrainMap != null;
		public Dictionary<int, float> EditedPoints { get; set; } = new();
		public int ChunkSize { get; set; }
		public ChunkCore ChunkCore { get; set; }
		public bool WasEdited { get; set; }

		public bool ForceCompletion { get; private set; }

		public void AssignNode(Node node)
		{
			this.node = node;
		}

		public void BuildMesh()
		{
			Meshes ??= new Mesh[5];
			for (var index = 0; index < Meshes.Length; index++)
			{
				Meshes[index] ??= new Mesh();
				Meshes[index].Clear();
				Meshes[index].vertices = MeshDataSets[index].chunkVertices;
				Meshes[index].triangles = MeshDataSets[index].chunkTriangles;
				Meshes[index].RecalculateNormals();
			}
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
				if (editedPoint.Key >= 0 && editedPoint.Key < LocalTerrainMap.Length)
					LocalTerrainMap[editedPoint.Key] = editedPoint.Value;
				else
					Debug.Log(editedPoint.Key);
			EditsHaveBeenApplied = true;
		}

		/// <summary>
		///     returns true when the mesh job has completed
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

		public void CreateAndQueueEditTerrainMapJob(Vector3Int differenceInPosition,
			NativeArray<EditedNodePointValue> editedNodePointValues, bool add)
		{
			var arrayLength = editedNodePointValues.Length;
			var terrainMapEditJob = new EditTerrainMapJob
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
				cube = new NativeArray<float>(8, Allocator.TempJob),
				smoothTerrain = smoothTerrain,
				flatShaded = !smoothTerrain || flatShaded,
				triCount = new NativeArray<int>(5, Allocator.TempJob),
				vertCount = new NativeArray<int>(5, Allocator.TempJob),
				vertices = new NativeArray<Vector3>(900000, Allocator.TempJob),
				triangles = new NativeArray<int>(900000, Allocator.TempJob),
				vertices1 = new NativeArray<Vector3>(900000, Allocator.TempJob),
				triangles1 = new NativeArray<int>(900000, Allocator.TempJob),
				vertices2 = new NativeArray<Vector3>(900000, Allocator.TempJob),
				triangles2 = new NativeArray<int>(900000, Allocator.TempJob),
				vertices3 = new NativeArray<Vector3>(900000, Allocator.TempJob),
				triangles3 = new NativeArray<int>(900000, Allocator.TempJob),
				vertices4 = new NativeArray<Vector3>(900000, Allocator.TempJob),
				triangles4 = new NativeArray<int>(900000, Allocator.TempJob)
			};
			jobHandler.StartJob(meshDataJob.Schedule(), meshDataJob);
		}

		public void CompleteMeshDataJob(IChunkJob jobHandlerChunkJob)
		{
			var meshDataJob = (MeshDataJob)jobHandlerChunkJob;

			//TODO: investigate Null ref here
			var triCount = meshDataJob.triCount.ToArray();
			var vertCount = meshDataJob.vertCount.ToArray();

			MeshDataSets ??= new MeshData[7];
			//LOD0
			var tCount = triCount[0];
			MeshDataSets[0].chunkTriangles = new int[tCount];
			for (var i = 0; i < tCount; i++) MeshDataSets[0].chunkTriangles[i] = meshDataJob.triangles[i];
			var vCount = vertCount[0];
			MeshDataSets[0].chunkVertices = new Vector3[vCount];
			for (var i = 0; i < vCount; i++) MeshDataSets[0].chunkVertices[i] = meshDataJob.vertices[i];

			//LOD1
			var tCount1 = triCount[1];
			MeshDataSets[1].chunkTriangles = new int[tCount1];
			for (var i = 0; i < tCount1; i++) MeshDataSets[1].chunkTriangles[i] = meshDataJob.triangles1[i];
			var vCount1 = vertCount[1];
			MeshDataSets[1].chunkVertices = new Vector3[vCount1];
			for (var i = 0; i < vCount1; i++) MeshDataSets[1].chunkVertices[i] = meshDataJob.vertices1[i];

			//LOD2
			var tCount2 = triCount[2];
			MeshDataSets[2].chunkTriangles = new int[tCount2];
			for (var i = 0; i < tCount2; i++) MeshDataSets[2].chunkTriangles[i] = meshDataJob.triangles2[i];
			var vCount2 = vertCount[2];
			MeshDataSets[2].chunkVertices = new Vector3[vCount2];
			for (var i = 0; i < vCount2; i++) MeshDataSets[2].chunkVertices[i] = meshDataJob.vertices2[i];

			//LOD3
			var tCount3 = triCount[3];
			MeshDataSets[3].chunkTriangles = new int[tCount3];
			for (var i = 0; i < tCount3; i++) MeshDataSets[3].chunkTriangles[i] = meshDataJob.triangles3[i];
			var vCount3 = vertCount[3];
			MeshDataSets[3].chunkVertices = new Vector3[vCount3];
			for (var i = 0; i < vCount3; i++) MeshDataSets[3].chunkVertices[i] = meshDataJob.vertices3[i];

			//LOD4
			var tCount4 = triCount[4];
			MeshDataSets[4].chunkTriangles = new int[tCount4];
			for (var i = 0; i < tCount4; i++) MeshDataSets[4].chunkTriangles[i] = meshDataJob.triangles4[i];
			var vCount4 = vertCount[4];
			MeshDataSets[4].chunkVertices = new Vector3[vCount4];
			for (var i = 0; i < vCount4; i++) MeshDataSets[4].chunkVertices[i] = meshDataJob.vertices4[i];

			BuildMesh();

			meshDataJob.cube.Dispose();
			meshDataJob.triCount.Dispose();
			meshDataJob.vertCount.Dispose();
			meshDataJob.vertices.Dispose();
			meshDataJob.triangles.Dispose();
			meshDataJob.vertices1.Dispose();
			meshDataJob.triangles1.Dispose();
			meshDataJob.vertices2.Dispose();
			meshDataJob.triangles2.Dispose();
			meshDataJob.vertices3.Dispose();
			meshDataJob.triangles3.Dispose();
			meshDataJob.vertices4.Dispose();
			meshDataJob.triangles4.Dispose();
			meshDataJob.terrainMap.Dispose();

			node.CreateChunkMesh();

			if (node.IsDisabled) node.ReturnChunk();
		}

		public class MeshData
		{
			public int[] chunkTriangles;
			public Vector3[] chunkVertices;
		}
	}
}
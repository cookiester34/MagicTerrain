using MagicTerrain_V2.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicTerrain_V2
{
	public class Chunk
	{
		private bool flatShaded;

		private JobHandler jobHandler;
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

		public MeshData[] MeshDataSets { get; set; }
		public Mesh[] Meshes { get; set; }
		public bool EditsHaveBeenApplied { get; set; }
		public bool Hasdata => LocalTerrainMap != null;
		public Dictionary<int, float> EditedPoints { get; set; } = new();
		public int ChunkSize { get; set; }
		public ChunkCore ChunkCore { get; set; }
		public bool WasEdited { get; set; }
		public bool ForceCompletion { get; private set; }

		public float[] LocalTerrainMap { get; set; }
		public float[] UnEditedLocalTerrainMap { get; set; }

		public byte[] compressedEditedKeys;
		public byte[] compressedEditedValues;

		public Action<bool> OnMeshJobDone;
		public Action OnDispose;

		public void BuildMesh(int lodIndex)
		{
			Meshes ??= new Mesh[5];
			Meshes[lodIndex] ??= new Mesh();
			Meshes[lodIndex].Clear();
			Meshes[lodIndex].vertices = MeshDataSets[lodIndex].chunkVertices;
			Meshes[lodIndex].triangles = MeshDataSets[lodIndex].chunkTriangles;
			Meshes[lodIndex].RecalculateNormals();
		}

		public void CompressChunkData()
		{
			compressedEditedKeys = EditedPoints.Keys.ToArray().Compress();
			compressedEditedValues = EditedPoints.Values.ToArray().Compress();
		}

		public void UncompressChunkData()
		{
			var editedKeys = compressedEditedKeys.UncompressIntArray();
			var editedValues = compressedEditedValues.UncompressFloatArray();
			for (var index = 0; index < editedKeys.Length; index++)
			{
				EditedPoints[editedKeys[index]] = editedValues[index];
			}
		}

		public void ApplyChunkEdits()
		{
			if (EditsHaveBeenApplied) return;
			foreach (var editedPoint in EditedPoints)
			{
				if (editedPoint.Key >= 0 && editedPoint.Key < LocalTerrainMap.Length)
					LocalTerrainMap[editedPoint.Key] = editedPoint.Value;
				else
					Debug.Log(editedPoint.Key);
			}
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
						return CompleteMeshDataJob(jobHandler.ChunkJob);
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
				CreateAndQueueMeshDataJob(0);
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

		//TODO: Memory leak the arrays never get GC'd
		public void CompleteTerrainMapJob(IChunkJob jobHandlerChunkJob)
		{
			var terrainMapJob = (TerrainMapJob)jobHandlerChunkJob;
			LocalTerrainMap = terrainMapJob.terrainMap.ToArray();
			UnEditedLocalTerrainMap = LocalTerrainMap.ToArray();
			ApplyChunkEdits();
			CreateAndQueueMeshDataJob(0);
			OnMeshJobDone?.Invoke(true);
			terrainMapJob.terrainMap.Dispose();
		}

		//This is slow
		public void CreateAndQueueMeshDataJob(int lodIndex)
		{
			var meshDataJob = new MeshDataJob
			{
				chunkSize = ChunkSize + 1,
				terrainMap = new NativeArray<float>(LocalTerrainMap, Allocator.TempJob),
				terrainSurface = terrainSurface,
				cube = new NativeArray<float>(8, Allocator.TempJob),
				smoothTerrain = smoothTerrain,
				flatShaded = !smoothTerrain || flatShaded,
				triCount = new NativeArray<int>(1, Allocator.TempJob),
				vertCount = new NativeArray<int>(1, Allocator.TempJob),
				//max number of triangles: 21845 and vertices: 65535
				vertices = new NativeArray<Vector3>(65535, Allocator.TempJob),
				triangles = new NativeArray<int>(21845, Allocator.TempJob),
				lodIndex = lodIndex
			};
			jobHandler.StartJob(meshDataJob.Schedule(), meshDataJob);
		}

		//This is slow
		public bool CompleteMeshDataJob(IChunkJob jobHandlerChunkJob)
		{
			var meshDataJob = (MeshDataJob)jobHandlerChunkJob;
			meshDataJob.cube.Dispose();
			meshDataJob.terrainMap.Dispose();

			var triCount = meshDataJob.triCount.ToArray();
			var vertCount = meshDataJob.vertCount.ToArray();
			var lodIndex = meshDataJob.lodIndex;

			MeshDataSets ??= new MeshData[4];
			var tCount = triCount[0];
			MeshDataSets[lodIndex].chunkTriangles = new int[tCount];
			for (var i = 0; i < tCount; i++) MeshDataSets[lodIndex].chunkTriangles[i] = meshDataJob.triangles[i];
			var vCount = vertCount[0];
			MeshDataSets[lodIndex].chunkVertices = new Vector3[vCount];
			for (var i = 0; i < vCount; i++) MeshDataSets[lodIndex].chunkVertices[i] = meshDataJob.vertices[i];

			BuildMesh(lodIndex);


			meshDataJob.triCount.Dispose();
			meshDataJob.vertCount.Dispose();
			meshDataJob.vertices.Dispose();
			meshDataJob.triangles.Dispose();

			// if (lodIndex >= 3)
			// {
				OnMeshJobDone?.Invoke(true);
				return true;
			// }
			// else
			// {
			// 	CreateAndQueueMeshDataJob(lodIndex + 1);
			// 	OnMeshJobDone?.Invoke(false);
			// }
			return false;
		}

		public class MeshData
		{
			public int[] chunkTriangles;
			public Vector3[] chunkVertices;
		}

		public void Dispose()
		{
			if (Meshes != null)
			{
				foreach (var mesh in Meshes)
				{
					Object.Destroy(mesh);
				}

				Meshes = null;
			}

			jobHandler.Dispose();
			jobHandler = null;
			LocalTerrainMap = null;
			UnEditedLocalTerrainMap = null;
			compressedEditedKeys = null;
			compressedEditedValues = null;
			MeshDataSets = null;
			OnMeshJobDone = null;
			OnDispose?.Invoke();
		}
	}
}
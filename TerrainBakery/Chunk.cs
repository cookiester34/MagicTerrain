using System;
using System.Collections.Generic;
using System.Linq;
using TerrainBakery.Helpers;
using TerrainBakery.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerrainBakery
{
	[Serializable]
	public class Chunk
	{
		private JobHandler jobHandler;
		private bool flatShaded;
		private bool smoothTerrain;
		private float terrainSurface;
		private int chunkSize;
		private int terrainMapSize;
		
		public ChunkContainer ChunkContainer { get; set; }
		public Vector3Int Position { get; set; }
		public Vector3 PositionReal { get; set; }
		
		private MeshData[] MeshDataSets { get; set; }
		internal Mesh[] Meshes { get; set; }
		
		private bool ForceCompletion { get; set; }
		private bool EditsHaveBeenApplied { get; set; }

		public bool WasEdited { get; set; }
		public bool IsActive { get; set; }

		[field:SerializeField]
		public float[] LocalTerrainMap { get; set; }
		public float[] UnEditedLocalTerrainMap { get; set; }
		
		public Dictionary<int, float> EditedPoints { get; } = new();
		public bool IsProcessing { get; set; }

		public byte[] compressedEditedKeys;
		public byte[] compressedEditedValues;
		
		public Action<Chunk, Vector3, float, bool> OnEdit;
		public Action<Chunk> OnDispose;
		
		public Chunk(float terrainSurface, bool smoothTerrain, bool flatShaded, int terrainMapSize, int chunkSize)
		{
			this.chunkSize = chunkSize;
			this.terrainMapSize = terrainMapSize;
			this.terrainSurface = terrainSurface;
			this.smoothTerrain = smoothTerrain;
			this.flatShaded = flatShaded;
			jobHandler = new JobHandler();

			MeshDataSets = new MeshData[5];
			for (var i = 0; i < MeshDataSets.Length; i++) MeshDataSets[i] = new MeshData();
		}

		private void BuildMesh(int lodIndex)
		{
			Meshes ??= new Mesh[5];
			Meshes[lodIndex] ??= new Mesh();
			Meshes[lodIndex].Clear();
			Meshes[lodIndex].vertices = MeshDataSets[lodIndex].chunkVertices;
			Meshes[lodIndex].triangles = MeshDataSets[lodIndex].chunkTriangles;
			Meshes[lodIndex].RecalculateNormals();
		}
		
		public bool IsVisible(Plane[] planes)
		{
			var nodeBounds = new Bounds(PositionReal, Vector3.one * (chunkSize * 2)); //if planets don't move this can be cached
			// Check if the renderer is within the view frustum of the camera
			var visible = GeometryUtility.TestPlanesAABB(planes, nodeBounds);
			return visible;
		}
		
		public void SetLodIndex(int index)
		{
			if (ChunkContainer != null)
			{
				ChunkContainer.LodIndex = index;
			}
		}
		
		public void Disable()
		{
			IsActive = false;
			ChunkContainer.DisableContainer();
		}
		
		public void EnableNode()
		{
			IsActive = true;
			ChunkContainer.EnableContainer();
			ChunkContainer.transform.position = PositionReal;
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

		internal void EditChunk(Vector3 hitPoint, float radius, bool add)
		{
			OnEdit?.Invoke(this, hitPoint, radius, add);
		}

		private void ApplyChunkEdits()
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
				IsProcessing = false;
				return true;
			}

			if (!jobHandler.CheckCompletion()) return false;
			
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
				case null:
					//Just so the chunk will be removed from the queue if it somehow manages to reach here.
					return true;
			}

			return false;
		}

		public void CreateAndQueueEditTerrainMapJob(Vector3Int differenceInPosition,
			NativeArray<EditedNodePointValue> editedNodePointValues, bool add)
		{
			IsProcessing = true;
			var arrayLength = editedNodePointValues.Length;
			var terrainMapEditJob = new EditTerrainMapJob
			{
				diferenceInPosition = differenceInPosition,
				points = new NativeArray<EditedNodePointValue>(editedNodePointValues, Allocator.TempJob),
				add = add,
				chunkSize = chunkSize + 1,
				terrainMap = new NativeArray<float>(LocalTerrainMap, Allocator.TempJob),
				wasEdited = new NativeArray<bool>(1, Allocator.TempJob)
			};
			jobHandler.StartJob(terrainMapEditJob.Schedule(arrayLength, 244), terrainMapEditJob);
		}

		private void CompleteEditTerrainMapJob(IChunkJob jobHandlerChunkJob)
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
			int seed)
		{
			IsProcessing = true;
			var terrainMapJob = new TerrainMapJob
			{
				chunkSize = chunkSize + 1,
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
			jobHandler.StartJob(terrainMapJob.Schedule(chunkSize + 1, 244), terrainMapJob);
		}

		public void CompleteTerrainMapJob(IChunkJob jobHandlerChunkJob)
		{
			var terrainMapJob = (TerrainMapJob)jobHandlerChunkJob;
			LocalTerrainMap = terrainMapJob.terrainMap.ToArray();
			UnEditedLocalTerrainMap = LocalTerrainMap.ToArray();

			if (LocalTerrainMap.Length <= 0)
			{
				Debug.LogError($"Critical Error, terrain map is empty at chunk position {Position}");
			}
			ApplyChunkEdits();
			CreateAndQueueMeshDataJob(0);
			terrainMapJob.terrainMap.Dispose();
		}

		//This is slow
		public void CreateAndQueueMeshDataJob(int lodIndex)
		{
			var meshDataJob = new MeshDataJob
			{
				chunkSize = chunkSize + 1,
				terrainMap = new NativeArray<float>(LocalTerrainMap, Allocator.TempJob),
				terrainSurface = terrainSurface,
				cube = new NativeArray<float>(8, Allocator.TempJob),
				smoothTerrain = smoothTerrain,
				flatShaded = !smoothTerrain || flatShaded,
				triCount = new NativeArray<int>(1, Allocator.TempJob),
				vertCount = new NativeArray<int>(1, Allocator.TempJob),
				//max number of triangles: 21845 and vertices: 65535
				vertices = new NativeArray<Vector3>(70000, Allocator.TempJob),
				triangles = new NativeArray<int>(25000, Allocator.TempJob),
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
			
			IsProcessing = false;
			if (ChunkContainer != null)
			{
				if (lodIndex >= 3)
				{
					IsProcessing = false;
					ChunkContainer.CreateChunkMesh();
					return true;
				}
			}
			else
			{
				Debug.LogError("Chunk has no ChunkContainer");
			}

			CreateAndQueueMeshDataJob(lodIndex + 1);
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
			LocalTerrainMap = null;
			UnEditedLocalTerrainMap = null;
			compressedEditedKeys = null;
			compressedEditedValues = null;
			
			MeshDataSets = new MeshData[5];
			for (var i = 0; i < MeshDataSets.Length; i++) MeshDataSets[i] = new MeshData();
			
			OnEdit = null;
			if (ChunkContainer != null) ChunkContainer.OnEdit -= EditChunk;
			ChunkContainer = null;
			OnDispose?.Invoke(this);
		}
	}
}
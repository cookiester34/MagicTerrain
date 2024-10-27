using MagicTerrain_V3.Gravity;
using MagicTerrain_V3.Helpers;
using MagicTerrain_V3.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MagicTerrain_V3
{
	public partial class ChunkCore : GravityInflucener
	{
		internal const float TERRAIN_SURFACE = 0.4f;
		
		[field: SerializeField]
		public bool DebugMode { get; set; }

		[SerializeField]
		private string planetName;

		[SerializeField]
		internal int chunkSize = 20;

		[SerializeField]
		private int chunkSetSize = 10;

		[SerializeField]
		private int viewDistance = 2;

		[SerializeField]
		private int ignoreFrustrumCullDistance = 2;

		[SerializeField]
		private int updateDistance = 20;

		[SerializeField]
		private Transform playerTransform;

		[SerializeField]
		private Camera mainCamera;

		[SerializeField]
		private int chunkPoolCount = 500;

		[SerializeField]
		internal int octaves = 3;

		[SerializeField]
		internal float weightedStrength;

		[SerializeField]
		internal float lacunarity = 2f;

		[SerializeField]
		internal float gain = 0.5f;

		[SerializeField]
		public int octavesCaves = 4;

		[SerializeField]
		public float weightedStrengthCaves = 1f;

		[SerializeField]
		public float lacunarityCaves = 2f;

		[SerializeField]
		public float gainCaves = 1f;

		[SerializeField]
		internal float domainWarpAmp = 1.0f;

		[SerializeField]
		private int seed = 1337;

		[SerializeField]
		internal bool smoothTerrain;

		[SerializeField]
		internal bool flatShaded;

		[SerializeField]
		private Material coreMaterial;
		
		private bool forceUpdate = true;
		
		private Plane[] cameraPlanes;

		private ChunkSetSaveLoadSystem loadSaveSystem;
		
		private Vector3 lastPlayerPosition;
		
		private Quaternion planetRotationLastUpdate;
		
		private int trueIgnoreCullDistance;
		
		private int trueViewDistance;
		
		private HashSet<Vector3> VisiblePositions { get; } = new();
		
		private readonly Dictionary<Vector3, Chunk> chunks = new(); //TODO: need to remove chunks

		private int fixedFrameCount;

		//debug info
		private Vector3 editedNodePointValuePosition;

		private List<EditedNodePointValue> editedNodePointValues = new();
		
		private void OnDrawGizmos()
		{
			if (!DebugMode) return;

			editedNodePointValues.ForEach(editedNodePointValue =>
			{
				Gizmos.color = Color.red;
				Gizmos.DrawSphere(editedNodePointValuePosition + editedNodePointValue.PointPosition, 0.1f);
			});

			if (loadSaveSystem != null)
			{
				foreach (var chunkSet in loadSaveSystem.ChunkSets)
				{
					Gizmos.color = Color.magenta;
					Gizmos.DrawWireCube(chunkSet.Key, new Vector3(chunkSetSize * chunkSize, chunkSetSize * chunkSize, chunkSetSize * chunkSize));

					foreach (var chunk in chunkSet.Value.chunks)
					{
						Gizmos.color = Color.blue;
						Gizmos.DrawCube(chunk.Key, new Vector3(1, 1, 1));
					}
				}
			}
		}
		
		private void Awake()
		{
			var chunkSizeDoubled = chunkSize * 2;
			var terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;
			var chunkParameters = new object[] { TERRAIN_SURFACE, smoothTerrain, flatShaded, terrainMapSize, chunkSize };
			loadSaveSystem = new ChunkSetSaveLoadSystem(this, planetName, chunkPoolCount, chunkSetSize * chunkSize, chunkParameters, coreMaterial);
		}
		
		private void Start()
		{
			base.Start();
			lastPlayerPosition = playerTransform.position;
			trueIgnoreCullDistance = ignoreFrustrumCullDistance * chunkSize;
			trueViewDistance = viewDistance * chunkSize;
		}

		private void Update()
		{
			ManageQueues(playerTransform.position);
		}

		private void FixedUpdate()
		{
			CalculateVisibleNodes();

			fixedFrameCount++;
			if (fixedFrameCount % 60 == 0)
			{
				fixedFrameCount = 0;
				loadSaveSystem.SaveOutOfRangeChunkSets(playerTransform.position);
			}
		}
		
		private void CalculateVisibleNodes()
		{
			cameraPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

			var playerLastPositionRelative = transform.worldToLocalMatrix.MultiplyPoint(playerTransform.position);
			var playerPosition = new Vector3Int(
				Mathf.RoundToInt(playerLastPositionRelative.x).RoundOff(chunkSize),
				Mathf.RoundToInt(playerLastPositionRelative.y).RoundOff(chunkSize),
				Mathf.RoundToInt(playerLastPositionRelative.z).RoundOff(chunkSize));

			var distance = Vector3.Distance(lastPlayerPosition, playerPosition);
			if (distance >= updateDistance || forceUpdate)
			{
				planetRotationLastUpdate = transform.rotation;
				lastPlayerPosition = playerPosition;
				forceUpdate = false;

				var lastVisibleNodes = VisiblePositions.ToList();
				VisiblePositions.Clear();

				var planetTrueSize = RadiusKm * 1.5f;

				for (var x = playerPosition.x - trueViewDistance;
				     x < playerPosition.x + trueViewDistance;
				     x += chunkSize)
				for (var y = playerPosition.y - trueViewDistance;
				     y < playerPosition.y + trueViewDistance;
				     y += chunkSize)
				for (var z = playerPosition.z - trueViewDistance;
				     z < playerPosition.z + trueViewDistance;
				     z += chunkSize)
				{
					var position = new Vector3(x, y, z);

					//chunk lays outside of the planet
					if (Vector3.Distance(position, Vector3.zero) >= planetTrueSize) continue;

					var rotatedPosition = Matrix4x4.Rotate(planetRotationLastUpdate) *
					                      transform.localToWorldMatrix.MultiplyPoint(position);
					var realPosition = new Vector3(rotatedPosition.x, rotatedPosition.y, rotatedPosition.z);

					if (!chunks.ContainsKey(position))
					{
						var chunkPosition = new Vector3Int(x, y, z);
						var requestChunk = loadSaveSystem.RequestChunk(chunkPosition);
						requestChunk.OnEdit += EditChunk;
						requestChunk.OnDispose += RemoveChunk;
						chunks.Add(position, requestChunk);
						queuedChunks.Add(requestChunk);
					}
					
					chunks[position].PositionReal = realPosition;

					VisiblePositions.Add(position);
				}

				foreach (var position in lastVisibleNodes)
				{
					if (VisiblePositions.Contains(position)) continue;
					if (chunks.ContainsKey(position)) chunks[position].Disable();
				}

				FrustrumCullVisibleNodes();
				return;
			}

			FrustrumCullVisibleNodes();
		}

		private void RemoveChunk(Chunk chunk)
		{
			chunk.OnDispose -= RemoveChunk;
			chunks.Remove(chunk.Position);
		}

		private void FrustrumCullVisibleNodes()
		{
			foreach (var nodePosition in VisiblePositions)
			{
				if (!chunks.TryGetValue(nodePosition, out var chunk)) continue;

				if (mainCamera == null)
				{
					Debug.LogError("Cannot perform frustum culling without a camera");
					chunk.EnableNode();
					continue;
				}

				var distance = Vector3.Distance(playerTransform.position, chunk.PositionReal);
				
				if (!chunk.IsVisible(cameraPlanes))
				{
					if (distance > trueIgnoreCullDistance)
					{
						chunk.Disable();
						continue;
					}
				}

				switch (distance)
				{
					case <100:
						chunk.SetLodIndex(0);
						break;
					case <150:
						chunk.SetLodIndex(1);
						break;
					case <200:
						chunk.SetLodIndex(2);
						break;
					default:
						chunk.SetLodIndex(3);
						break;
				}

				chunk.EnableNode();
			}
		}
		
		public void EditChunk(Chunk chunk, Vector3 hitPoint, float radius, bool add)
		{
			var neighbourNodes = GetNeighbourChunks(chunk.Position);

			if (neighbourNodes.Any(x => x.IsProcessing))
			{
				return;
			}

			if (queuedNodesCirclePoints.ContainsKey(chunk))
			{
				Debug.LogError("Failed to add chunk to edit queue, cancelling edit");
				return;
			}

			var ceilToInt = Mathf.CeilToInt(radius) * 2 + 1;
			var arraySize = ceilToInt * ceilToInt * ceilToInt;

			if (arraySize <= 0)
			{
				return;
			}

			hitPoint = transform.worldToLocalMatrix.MultiplyPoint(hitPoint);
			hitPoint -= chunk.Position;

			var getCirclePointsJob = GetCirclePointJobs(new Vector3Int((int)hitPoint.x, (int)hitPoint.y, (int)hitPoint.z), arraySize, radius, add);
			var jobHandle = getCirclePointsJob.Schedule();
			queuedNodesCirclePoints.Add(chunk, new ChunkEditJobData(jobHandle, getCirclePointsJob, add));

			JobHandle.ScheduleBatchedJobs();
		}

		private IEnumerable<Chunk> GetNeighbourChunks(Vector3Int chunkPosition)
		{
			List<Chunk> foundChunks = new();
			for (var x = chunkPosition.x - chunkSize; x <= chunkPosition.x + chunkSize; x+= chunkSize)
			{
				for (var y = chunkPosition.y - chunkSize; y <= chunkPosition.y + chunkSize; y+= chunkSize)
				{
					for (var z = chunkPosition.z - chunkSize; z <= chunkPosition.z + chunkSize; z+= chunkSize)
					{
						if (chunks.TryGetValue(new Vector3Int(x, y, z), out var foundChunk))
						{
							foundChunks.Add(foundChunk);
						}
					}
				}
			}

			return foundChunks;
		}

		private GetCirclePointsJob GetCirclePointJobs(Vector3Int hitPoint, int arraySize, float radius, bool add)
		{
			var getCirclePointsJob = new GetCirclePointsJob()
			{
				hitPosition = hitPoint,
				add = add,
				radius = radius,
				points = new NativeArray<EditedNodePointValue>(arraySize, Allocator.TempJob)
			};
			return getCirclePointsJob;
		}
		
		private void OnApplicationQuit()
		{
			loadSaveSystem.SaveAllChunkSets();
		}
	}
}
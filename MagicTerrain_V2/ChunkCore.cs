using MagicTerrain_V2.Gravity;
using MagicTerrain_V2.Helpers;
using MagicTerrain_V2.Jobs;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MagicTerrain_V2
{
	public partial class ChunkCore : GravityInflucener
	{
		[field: SerializeField]
		public bool DebugMode { get; set; }

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
		private int chunkContainerStartPoolCount = 100;

		[SerializeField]
		private int queueUpdateFrequency = 10;

		[SerializeField]
		private int queueDequeueLimit = 5;

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

		[SerializeField]
		private ChunkContainer chunkContainerPrefab;

		private bool forceUpdate = true;

		private Vector3 lastPlayerPosition;

		private Vector3 lastChunkSavePosition;

		private int queueUpdateCount;

		private readonly Dictionary<Vector3, Node> nodes = new();

		private readonly List<ChunkContainer> chunkContainers = new();

		private int terrainMapSize;

		private float TrueWorldSize => RadiusKm;

		private HashSet<Vector3> VisiblePositions { get; } = new();

		private Quaternion planetRotationLastUpdate;

		//debug info
		private Vector3 editedNodePointValuePosition;

		private List<EditedNodePointValue> editedNodePointValues = new();

		private Plane[] cameraPlanes;

		private int trueIgnoreCullDistance;

		private int trueViewDistance;

		private void OnDrawGizmos()
		{
			if (!DebugMode) return;

			editedNodePointValues.ForEach(editedNodePointValue =>
			{
				Gizmos.color = Color.red;
				Gizmos.DrawSphere(editedNodePointValuePosition + editedNodePointValue.PointPosition, 0.1f);
			});
		}

		private void OnApplicationQuit()
		{
			ChunkSetSaveLoadSystem.SaveAllChunkSets();
		}

		//probably wont to move this somewhere else later on
		private void Awake()
		{
			ChunkSetSaveLoadSystem.InitializeChunkSetSaveLoadSystem(chunkSetSize * chunkSize);
			var roundVectorDownToNearestChunkSet = ChunkSetSaveLoadSystem.RoundVectorDownToNearestChunkSet(playerTransform.position);
			if (!ChunkSetSaveLoadSystem.TryLoadChunkSet(this, roundVectorDownToNearestChunkSet))
			{
				Debug.LogError($"Failed to load ChunkSet at {roundVectorDownToNearestChunkSet}");
			}
		}

		private void Start()
		{
			base.Start();
			lastPlayerPosition = playerTransform.position;
			lastChunkSavePosition = playerTransform.position;
			trueIgnoreCullDistance = ignoreFrustrumCullDistance * chunkSize;
			trueViewDistance = viewDistance * chunkSize;

			for (var i = 0; i < chunkContainerStartPoolCount; i++)
			{
				var requestedChunkContainer = Instantiate(chunkContainerPrefab, transform);
				requestedChunkContainer.ChunkCore = this;
				requestedChunkContainer.transform.position = Vector3.zero;
				chunkContainers.Add(requestedChunkContainer);
				requestedChunkContainer.gameObject.SetActive(false);
			}

			var chunkSizeDoubled = chunkSize * 2;
			terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;
		}

		private void FixedUpdate()
		{
			ManageQueues();

			CalculateVisibleNodes();

			var playerTransformPosition = playerTransform.position;
			var distance = Vector3.Distance(playerTransformPosition, lastChunkSavePosition);
			if (distance <= viewDistance)
			{
				lastChunkSavePosition = playerTransformPosition;
				ChunkSetSaveLoadSystem.SaveOutOfRangeChunkSets(lastPlayerPosition, trueViewDistance);
			}
		}

		private void ManageQueues()
		{
			ChecKGetCirclePointJobsQueue();

			CheckEditQueues();

			#region ChunkCreationQueue

			queueUpdateCount++;
			if (queueUpdateCount % queueUpdateFrequency != 0) return;
			queueUpdateCount = 0;

			CheckGenerateQueues();

			#endregion
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

				var trueWorldSize = TrueWorldSize * 1.5f;

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
					if (Vector3.Distance(position, Vector3.zero) >= trueWorldSize) continue;

					var rotatedPosition = Matrix4x4.Rotate(planetRotationLastUpdate) *
					                      transform.localToWorldMatrix.MultiplyPoint(position);
					var realPosition = new Vector3(rotatedPosition.x, rotatedPosition.y, rotatedPosition.z);

					if (!nodes.ContainsKey(position))
					{
						var chunkPosition = new Vector3Int(x, y, z);
						var requestChunk = RequestChunk(chunkPosition);
						var node = new Node(chunkPosition, realPosition, chunkSize, requestChunk, this);
						requestChunk.AssignNode(node);
						nodes.Add(position, node);
					}
					else
					{
						nodes[position].PositionReal = realPosition;
					}

					VisiblePositions.Add(position);
				}

				foreach (var position in lastVisibleNodes)
				{
					if (VisiblePositions.Contains(position)) continue;
					nodes[position].Disable();
				}

				FrustrumCullVisibleNodes();
				return;
			}

			FrustrumCullVisibleNodes();
		}

		private void FrustrumCullVisibleNodes()
		{
			foreach (var nodePosition in VisiblePositions)
			{
				var node = nodes[nodePosition];

				if (mainCamera == null)
				{
					Debug.LogError("Cannot perform frustrum culling without a camera");
					node.EnableNode();
					continue;
				}
				if (!node.IsNodeVisible(cameraPlanes))
				{
					var distance = Vector3.Distance(playerTransform.position, node.PositionReal);
					if (distance > trueIgnoreCullDistance)
					{
						node.Disable();
						continue;
					}
				}

				node.EnableNode();
			}
		}

		private Chunk RequestChunk(Vector3Int position)
		{
			if (ChunkSetSaveLoadSystem.TryGetChunk(this, position, out var foundChunk)) return foundChunk;

			var requestedChunk = new Chunk(TERRAIN_SURFACE, smoothTerrain, flatShaded);
			requestedChunk.ChunkSize = chunkSize;
			ChunkSetSaveLoadSystem.AddChunkToChunkSet(this, position, requestedChunk);

			return requestedChunk;
		}

		public ChunkContainer RequestChunkContainer(Vector3 position, Node node, Chunk chunk)
		{
			ChunkContainer foundContainer = null;
			foreach (var chunkContainer in chunkContainers)
			{
				if (chunkContainer.IsUsed) continue;
				foundContainer = chunkContainer;
			}

			chunk.ChunkCore = this;

			if (foundContainer != null)
			{
				SetupChunkContainer(position, node, chunk, foundContainer);
				return foundContainer;
			}

			var requestedChunkContainer = Instantiate(chunkContainerPrefab, transform);
			requestedChunkContainer.ChunkCore = this;
			
			SetupChunkContainer(position, node, chunk, requestedChunkContainer);

			chunkContainers.Add(requestedChunkContainer);
			return requestedChunkContainer;
		}

		private void SetupChunkContainer(Vector3 position, Node node, Chunk chunk, ChunkContainer foundContainer)
		{
			foundContainer.AssignChunk(chunk, coreMaterial);
			foundContainer.transform.localPosition = position;

			if (chunk is { Hasdata: false })
			{
				node.Chunk.CreateAndQueueTerrainMapJob(
					node.Position,
					TrueWorldSize,
					octaves,
					weightedStrength,
					lacunarity,
					gain,
					octavesCaves,
					weightedStrengthCaves,
					lacunarityCaves,
					gainCaves,
					domainWarpAmp,
					terrainMapSize,
					seed);
				generatingNodes.Add(node);
			}
			else if (chunk != null)
			{
				foundContainer.CreateChunkMesh();
			}

			foundContainer.gameObject.SetActive(true);
		}

		public void ReturnChunkContainer(ChunkContainer chunkContainer)
		{
			chunkContainer.gameObject.SetActive(false);
			chunkContainer.UnAssignChunk();
		}

		public void EditNode(Node node, Vector3 hitPoint, float radius, bool add)
		{
			var neighbourNodes = GetNeighbourChunks(node.Position);

			if (neighbourNodes.Any(x => editingNodes.Contains(x)))
			{
				return;
			}

			if (queuedNodesCirclePoints.ContainsKey(node))
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
			hitPoint -= node.Position;
			
			var getCirclePointsJob = GetCirclePointJobs(new Vector3Int((int)hitPoint.x, (int)hitPoint.y, (int)hitPoint.z), arraySize, radius, add);
			var jobHandle = getCirclePointsJob.Schedule();
			queuedNodesCirclePoints.Add(node, new ChunkEditJobData(jobHandle, getCirclePointsJob, add));
			
			JobHandle.ScheduleBatchedJobs();
		}

		private IEnumerable<Node> GetNeighbourChunks(Vector3Int chunkPosition)
		{
			List<Node> foundChunks = new();
			for (var x = chunkPosition.x - chunkSize; x <= chunkPosition.x + chunkSize; x+= chunkSize)
			{
				for (var y = chunkPosition.y - chunkSize; y <= chunkPosition.y + chunkSize; y+= chunkSize)
				{
					for (var z = chunkPosition.z - chunkSize; z <= chunkPosition.z + chunkSize; z+= chunkSize)
					{
						if (nodes.TryGetValue(new Vector3Int(x, y, z), out var foundChunk))
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
	}
}
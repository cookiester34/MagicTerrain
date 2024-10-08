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

		private int queueUpdateCount;

		private readonly Dictionary<Vector3, Node> nodes = new();
		private readonly HashSet<Node> nodesToRemove = new();

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

		private int frameCount;

		private bool stopEverything;

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
			for (var i = 0; i < chunkContainerStartPoolCount; i++)
			{
				var requestedChunkContainer = Instantiate(chunkContainerPrefab, transform);
				requestedChunkContainer.ChunkCore = this;
				requestedChunkContainer.transform.position = Vector3.zero;
				chunkContainers.Add(requestedChunkContainer);
				requestedChunkContainer.gameObject.SetActive(false);
			}

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
			trueIgnoreCullDistance = ignoreFrustrumCullDistance * chunkSize;
			trueViewDistance = viewDistance * chunkSize;

			var chunkSizeDoubled = chunkSize * 2;
			terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;
		}

		private void Update()
		{
			// foreach (var node in nodesToRemove.ToArray())
			// {
			// 	if (!node.Generating) continue;
			// 	nodes.Remove(node.key);
			// 	nodesToRemove.Remove(node);
			// }

			CalculateVisibleNodes();

			ManageQueues();
		}

		private void ManageQueues()
		{
			ChecKGetCirclePointJobsQueue();

			CheckEditQueues();

			CheckGenerateQueues();

			frameCount++;
			if (frameCount % 5 <= 0)
			{
				frameCount = 0;
				DequeNodes();
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
				ChunkSetSaveLoadSystem.SaveOutOfRangeChunkSets(lastPlayerPosition, trueViewDistance);

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
						var node = new Node(position, chunkPosition, realPosition, chunkSize, requestChunk, this);
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
					if (nodes.ContainsKey(position)) nodes[position].Disable();
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
					Debug.LogError("Cannot perform frustum culling without a camera");
					node.EnableNode();
					continue;
				}

				var distance = Vector3.Distance(playerTransform.position, node.PositionReal);
				if (!node.IsNodeVisible(cameraPlanes))
				{
					if (distance > trueIgnoreCullDistance)
					{
						node.Disable();
						continue;
					}
				}

				switch (distance)
				{
					case <100:
						node.SetLodIndex(0);
						break;
					case <150:
						node.SetLodIndex(1);
						break;
					case <200:
						node.SetLodIndex(2);
						break;
					default:
						node.SetLodIndex(3);
						break;
				}

				node.EnableNode();
			}
		}

		public void RemoveNode(Node node)
		{
			if (!node.Generating)
			{
				nodes.Remove(node.key);
			}
			else
			{
				nodesToRemove.Add(node);
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
			if (chunkContainers.Count > 0)
			{
				foundContainer = chunkContainers[0];
				chunkContainers.Remove(foundContainer);
			}
			foundContainer ??= Instantiate(chunkContainerPrefab, transform);
			foundContainer.material = coreMaterial;

			chunk.ChunkCore = this;
			foundContainer.Node = node;
			if (chunk is { Hasdata: false })
			{
				queuedNodes.Add(node);
			}
			else
			{
				foundContainer.CreateChunkMesh();
			}

			SetupChunkContainer(position, foundContainer);
			return foundContainer;
		}

		private void SetupChunkContainer(Vector3 position, ChunkContainer foundContainer)
		{
			foundContainer.IsUsed = true;
			foundContainer.transform.localPosition = position;
			foundContainer.gameObject.SetActive(true);
		}

		public void ReturnChunkContainer(ChunkContainer chunkContainer)
		{
			chunkContainer.DisableContainer();
			chunkContainer.UnAssignChunk();
			chunkContainers.Add(chunkContainer);
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
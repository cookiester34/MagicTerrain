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
	public class ChunkCore : GravityInflucener
	{
		[field: SerializeField]
		public bool DebugMode { get; set; }

		[SerializeField]
		private int chunkSize = 32;

		[SerializeField]
		private float worldSize = 10;

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
		private bool smoothTerrain;

		[SerializeField]
		private bool flatShaded;

		[SerializeField]
		private Material coreMaterial;

		[SerializeField]
		private ChunkContainer chunkContainerPrefab;

		private bool forceUpdate = true;

		private Vector3 lastPlayerPosition;

		private int queueUpdateCount;

		private readonly Dictionary<Vector3, Node> nodes = new();

		private readonly List<Node> queuedNodes = new();

		private readonly Dictionary<Node, ChunkTerrainMapJobData> queuedNodesCheckTerrainMapCompletion = new();

		private readonly Dictionary<Node, ChunkMarchChunkJobData> queuedNodesCheckChunkJobCompletion = new();

		private readonly Dictionary<Node, ChunkEditJobData> queuedNodesCirclePoints = new();

		private readonly Dictionary<Node, EditTerrainMapJobData> queuedNodesTerrainMapEdit = new();

		private readonly List<ChunkContainer> chunkContainers = new();

		private readonly Dictionary<Vector3Int, Chunk> registeredChunks = new();

		private int terrainMapSize;

		private float TrueWorldSize => worldSize * chunkSize;

		private HashSet<Vector3> VisiblePositions { get; } = new();
		
		private Quaternion planetRotationLastUpdate;
		
		//debug info
		private Vector3 editedNodePointValuePosition;
		
		private List<EditedNodePointValue> editedNodePointValues = new();
		
		private Plane[] cameraPlanes;

		private int trueIgnoreCullDistance;
		
		private int trueViewDistance;

		private void Start()
		{
			base.Start();
			lastPlayerPosition = playerTransform.position;
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
		}

		private void OnDrawGizmos()
		{
			if (!DebugMode) return;

			editedNodePointValues.ForEach(editedNodePointValue =>
			{
				Gizmos.color = Color.red;
				Gizmos.DrawSphere(editedNodePointValuePosition + editedNodePointValue.PointPosition, 0.1f);
			});
		}

		private void ManageQueues()
		{
			ChecKGetCirclePointJobsQueue();

			CheckEditTerrainMapJobsQueue();

			#region ChunkCreationQueue

			queueUpdateCount++;
			if (queueUpdateCount % queueUpdateFrequency != 0) return;
			queueUpdateCount = 0;

			SchedualTerrainMapJobsQueue();

			SchedualMeshCreationJobsQueue();

			CheckMeshCreationJobQueue();

			#endregion
		}

		private void ChecKGetCirclePointJobsQueue()
		{
			if (queuedNodesCirclePoints.Count <= 0) return;

			List<Node> circleNodeToRemove = new();
			foreach (var (node, chunkEditJobData) in queuedNodesCirclePoints)
			{
				if (!chunkEditJobData.GetCirclePointsJobHandle.IsCompleted) continue;

				chunkEditJobData.GetCirclePointsJobHandle.Complete();

				var neighbourChunks = GetNeighbourChunks(node.Position);
				var isNeighbourChunkAlreadyQueued = false;
				foreach (var neighbourChunk in neighbourChunks)
				{
					if (queuedNodesTerrainMapEdit.ContainsKey(neighbourChunk))
					{
						isNeighbourChunkAlreadyQueued = true;
						break;
					}
				}
				if (isNeighbourChunkAlreadyQueued)
				{
					circleNodeToRemove.Add(node);
					continue;
				}

				var editedNodePointValues = chunkEditJobData.GetCirclePointsJob.points;
				this.editedNodePointValues.Clear();
				this.editedNodePointValues.AddRange(editedNodePointValues);
				editedNodePointValuePosition = node.Position;
				
				foreach (var neighbourNode in neighbourChunks)
				{
					var diferenceInPosition = node.Position - neighbourNode.Position;
					
					var terrainMapEditJob = new EditTerrainMapJob()
					{
						diferenceInPosition = diferenceInPosition,
						points = new NativeArray<EditedNodePointValue>(editedNodePointValues, Allocator.Persistent),
						add = chunkEditJobData.Add,
						chunkSize = chunkSize + 1,
						terrainMap = new NativeArray<float>(neighbourNode.Chunk.LocalTerrainMap, Allocator.Persistent),
						wasEdited = new NativeArray<bool>(1, Allocator.Persistent)
					};
					var jobHandler = terrainMapEditJob.Schedule(editedNodePointValues.Length, 60);
					JobHandle.ScheduleBatchedJobs();
				
					queuedNodesTerrainMapEdit.Add(neighbourNode, new EditTerrainMapJobData(jobHandler, terrainMapEditJob));
					neighbourNode.IsProccessing = true;
				}
				
				circleNodeToRemove.Add(node);
				chunkEditJobData.GetCirclePointsJob.points.Dispose();
			}

			foreach (var node in circleNodeToRemove)
			{
				queuedNodesCirclePoints.Remove(node);
			}
		}

		private void CheckEditTerrainMapJobsQueue()
		{
			if (queuedNodesTerrainMapEdit.Count <= 0) return;

			List<Node> terrainMapNodeToRemove = new();
			foreach (var (node, editTerrainMapJobData) in queuedNodesTerrainMapEdit)
			{
				if (!editTerrainMapJobData.EditTerrainMapJobHandle.IsCompleted) continue;

				editTerrainMapJobData.EditTerrainMapJobHandle.Complete();

				var wasEdited = editTerrainMapJobData.EditTerrainMapJob.wasEdited[0];
				if (wasEdited)
				{
					node.Chunk.LocalTerrainMap = editTerrainMapJobData.EditTerrainMapJob.terrainMap.ToArray();

					var meshDataJob = new MeshDataJob
					{
						chunkSize = chunkSize + 1,
						terrainMap = new NativeArray<float>(node.ChunkContainer.Chunk.LocalTerrainMap, Allocator.Persistent),
						terrainSurface = 0.5f,
						vertices = new NativeArray<Vector3>(900000, Allocator.Persistent),
						triangles = new NativeArray<int>(900000, Allocator.Persistent),
						cube = new NativeArray<float>(8, Allocator.Persistent),
						smoothTerrain = smoothTerrain,
						flatShaded = !smoothTerrain || flatShaded,
						triCount = new NativeArray<int>(1, Allocator.Persistent),
						vertCount = new NativeArray<int>(1, Allocator.Persistent)
					};
					var meshDataJobHandle = meshDataJob.Schedule();

					JobHandle.ScheduleBatchedJobs();
					queuedNodesCheckChunkJobCompletion.Add(node, new ChunkMarchChunkJobData(meshDataJobHandle, meshDataJob));
				}
				else
				{
					node.IsProccessing = false;
				}

				editTerrainMapJobData.EditTerrainMapJob.wasEdited.Dispose();
				editTerrainMapJobData.EditTerrainMapJob.terrainMap.Dispose();

				terrainMapNodeToRemove.Add(node);
			}

			foreach (var node in terrainMapNodeToRemove)
			{
				queuedNodesTerrainMapEdit.Remove(node);
			}
		}

		private void SchedualTerrainMapJobsQueue()
		{

			var playerPosition = playerTransform.position;
			if (queueDequeueLimit <= queuedNodesCheckTerrainMapCompletion.Count || queuedNodes.Count <= 0) return;

			var orderedEnumerable = queuedNodes.OrderBy(node =>
				Vector3.Distance(node.PositionReal, playerPosition));

			foreach (var node in orderedEnumerable)
			{
				if (queueDequeueLimit <= queuedNodesCheckTerrainMapCompletion.Count) break;

				if (node.ChunkContainer != null && node.ChunkContainer.Chunk != null)
				{
					node.IsQueued = true;
				}
				else
				{
					continue;
				}

				if (node.IsLoaded)
				{
					var terrainMapJob = new TerrainMapJob
					{
						chunkSize = chunkSize + 1,
						chunkPosition = node.Position,
						planetCenter = Vector3.zero,
						planetSize = TrueWorldSize,
						octaves = octaves,
						weightedStrength = weightedStrength,
						lacunarity = lacunarity,
						gain = gain,
						octavesCaves = octavesCaves,
						weightedStrengthCaves = weightedStrengthCaves,
						lacunarityCaves = lacunarityCaves,
						gainCaves = gainCaves,
						domainWarpAmp = domainWarpAmp,
						terrainMap = new NativeArray<float>(terrainMapSize, Allocator.Persistent),
						seed = seed
					};
					var terrainMapJobHandle = terrainMapJob.Schedule(chunkSize + 1, 200);
					JobHandle.ScheduleBatchedJobs();

					//TODO: Create a better way to handle this
					queuedNodesCheckTerrainMapCompletion.TryAdd(node,
						new ChunkTerrainMapJobData(terrainMapJobHandle, terrainMapJob));
				}

				//when chunk is done remove from queue
				queuedNodes.Remove(node);
			}
		}

		private void SchedualMeshCreationJobsQueue()
		{
			if (queuedNodesCheckTerrainMapCompletion.Count <= 0) return;

			List<Node> terrainMapNodeToRemove = new();
			foreach (var (node, creationQueueData) in queuedNodesCheckTerrainMapCompletion)
			{
				if (!creationQueueData.TerrainMapJobHandle.IsCompleted) continue;

				creationQueueData.TerrainMapJobHandle.Complete();

				node.Chunk.LocalTerrainMap = creationQueueData.TerrainMapJob.terrainMap.ToArray();

				var meshDataJob = new MeshDataJob
				{
					chunkSize = chunkSize + 1,
					terrainMap = new NativeArray<float>(node.ChunkContainer.Chunk.LocalTerrainMap,
						Allocator.Persistent),
					terrainSurface = 0.5f,
					vertices = new NativeArray<Vector3>(900000, Allocator.Persistent),
					triangles = new NativeArray<int>(900000, Allocator.Persistent),
					cube = new NativeArray<float>(8, Allocator.Persistent),
					smoothTerrain = smoothTerrain,
					flatShaded = !smoothTerrain || flatShaded,
					triCount = new NativeArray<int>(1, Allocator.Persistent),
					vertCount = new NativeArray<int>(1, Allocator.Persistent)
				};
				var meshDataJobHandle = meshDataJob.Schedule();
				creationQueueData.TerrainMapJob.terrainMap.Dispose();

				JobHandle.ScheduleBatchedJobs();
				queuedNodesCheckChunkJobCompletion.Add(node,
					new ChunkMarchChunkJobData(meshDataJobHandle, meshDataJob));

				terrainMapNodeToRemove.Add(node);
			}

			foreach (var node in terrainMapNodeToRemove)
			{
				queuedNodesCheckTerrainMapCompletion.Remove(node);
			}
		}

		private void CheckMeshCreationJobQueue()
		{
			if (queuedNodesCheckChunkJobCompletion.Count <= 0) return;

			List<Node> chunkCreationNodeToRemove = new();
			foreach (var (node, creationQueueData) in queuedNodesCheckChunkJobCompletion)
			{
				if (!creationQueueData.MeshDataJobHandle.IsCompleted) continue;

				creationQueueData.MeshDataJobHandle.Complete();

				var chunkContainerChunk = node.ChunkContainer.Chunk;
				var tCount = creationQueueData.MeshDataJob.triCount[0];
				chunkContainerChunk.ChunkTriangles = new int[tCount];

				var meshDataJob = creationQueueData.MeshDataJob;

				for (var i = 0; i < tCount; i++)
				{
					chunkContainerChunk.ChunkTriangles[i] = meshDataJob.triangles[i];
				}

				var vCount = creationQueueData.MeshDataJob.vertCount[0];
				chunkContainerChunk.ChunkVertices = new Vector3[vCount];
				for (var i = 0; i < vCount; i++)
				{
					chunkContainerChunk.ChunkVertices[i] = meshDataJob.vertices[i];
				}

				chunkContainerChunk.BuildMesh();

				creationQueueData.MeshDataJob.vertices.Dispose();
				creationQueueData.MeshDataJob.triangles.Dispose();
				creationQueueData.MeshDataJob.cube.Dispose();
				creationQueueData.MeshDataJob.triCount.Dispose();
				creationQueueData.MeshDataJob.vertCount.Dispose();
				creationQueueData.MeshDataJob.terrainMap.Dispose();

				node.ChunkContainer.CreateChunkMesh(coreMaterial);
				node.IsQueued = false;
				node.IsProccessing = false;

				if (node.IsDisabled)
				{
					node.ReturnChunk();
				}

				chunkCreationNodeToRemove.Add(node);
			}

			foreach (var octreeNode in chunkCreationNodeToRemove)
			{
				queuedNodesCheckChunkJobCompletion.Remove(octreeNode);
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
						nodes.Add(position,
							new Node(chunkPosition, realPosition, chunkSize, RequestChunk(chunkPosition), this));
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
			if (registeredChunks.TryGetValue(position, out var foundChunk)) return foundChunk;

			var requestedChunk = new Chunk();
			registeredChunks.Add(position, requestedChunk);
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

			if (foundContainer != null)
			{
				foundContainer.AssignChunk(chunk);
				foundContainer.transform.localPosition = position;

				if (chunk is { Hasdata: false })
				{
					queuedNodes.Add(node);
				}
				else if (chunk != null)
				{
					foundContainer.CreateChunkMesh(coreMaterial);
				}

				foundContainer.gameObject.SetActive(true);
				return foundContainer;
			}

			var requestedChunkContainer = Instantiate(chunkContainerPrefab, transform);
			requestedChunkContainer.ChunkCore = this;
			requestedChunkContainer.AssignChunk(chunk);
			requestedChunkContainer.transform.localPosition = position;

			if (chunk is { Hasdata: false })
			{
				queuedNodes.Add(node);
			}
			else if (chunk != null)
			{
				requestedChunkContainer.CreateChunkMesh(coreMaterial);
			}

			chunkContainers.Add(requestedChunkContainer);
			requestedChunkContainer.gameObject.SetActive(true);
			return requestedChunkContainer;
		}

		public void ReturnChunkContainer(ChunkContainer chunkContainer)
		{
			chunkContainer.gameObject.SetActive(false);
			chunkContainer.UnAssignChunk();
		}

		public void EditNode(Node node, Vector3 hitPoint, float radius, bool add)
		{
			var neighbourNodes = GetNeighbourChunks(node.Position);

			if (neighbourNodes.Any(x => x.IsProccessing)) return;

			if (queuedNodesCirclePoints.ContainsKey(node))
			{
				Debug.LogError("Failed to add chunk to edit queue, cancelling edit");
				return;
			}

			node.IsProccessing = true;
			var ceilToInt = Mathf.CeilToInt(radius) * 2 + 1;
			var arraySize = ceilToInt * ceilToInt * ceilToInt;
			
			if (arraySize <= 0)
			{
				node.IsProccessing = false;
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
				points = new NativeArray<EditedNodePointValue>(arraySize, Allocator.Persistent)
			};
			return getCirclePointsJob;
		}
	}
}
using SubModules.MagicTerrain.MagicTerrain_V2.Helpers;
using SubModules.MagicTerrain.MagicTerrain_V2.Jobs;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class ChunkCore : MonoBehaviour
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

		private bool forceUpdate = true;

		private Vector3 lastPlayerPosition;

		private int queueUpdateCount;

		private readonly List<Chunk> queuedChunkEdits = new();

		private readonly Dictionary<Vector3, Node> nodes = new();

		private readonly List<Node> queuedNodes = new();

		private readonly Dictionary<Node, ChunkTerrainMapJobData> queuedNodesCheckTerrainMapCompletion = new();

		private readonly Dictionary<Node, ChunkMarchChunkJobData> queuedNodesCheckChunkJobCompletion = new();

		private readonly List<ChunkContainer> chunkContainers = new();

		private readonly Dictionary<Vector3Int, Chunk> registeredChunks = new();

		private Vector3 CorePosition => transform.position;

		private int terrainMapSize;

		private float TrueWorldSize => worldSize * chunkSize;

		private HashSet<Vector3> visiblePositions { get; } = new();

		private void Start()
		{
			lastPlayerPosition = playerTransform.position;

			for (var i = 0; i < chunkContainerStartPoolCount; i++)
			{
				var chunkContainerObject = new GameObject("ChunkContainer");
				chunkContainerObject.transform.SetParent(transform);
				var requestedChunkContainer = chunkContainerObject.AddComponent<ChunkContainer>();
				requestedChunkContainer.transform.position = Vector3.zero;
				chunkContainers.Add(requestedChunkContainer);
				requestedChunkContainer.gameObject.SetActive(false);
			}
			
			var chunkSizeDoubled = chunkSize * 2;
			terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;
		}

		private void Update()
		{
			ManageQueues();

			CalculateVisibleNodes();
		}

		private void OnDrawGizmos()
		{
			if (!DebugMode) return;
		}

		private void ManageQueues()
		{
			for (var i = 0; i < queuedChunkEdits.Count; i++)
			{
				//RequestChunkEdit - if it rejects edit move to the back of list
				queuedChunkEdits.RemoveAt(0);
			}

			#region ChunkCreationQueue

			queueUpdateCount++;
			if (queueUpdateCount % queueUpdateFrequency != 0) return;

			queueUpdateCount = 0;

			var playerPosition = playerTransform.position;
			if (queueDequeueLimit > queuedNodesCheckTerrainMapCompletion.Count &&
			    queuedNodes.Count > 0)
			{
				var orderedEnumerable = queuedNodes.OrderBy(node =>
						Vector3.Distance(node.Position, playerPosition));

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
							planetCenter = CorePosition,
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

			if (queuedNodesCheckTerrainMapCompletion.Count > 0)
			{
				List<Node> terrainMapNodeToRemove = new();
				foreach (var (node, creationQueueData) in queuedNodesCheckTerrainMapCompletion)
				{
					if (!creationQueueData.TerrainMapJobHandle.IsCompleted) continue;

					creationQueueData.TerrainMapJobHandle.Complete();

					node.ChunkContainer.Chunk.LocalTerrainMap =
						creationQueueData.TerrainMapJob.terrainMap.ToArray();

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
			#endregion
		}

		private void CalculateVisibleNodes()
		{
			// if (mainCamera == null)
			// {
			// 	Debug.LogError("Cannot Calculate Visible Nodes without a camera");
			// 	var planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
			// 	return;
			// }
			
			var playerLastPositionRelative = transform.worldToLocalMatrix.MultiplyPoint(playerTransform.position);
			var playerPosition = new Vector3Int(
				Mathf.RoundToInt(playerLastPositionRelative.x).RoundOff(chunkSize),
				Mathf.RoundToInt(playerLastPositionRelative.y).RoundOff(chunkSize),
				Mathf.RoundToInt(playerLastPositionRelative.z).RoundOff(chunkSize));
			
			var distance = Vector3.Distance(lastPlayerPosition, playerPosition);
			if (!(distance >= updateDistance) && !forceUpdate) return;
			
			lastPlayerPosition = playerPosition;
			forceUpdate = false;

			var lastVisibleNodes = visiblePositions.ToList();
			visiblePositions.Clear();

			var trueViewDistance = viewDistance * chunkSize;
			
			for (var x = playerPosition.x - trueViewDistance; x < playerPosition.x + trueViewDistance; x += chunkSize)
			for (var y = playerPosition.y - trueViewDistance; y < playerPosition.y + trueViewDistance; y += chunkSize)
			for (var z = playerPosition.z - trueViewDistance; z < playerPosition.z + trueViewDistance; z += chunkSize)
			{
				var position = new Vector3(x,y,z);
				if (!nodes.ContainsKey(position))
				{
					var chunkPosition = new Vector3Int(x,y,z);
					nodes.Add(position, new Node(chunkPosition, chunkSize, RequestChunk(chunkPosition), this));
				}
				visiblePositions.Add(position);
			}
			
			foreach (var position in lastVisibleNodes)
			{
				if (visiblePositions.Contains(position)) continue;
				nodes[position].Disable();
			}

			foreach (var nodePosition in visiblePositions)
			{
				nodes[nodePosition].EnableNode();
			}
		}

		public Chunk RequestChunk(Vector3Int position)
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
				foundContainer.transform.position = position;

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

			var chunkContainerObject = new GameObject("ChunkContainer");
			chunkContainerObject.transform.SetParent(transform);
			var requestedChunkContainer = chunkContainerObject.AddComponent<ChunkContainer>();
			requestedChunkContainer.AssignChunk(chunk);
			requestedChunkContainer.transform.position = position;

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
	}
}
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
		private int rootNodeSize = 16;

		[SerializeField]
		private int viewDistance = 2;

		[SerializeField]
		private int updateDistance = 20;

		[SerializeField]
		private Transform playerTransform;

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

		private readonly Dictionary<OctreeNode, ChunkCreationQueueData> queuedOctreeNodes = new();
		
		private readonly Dictionary<OctreeNode, ChunkCreationQueueData> queuedOctreeNodesCheckTerrainMapCompletion = new();
		
		private readonly Dictionary<OctreeNode, ChunkCreationQueueData> queuedOctreeNodesCheckChunkJobCompletion = new();
		
		private readonly List<ChunkContainer> chunkContainers = new();

		private readonly Dictionary<Vector3Int, Chunk> registeredChunks = new();

		private readonly List<OctreeNode> rootNodes = new();
		
		private readonly List<ChunkContainer> chunksToBeDisabled = new();
			
		private readonly List<ChunkContainer> chunksToBeEnabled = new();
		
		private Vector3 CorePosition => transform.position;

		private int terrainMapSize;
		
		public float trueWorldSize => worldSize * chunkSize;
		
		public List<OctreeNode> VisibleNodes { get; } = new();

		private void Start()
		{
			lastPlayerPosition = playerTransform.position;

			for (var i = 0; i < chunkContainerStartPoolCount; i++) RequestChunkContainer(Vector3.zero, null, null);

			// Create the root nodes of the octree system
			var nodeSize = chunkSize * rootNodeSize;
			for (var x = CorePosition.x; x < CorePosition.x + trueWorldSize; x += nodeSize)
			for (var y = CorePosition.y; y < CorePosition.y + trueWorldSize; y += nodeSize)
			for (var z = CorePosition.z; z < CorePosition.z + trueWorldSize; z += nodeSize)
				rootNodes.Add(new OctreeNode(new Vector3Int((int)x, (int)y, (int)z), nodeSize, chunkSize, this));
			
			var chunkSizeDoubled = chunkSize * 2;
			terrainMapSize = chunkSizeDoubled * (chunkSizeDoubled * chunkSize);
		}

		private void Update()
		{
			ManageQueues();

			CalculateVisibleNodes();


			for (var i = 0; i < 10; i++)
			{
				if (chunksToBeDisabled.Count <= 0) continue;
				chunksToBeDisabled[0].gameObject.SetActive(false);
				chunksToBeDisabled.RemoveAt(0);
			}

			for (var i = 0; i < 10; i++)
			{
				if (chunksToBeEnabled.Count <= 0) continue;
				chunksToBeEnabled[0].gameObject.SetActive(true);
				chunksToBeEnabled.RemoveAt(0);
			}
		}

		private void OnDrawGizmos()
		{
			if (!DebugMode) return;
			// Draw the octree nodes in the Unity editor
			foreach (var rootNode in rootNodes) rootNode?.DrawGizmos();
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
			if (queueUpdateCount % queueUpdateFrequency == 0)
			{
				queueUpdateCount = 0;
				
				if (queueDequeueLimit > queuedOctreeNodesCheckTerrainMapCompletion.Count &&
				    queuedOctreeNodes.Count > 0)
				{
					var playerPosition = playerTransform.position;
					var orderedEnumerable =
						queuedOctreeNodes.OrderBy(keyValuePair =>
							Vector3.Distance(keyValuePair.Key.Position, playerPosition));

					foreach (var keyValuePair in orderedEnumerable)
					{
						if (queueDequeueLimit <= queuedOctreeNodesCheckTerrainMapCompletion.Count) break;

						var octreeNode = keyValuePair.Key;

						if (octreeNode?.ChunkContainer != null && octreeNode.ChunkContainer.Chunk != null)
						{
							octreeNode.IsQueued = true;
						}
						else
						{
							continue;
						}

						if (octreeNode.IsLoaded)
						{
							//Schedule chunk jobs
							var terrainMapJob = new TerrainMapJob
							{
								chunkSize = chunkSize + 1,
								chunkPosition = octreeNode.Position,
								planetCenter = CorePosition,
								planetSize = trueWorldSize,
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
							keyValuePair.Value.SetTerrainMapJob(terrainMapJob);
							keyValuePair.Value.SetTerrainMapJobHandle(terrainMapJob.Schedule(chunkSize + 1, 200));
								;
							JobHandle.ScheduleBatchedJobs();

							queuedOctreeNodesCheckTerrainMapCompletion.Add(octreeNode, keyValuePair.Value);
						}

						//when chunk is done remove from queue
						queuedOctreeNodes.Remove(octreeNode);
					}
				}

				if (queuedOctreeNodesCheckTerrainMapCompletion.Count > 0)
				{
					List<OctreeNode> terrainMapNodeToRemove = new();
					foreach (var chunkCreationQueueData in queuedOctreeNodesCheckTerrainMapCompletion)
					{
						var creationQueueData = chunkCreationQueueData.Value;
						if (creationQueueData.TerrainMapJobHandle.IsCompleted)
						{
							creationQueueData.TerrainMapJobHandle.Complete();

							var octreeNode = chunkCreationQueueData.Key;
							octreeNode.ChunkContainer.Chunk.LocalTerrainMap = creationQueueData.TerrainMapJob.terrainMap.ToArray();
							
							var meshDataJob = new MeshDataJob
							{
								chunkSize = chunkSize + 1,
								terrainMap = new NativeArray<float>(octreeNode.ChunkContainer.Chunk.LocalTerrainMap, Allocator.Persistent),
								terrainSurface = 0.5f,
								vertices = new NativeArray<Vector3>(900000, Allocator.Persistent),
								triangles = new NativeArray<int>(900000, Allocator.Persistent),
								cube = new NativeArray<float>(8, Allocator.Persistent),
								smoothTerrain = smoothTerrain,
								flatShaded = !smoothTerrain || flatShaded,
								triCount = new NativeArray<int>(1, Allocator.Persistent),
								vertCount = new NativeArray<int>(1, Allocator.Persistent)
							};
							creationQueueData.SetMeshDataJob(meshDataJob);
							creationQueueData.SetMarkChunkJobHandle(meshDataJob.Schedule());
							creationQueueData.TerrainMapJob.terrainMap.Dispose();
							
							JobHandle.ScheduleBatchedJobs();
							queuedOctreeNodesCheckChunkJobCompletion.Add(octreeNode, creationQueueData);
					
							terrainMapNodeToRemove.Add(octreeNode);
						}
					}
					
					foreach (var octreeNode in terrainMapNodeToRemove)
					{
						queuedOctreeNodesCheckTerrainMapCompletion.Remove(octreeNode);
					}
				}

				if (queuedOctreeNodesCheckChunkJobCompletion.Count > 0)
				{
					List<OctreeNode> chunkCreationNodeToRemove = new();
					foreach (var chunkCreationQueueData in queuedOctreeNodesCheckChunkJobCompletion)
					{
						var creationQueueData = chunkCreationQueueData.Value;
						if (creationQueueData.MarkChunkJobHandle.IsCompleted)
						{
							creationQueueData.MarkChunkJobHandle.Complete();
					
							var octreeNode = chunkCreationQueueData.Key;
							var chunkContainerChunk = octreeNode.ChunkContainer.Chunk;
							var tCount = creationQueueData.MeshDataJob.triCount[0];
							chunkContainerChunk.ChunkTriangles = new int[tCount];
							for (var i = 0; i < tCount; i++)
							{
								var meshDataJob = creationQueueData.MeshDataJob;
								chunkContainerChunk.ChunkTriangles[i] = meshDataJob.triangles[i];
							}
					
							var vCount = creationQueueData.MeshDataJob.vertCount[0];
							chunkContainerChunk.ChunkVertices = new Vector3[vCount];
							for (var i = 0; i < vCount; i++)
							{
								var meshDataJob = creationQueueData.MeshDataJob;
								chunkContainerChunk.ChunkVertices[i] = meshDataJob.vertices[i];
							}
					
							chunkContainerChunk.BuildMesh();
					
							creationQueueData.MeshDataJob.vertices.Dispose();
							creationQueueData.MeshDataJob.triangles.Dispose();
							creationQueueData.MeshDataJob.cube.Dispose();
							creationQueueData.MeshDataJob.triCount.Dispose();
							creationQueueData.MeshDataJob.vertCount.Dispose();
							creationQueueData.MeshDataJob.terrainMap.Dispose();
					
							octreeNode.ChunkContainer.CreateChunkMesh(coreMaterial);
							octreeNode.IsQueued = false;
					
							if (octreeNode.IsDisabled)
							{
								octreeNode.ReturnChunk();
							}
					
							chunkCreationNodeToRemove.Add(octreeNode);
						}
					}
					
					foreach (var octreeNode in chunkCreationNodeToRemove)
					{
						queuedOctreeNodesCheckChunkJobCompletion.Remove(octreeNode);
					}
				}
			}
			#endregion
		}

		private void CalculateVisibleNodes()
		{
			var playerPosition = playerTransform.position;
			var distance = Vector3.Distance(lastPlayerPosition, playerPosition);
			if (distance >= updateDistance || forceUpdate)
			{
				lastPlayerPosition = playerPosition;
				forceUpdate = false;

				MarkAllNonVisible();

				var lastVisibleNodes = VisibleNodes.ToList();
				VisibleNodes.Clear();
				
				var trueViewDistance = viewDistance * chunkSize;
				for (var x = playerPosition.x - trueViewDistance;
				     x < playerPosition.x + trueViewDistance;
				     x += chunkSize)
				for (var y = playerPosition.y - trueViewDistance;
				     y < playerPosition.y + trueViewDistance;
				     y += chunkSize)
				for (var z = playerPosition.z - trueViewDistance;
				     z < playerPosition.z + trueViewDistance;
				     z += chunkSize)
					EnableVisibleNodes(new Vector3(x, y, z));

				foreach (var node in VisibleNodes)
				{
					if (lastVisibleNodes.Contains(node))
					{
						lastVisibleNodes.Remove(node);
					}
				}
				
				DisableNonVisbleNodes(lastVisibleNodes);
			}
		}

		private void MarkAllNonVisible()
		{
			foreach (var node in VisibleNodes) node.SetNotVisible();
		}

		private void EnableVisibleNodes(Vector3 position)
		{
			// Calculate which nodes are visible and which are not
			// This is where the magic happens

			foreach (var rootNode in rootNodes) rootNode.EnableVisibleNodes(position);
		}

		private void DisableNonVisbleNodes(List<OctreeNode> lastVisibleNodes)
		{
			foreach (var node in lastVisibleNodes)
			{
				node.DisableNonVisibleNodes();
			}
		}

		public Chunk RequestChunk(Vector3Int position)
		{
			if (registeredChunks.TryGetValue(position, out var foundChunk)) return foundChunk;

			var requestedChunk = new Chunk(position);
			registeredChunks.Add(position, requestedChunk);
			return requestedChunk;
		}

		public ChunkContainer RequestChunkContainer(Vector3 position, OctreeNode octreeNode, Chunk chunk)
		{
			for (var i = 0; i < chunkContainers.Count; i++)
			{
				if (chunkContainers[i].IsUsed) continue;
				chunkContainers[i].AssignChunk(chunk);
				chunkContainers[i].transform.position = position;
				
				if (chunk is { Hasdata: false })
				{
					queuedOctreeNodes.TryAdd(octreeNode, new ChunkCreationQueueData());
				}
				else if (chunk != null)
				{
					chunkContainers[i].CreateChunkMesh(coreMaterial);
				}
				
				chunksToBeEnabled.Add(chunkContainers[i]);
				return chunkContainers[i];
			}

			var chunkContainerObject = new GameObject("ChunkContainer");
			chunkContainerObject.transform.SetParent(transform);
			var requestedChunkContainer = chunkContainerObject.AddComponent<ChunkContainer>();
			requestedChunkContainer.AssignChunk(chunk);
			requestedChunkContainer.transform.position = position;
			
			if (chunk is { Hasdata: false })
			{
				queuedOctreeNodes.TryAdd(octreeNode, new ChunkCreationQueueData());
			}
			else if (chunk != null)
			{
				requestedChunkContainer.CreateChunkMesh(coreMaterial);
			}
			
			chunkContainers.Add(requestedChunkContainer);
			chunksToBeEnabled.Add(requestedChunkContainer);
			return requestedChunkContainer;
		}

		private void AddChunk(Vector3 position)
		{
			// Find the appropriate octree node for the chunk and add it to that node
			foreach (var node in rootNodes.Select(rootNode => rootNode.FindNode(position))
				         .Where(node => node is
				         {
					         IsChunkNode: true
				         }))
			{
				node.RequestChunk();
				return;
			}
		}

		private void RemoveChunk(Vector3 position)
		{
			// Find the appropriate octree node for the chunk and remove it from that node
			OctreeNode node;
			foreach (var rootNode in rootNodes)
			{
				node = rootNode.FindNode(position);
				if (node is not { IsChunkNode: true }) continue;
				node.ReturnChunk();
				return;
			}
		}

		public void ReturnChunkContainer(ChunkContainer chunkContainer)
		{
			chunksToBeDisabled.Add(chunkContainer);
			chunkContainer.UnAssignChunk();
		}
	}
}
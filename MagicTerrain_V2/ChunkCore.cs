using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
    public class ChunkCore : MonoBehaviour
    {
        [field:SerializeField]
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

        private List<OctreeNode> visibleNodes = new();
        public List<OctreeNode> VisibleNodes => visibleNodes;

        private Vector3 lastPlayerPosition;

        private bool forceUpdate = true;

        private List<OctreeNode> rootNodes = new();

        private Dictionary<Vector3Int, Chunk> registeredChunks = new();

        private List<ChunkContainer> chunkContainers = new();

        private HashSet<OctreeNode> queuedOctreeNodes = new();

        private List<Chunk> queuedChunkEdits = new();

        private int queueUpdateCount;

        private void Start()
        {
            lastPlayerPosition = playerTransform.position;

            for (int i = 0; i < chunkContainerStartPoolCount; i++)
            {
                RequestChunkContainer(null);
            }

            // Create the root nodes of the octree system
            var trueWorldSize = worldSize * chunkSize;
            var nodeSize = chunkSize * rootNodeSize;
            for (var x = CorePosition.x -trueWorldSize; x < CorePosition.x + trueWorldSize; x+= nodeSize)
            for (var y = CorePosition.y -trueWorldSize; y < CorePosition.y + trueWorldSize; y+= nodeSize)
            for (var z = CorePosition.z -trueWorldSize; z < CorePosition.z + trueWorldSize; z+= nodeSize)
            {
                rootNodes.Add(new OctreeNode(new Vector3Int((int)x,(int)y,(int)z), nodeSize, chunkSize, this));
            }
        }

        private Vector3 CorePosition => transform.position;

        private void Update()
        {
            ManageQueues();

            CalculateVisibleNodes();
        }

        private void ManageQueues()
        {
            for (int i = 0; i < queuedChunkEdits.Count; i++)
            {
                //RequestChunkEdit - if it rejects edit move to the back of list
                //queuedChunkEdits[i];
                queuedChunkEdits.RemoveAt(0);
            }

            queueUpdateCount++;
            if (queueUpdateCount % queueUpdateFrequency == 0)
            {
                queueUpdateCount = 0;
                var playerPosition = playerTransform.position;
                var orderedEnumerable = queuedOctreeNodes.OrderBy(node => Vector3.Distance(node.Position, playerPosition));
                var count = 0;
                foreach (var octreeNode in orderedEnumerable)
                {
                    count++;
                    if (queueDequeueLimit <= count) break;


                    if (octreeNode.IsLoaded)
                    {
                        //Schedule chunk jobs
                        //octreeNode.ChunkContainer.Chunk
                    }
                    queuedOctreeNodes.Remove(octreeNode);
                }
            }
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
                {
                    EnableVisibleNodes(new Vector3(x, y, z));
                }

                DisableNonVisbleNodes();
            }
        }

        private void MarkAllNonVisible()
        {
            foreach (var node in visibleNodes)
            {
                node.SetNotVisible();
            }
        }

        private void EnableVisibleNodes(Vector3 position)
        {
            // Calculate which nodes are visible and which are not
            // This is where the magic happens

            foreach (var rootNode in rootNodes)
            {
                rootNode.EnableVisibleNodes(position);
            }
        }

        private void DisableNonVisbleNodes()
        {
            foreach (var rootNode in rootNodes)
            {
                rootNode.DisableNonVisibleNodes();
            }
        }

        public Chunk RequestChunk(Vector3Int position)
        {
            if (registeredChunks.TryGetValue(position, out var foundChunk))
            {
                return foundChunk;
            }

            var requestedChunk = new Chunk(position, chunkSize);
            registeredChunks.Add(position, requestedChunk);
            return requestedChunk;
        }

        public ChunkContainer RequestChunkContainer(Chunk chunk)
        {
            for (int i = 0; i < chunkContainers.Count; i++)
            {
                if (chunkContainers[i].IsUsed) continue;
                chunkContainers[i].AssignChunk(chunk);
                return chunkContainers[i];
            }

            var requestedChunkContainer = new ChunkContainer();
            requestedChunkContainer.AssignChunk(chunk);
            chunkContainers.Add(requestedChunkContainer);
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

        private void OnDrawGizmos()
        {
            if (!DebugMode) return;
            // Draw the octree nodes in the Unity editor
            foreach (var rootNode in rootNodes)
            {
                rootNode?.DrawGizmos();
            }
        }
    }
}
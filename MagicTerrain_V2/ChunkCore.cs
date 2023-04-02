using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
    public class ChunkCore : MonoBehaviour
    {
        private List<OctreeNode> rootNodes = new();
        
        [SerializeField]
        private int chunkSize = 32;
        [SerializeField]
        private int rootNodeSize = 16;
        [SerializeField]
        private int viewDistance = 10;
        [SerializeField]
        private Transform playerTransform;
        [SerializeField]
        private float worldSize = 10;

        private List<OctreeNode> visibleNodes = new();
        public List<OctreeNode> VisibleNodes => visibleNodes;

        [field:SerializeField]
        public bool DebugMode { get; set; }

        private void Start()
        {
            // Create the root nodes of the octree system
            var trueWorldSize = worldSize * chunkSize;
            var nodeSize = chunkSize * rootNodeSize;
            for (var x = CorePosition.x -trueWorldSize; x < CorePosition.x + trueWorldSize; x+= nodeSize)
            for (var y = CorePosition.y -trueWorldSize; y < CorePosition.y + trueWorldSize; y+= nodeSize)
            for (var z = CorePosition.z -trueWorldSize; z < CorePosition.z + trueWorldSize; z+= nodeSize)
            {
                rootNodes.Add(new OctreeNode(new Vector3(x,y,z), nodeSize, chunkSize, this));
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
            
        }

        private void CalculateVisibleNodes()
        {
            MarkAllNonVisible();

            var trueViewDistance = viewDistance * chunkSize;
            var playerPosition = playerTransform.position;
            for (var x = playerPosition.x - trueViewDistance; x < playerPosition.x + trueViewDistance; x += chunkSize)
            for (var y = playerPosition.y - trueViewDistance; y < playerPosition.y + trueViewDistance; y += chunkSize)
            for (var z = playerPosition.z - trueViewDistance; z < playerPosition.z + trueViewDistance; z += chunkSize)
            {
                EnableVisibleNodes(new Vector3(x, y, z));
            }

            DisableNonVisbleNodes();
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

        private void AddChunk(Vector3 position)
        {
            // Find the appropriate octree node for the chunk and add it to that node
            foreach (var node in rootNodes.Select(rootNode => rootNode.FindNode(position))
                         .Where(node => node is
                         {
                             IsChunkNode: true
                         }))
            {
                node.CreateChunk();
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
                //remove node
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

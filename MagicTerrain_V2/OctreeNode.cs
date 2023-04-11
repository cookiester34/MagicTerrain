using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class OctreeNode
	{
		private readonly OctreeNode[] children;

		private readonly ChunkCore chunkCore;

		private readonly int minSize;

		private Vector3Int position;

		private readonly int size;
		
		public Vector3Int Position => position;

		private ChunkContainer chunkContainer;
		public ChunkContainer ChunkContainer => chunkContainer;

		public bool IsChunkNode => size <= minSize;

		public bool IsLoaded { get; private set; }

		public bool IsDisabled { get; private set; }

		public bool IsEnabled => !IsDisabled;

		public bool IsVisible { get; private set; }
		
		public bool IsQueued { get; set; }

		public OctreeNode(Vector3Int position, int size, int minSize, ChunkCore chunkCore)
		{
			this.chunkCore = chunkCore;
			this.position = position;
			this.size = size;
			this.minSize = minSize;
			
			IsDisabled = true;
			IsLoaded = false;

			if (IsChunkNode) return;

			children = new OctreeNode[8];
			Subdivide();
		}

		public void RequestChunk()
		{
			chunkContainer = chunkCore.RequestChunkContainer(position, this, chunkCore.RequestChunk(position));
		}

		public void ReturnChunk()
		{
			if (chunkContainer == null) return;
			chunkCore.ReturnChunkContainer(chunkContainer);
			chunkContainer = null;
		}

		public void SetNotVisible()
		{
			IsVisible = false;
		}

		public void EnableVisibleNodes(Vector3 point)
		{
			if (IsVisible)
			{
				CheckIfChildNodesAreVisible(point);
				return;
			}

			if (!Contains(point)) return;

			CheckIfChildNodesAreVisible(point);

			if (IsChunkNode && !IsLoaded)
			{
				RequestChunk();
				IsLoaded = true;
			}

			IsDisabled = false;
			IsVisible = true;
			chunkCore.VisibleNodes.Add(this);
		}

		private void CheckIfChildNodesAreVisible(Vector3 point)
		{
			if (children == null) return;

			foreach (var node in children) node?.EnableVisibleNodes(point);
		}

		public void DisableNonVisibleNodes()
		{
			if (IsVisible || IsDisabled) return;
			IsDisabled = true;
			IsLoaded = false;
			if (chunkCore.VisibleNodes.Contains(this))
			{
				chunkCore.VisibleNodes.Remove(this);
			}
			if (IsChunkNode && !IsQueued)
			{
				ReturnChunk();
			}
		}

		public OctreeNode FindNode(Vector3 position)
		{
			if (!Contains(position)) return null;

			if (children?[0] == null) return this;

			var index = GetChildIndex(position);
			return children[index].FindNode(position);
		}

		private bool Contains(Vector3 position)
		{
			var halfNodeSize = size / 2;
			return position.x >= this.position.x - halfNodeSize && position.x <= this.position.x + halfNodeSize &&
			       position.y >= this.position.y - halfNodeSize && position.y <= this.position.y + halfNodeSize &&
			       position.z >= this.position.z - halfNodeSize && position.z <= this.position.z + halfNodeSize;
		}

		private void Subdivide()
		{
			var newNodeSize = size / 2;
			if (newNodeSize < minSize || children == null) return;

			var quarterSize = size / 4;

			children[0] = new OctreeNode(position + new Vector3Int(-quarterSize, -quarterSize, -quarterSize),
				newNodeSize, minSize, chunkCore);
			children[1] = new OctreeNode(position + new Vector3Int(quarterSize, -quarterSize, -quarterSize),
				newNodeSize, minSize, chunkCore);
			children[2] = new OctreeNode(position + new Vector3Int(-quarterSize, -quarterSize, quarterSize),
				newNodeSize, minSize, chunkCore);
			children[3] = new OctreeNode(position + new Vector3Int(quarterSize, -quarterSize, quarterSize), 
				newNodeSize, minSize, chunkCore);
			children[4] = new OctreeNode(position + new Vector3Int(-quarterSize, quarterSize, -quarterSize),
				newNodeSize, minSize, chunkCore);
			children[5] = new OctreeNode(position + new Vector3Int(quarterSize, quarterSize, -quarterSize), 
				newNodeSize, minSize, chunkCore);
			children[6] = new OctreeNode(position + new Vector3Int(-quarterSize, quarterSize, quarterSize), 
				newNodeSize, minSize, chunkCore);
			children[7] = new OctreeNode(position + new Vector3Int(quarterSize, quarterSize, quarterSize), 
				newNodeSize, minSize, chunkCore);
		}

		private int GetChildIndex(Vector3 position)
		{
			var index = 0;
			var halfNodeSize = size / 2;
			if (position.x >= position.x + halfNodeSize) index += 1;

			if (position.y >= position.y + halfNodeSize) index += 2;

			if (position.z >= position.z + halfNodeSize) index += 4;

			return index;
		}

		public void DrawGizmos()
		{
			if (IsDisabled) return;

			if (IsChunkNode)
			{
				Gizmos.color = IsDisabled ? Color.red : Color.cyan;
				var offset = new Vector3(size, size, size) / 2;
				Gizmos.DrawCube(position + offset, new Vector3(size, size, size) / 8);
			}
			else
			{
				Gizmos.color = IsDisabled ? Color.red : Color.cyan;
				Gizmos.DrawWireCube(position, new Vector3(size, size, size));
			}

			if (children?[0] == null) return;
			foreach (var child in children) child.DrawGizmos();
		}
	}
}
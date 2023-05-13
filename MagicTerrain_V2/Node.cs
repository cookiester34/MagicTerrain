using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Node
	{
		private readonly ChunkCore chunkCore;

		[SerializeField]
		private Chunk chunk;
		public Chunk Chunk
		{
			get => chunk;
			set => chunk = value;
		}

		[SerializeField]
		private Vector3Int position;
		public Vector3Int Position => position;
		
		[SerializeField]
		private Vector3 positionReal;
		public Vector3 PositionReal
		{
			get => positionReal;
			set => positionReal = value;
		}

		[SerializeField]
		private ChunkContainer chunkContainer;
		public ChunkContainer ChunkContainer => chunkContainer;

		[field:SerializeField]
		public bool IsLoaded { get; private set; }

		[field:SerializeField]
		public bool IsDisabled { get; private set; }

		[field:SerializeField]
		public bool IsVisible { get; private set; }
		
		[field:SerializeField]
		public bool IsQueued { get; set; }

		public bool IsProccessing { get; set; }

		private int size;

		public Node(Vector3Int position, Vector3 positionReal, int size, Chunk chunk, ChunkCore chunkCore)
		{
			this.size = size * 2;
			this.chunkCore = chunkCore;
			this.position = position;
			this.positionReal = positionReal;
			this.chunk = chunk;

			IsDisabled = true;
			IsLoaded = false;
			IsVisible = false;
		}

		public void RequestChunk()
		{
			chunkContainer = chunkCore.RequestChunkContainer(position, this, chunk);
			chunkContainer.Node = this;
		}

		public void ReturnChunk()
		{
			if (chunkContainer == null) return;
			chunkContainer.Node = null;
			chunkCore.ReturnChunkContainer(chunkContainer);
			chunkContainer = null;
		}

		public void EnableNode()
		{
			if (!IsDisabled) return;
			if (!IsLoaded)
			{
				IsLoaded = true;
				RequestChunk();
			}

			if (IsVisible) return;
			
			IsDisabled = false;
			IsVisible = true;
		}
		
		public bool IsNodeVisible(Plane[] planes)
		{
			var nodeBounds = new Bounds(positionReal, Vector3.one * size);
			// Check if the renderer is within the view frustum of the camera
			var visible = GeometryUtility.TestPlanesAABB(planes, nodeBounds);
			return visible;
		}

		public void Disable()
		{
			IsDisabled = true;
			IsLoaded = false;
			IsVisible = false;
			
			if (!IsQueued)
			{
				ReturnChunk();
			}
		}
	}
}
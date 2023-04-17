using System;
using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
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

		[field:SerializeField]
		private Bounds NodeBounds { get; set; }

		public bool IsProccessing { get; set; }

		public Node(Vector3Int position, int size, Chunk chunk, ChunkCore chunkCore)
		{
			this.chunkCore = chunkCore;
			this.position = position;
			this.chunk = chunk;
			NodeBounds = new Bounds(position, Vector3.one * size);
			
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
		
		private bool IsNodeVisible(Plane[] planes)
		{
			// Check if the renderer is within the view frustum of the camera
			var visible = GeometryUtility.TestPlanesAABB(planes, NodeBounds);
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
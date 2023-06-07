using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Node
	{
		private readonly ChunkCore chunkCore;

		private Chunk chunk;
		public Chunk Chunk
		{
			get => chunk;
			set => chunk = value;
		}

		public Vector3Int Position { get; }

		private Vector3 positionReal;
		public Vector3 PositionReal
		{
			get => positionReal;
			set => positionReal = value;
		}

		public ChunkContainer ChunkContainer { get; private set; }

		public bool IsLoaded { get; private set; }

		public bool IsDisabled { get; private set; }

		public bool IsVisible { get; private set; }

		private int size;

		public Node(Vector3Int position, Vector3 positionReal, int size, Chunk chunk, ChunkCore chunkCore)
		{
			this.size = size * 2;
			this.chunkCore = chunkCore;
			this.Position = position;
			this.positionReal = positionReal;
			this.chunk = chunk;

			IsDisabled = true;
			IsLoaded = false;
			IsVisible = false;
			
			RequestChunk();
		}

		public void RequestChunk()
		{
			ChunkContainer = chunkCore.RequestChunkContainer(Position, this, chunk);
			ChunkContainer.Node = this;
		}

		public void ReturnChunk()
		{
			if (ChunkContainer == null) return;
			ChunkContainer.Node = null;
			chunkCore.ReturnChunkContainer(ChunkContainer);
			ChunkContainer = null;
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
			
			ReturnChunk();
		}

		public async void CreateChunkMesh()
		{
			var count = 0;
			while (ChunkContainer == null)
			{
				if (count >= 20)
				{
					break;
				}
				count++;
				await Task.Yield();
			}
			if (ChunkContainer != null)
			{
				ChunkContainer.CreateChunkMesh();
			}
		}
	}
}
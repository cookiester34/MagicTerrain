﻿using System.Threading.Tasks;
using UnityEngine;

namespace MagicTerrain_V2
{
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
		
		public bool Generating { get; set; }
		
		public Vector3 key { get; }

		private int size;

		public Node(Vector3 key, Vector3Int position, Vector3 positionReal, int size, Chunk chunk, ChunkCore chunkCore)
		{
			this.key = key;
			this.size = size * 2;
			this.chunkCore = chunkCore;
			this.Position = position;
			this.positionReal = positionReal;
			this.chunk = chunk;

			IsDisabled = true;
			IsLoaded = false;
			IsVisible = false;
			
			RequestChunk();

			chunk.OnMeshJobDone += HandleMeshJobDone;
			chunk.OnDispose += DisposeNode;
		}

		private void HandleMeshJobDone(bool isGenerating)
		{
			CreateChunkMesh();
			Generating = !isGenerating;
			if (IsDisabled) ReturnChunk();
		}

		public void RequestChunk()
		{
			ChunkContainer = chunkCore.RequestChunkContainer(Position, this, chunk);
			ChunkContainer.EnableContainer();
		}

		public void ReturnChunk()
		{
			if (ChunkContainer == null) return;
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

		public void SetLodIndex(int index)
		{
			if (ChunkContainer != null)
			{
				ChunkContainer.LodIndex = index;
			}
		}
		
		public bool IsNodeVisible(Plane[] planes)
		{
			var nodeBounds = new Bounds(positionReal, Vector3.one * size); //if planets don't move this can be cached
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

		public void DisposeNode()
		{
			ReturnChunk();
			chunkCore.RemoveNode(this);
			chunk.OnMeshJobDone -= HandleMeshJobDone;
			chunk.OnDispose -= DisposeNode;
			Chunk = null;
		}
	}
}
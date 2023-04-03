using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class ChunkContainer
	{
		private Chunk chunk;
		public Chunk Chunk => chunk;

		private MeshRenderer meshRenderer;

		private MeshCollider meshCollider;

		public bool IsUsed => chunk != null;

		public ChunkContainer()
		{

		}

		public void AssignChunk(Chunk newChunk)
		{
			chunk = newChunk;
		}

		public void UnAssignChunk()
		{
			chunk = null;
		}
	}
}
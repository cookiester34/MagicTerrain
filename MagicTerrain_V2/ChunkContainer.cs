using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class ChunkContainer : MonoBehaviour
	{
		private MeshCollider meshCollider;

		private MeshRenderer meshRenderer;

		private MeshFilter meshFilter;

		[field:SerializeField]
		public Chunk Chunk { get; private set; }

		public bool IsUsed => Chunk != null;

		public void AssignChunk(Chunk newChunk)
		{
			Chunk = newChunk;
		}

		public void UnAssignChunk()
		{
			Chunk = null;
			CheckContainerHasComponents();
			meshFilter.sharedMesh = null;
			// meshCollider.sharedMesh = null;
		}

		public void CreateChunkMesh(Material material)
		{
			if (Chunk?.Meshes == null) return;
			if (Chunk.Meshes.Length == 0) return;
			CheckContainerHasComponents();
			meshRenderer.material = material;
			meshFilter.sharedMesh = Chunk.Meshes[0];
			// meshCollider.sharedMesh = Chunk.Meshes[0];
		}

		private void CheckContainerHasComponents()
		{
			if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
			if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
			if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
		}
	}
}
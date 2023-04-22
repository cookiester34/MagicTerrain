using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class ChunkContainer : MonoBehaviour
	{
		private MeshCollider meshCollider;

		private MeshRenderer meshRenderer;

		private MeshFilter meshFilter;

		public Chunk Chunk { get; private set; }
		
		public Node Node { get; set; }

		public bool IsUsed { get; set; }

		public ChunkCore ChunkCore { get; set; }

		public void AssignChunk(Chunk newChunk)
		{
			Chunk = newChunk;
			IsUsed = true;
		}

		public void UnAssignChunk()
		{
			Chunk = null;
			IsUsed = false;
			CheckContainerHasComponents();
			meshFilter.sharedMesh = null;
			meshCollider.sharedMesh = null;
		}

		public void CreateChunkMesh(Material material)
		{
			if (Chunk?.Meshes == null) return;
			if (Chunk.Meshes.Length == 0) return;
			CheckContainerHasComponents();
			meshRenderer.material = material;
			meshFilter.sharedMesh = Chunk.Meshes[0];
			meshCollider.sharedMesh = Chunk.Meshes[0];
		}

		private void CheckContainerHasComponents()
		{
			if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
			if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
			if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
		}

		public void EditChunk(Vector3Int hitPoint, float radius, bool add)
		{
			ChunkCore.EditNode(Node, new Vector3Int(hitPoint.x, hitPoint.y, hitPoint.z), radius, add);
		}
	}
}
using UnityEngine;

namespace MagicTerrain_V2
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

		private int lodIndex;
		public int LodIndex
		{
			get
			{
				return lodIndex;
			}
			set
			{
				if (lodIndex != value)
				{
					lodIndex = value;
					UpdateMeshLod();
				}
			}
		}

		private void Awake()
		{
			if (meshRenderer == null) meshRenderer = gameObject.GetComponent<MeshRenderer>();
			if (meshCollider == null) meshCollider = gameObject.GetComponent<MeshCollider>();
			if (meshFilter == null) meshFilter = gameObject.GetComponent<MeshFilter>();
		}

		public void DisableContainer()
		{
			meshRenderer.enabled = false;
		}

		public void EnableContainer()
		{
			meshRenderer.enabled = true;
		}

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
			meshFilter.sharedMesh = Chunk.Meshes[LodIndex];
			meshCollider.sharedMesh = Chunk.Meshes[LodIndex];
		}

		private void UpdateMeshLod()
		{
			if (Chunk?.Meshes == null) return;
			if (Chunk.Meshes.Length == 0) return;
			CheckContainerHasComponents();
			meshFilter.sharedMesh = Chunk.Meshes[LodIndex];
			meshCollider.sharedMesh = Chunk.Meshes[LodIndex];
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
using UnityEngine;

namespace MagicTerrain_V2
{
	public class ChunkContainer : MonoBehaviour
	{
		private MeshCollider meshCollider;

		private MeshRenderer meshRenderer;

		private MeshFilter meshFilter;

		private Material material;
		
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

		public void AssignMaterial(Material material)
		{
			this.material = material;
			IsUsed = true;
		}

		public void UnAssignChunk()
		{
			Node = null;
			IsUsed = false;
			CheckContainerHasComponents();
			meshFilter.sharedMesh = null;
			meshCollider.sharedMesh = null;
		}

		public void CreateChunkMesh()
		{
			var nodeChunk = Node.Chunk;
			if (nodeChunk?.Meshes == null) return;
			if (nodeChunk.Meshes.Length == 0) return;
			CheckContainerHasComponents();
			meshRenderer.material = material;
			meshFilter.sharedMesh = nodeChunk.Meshes[LodIndex];
			meshCollider.sharedMesh = nodeChunk.Meshes[LodIndex];
		}

		private void UpdateMeshLod()
		{
			var nodeChunk = Node.Chunk;
			if (nodeChunk?.Meshes == null) return;
			if (nodeChunk.Meshes.Length == 0) return;
			CheckContainerHasComponents();
			meshFilter.sharedMesh = nodeChunk.Meshes[LodIndex];
			meshCollider.sharedMesh = nodeChunk.Meshes[LodIndex];
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
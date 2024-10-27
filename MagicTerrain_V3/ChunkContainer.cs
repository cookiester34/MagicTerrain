using System;
using UnityEngine;

namespace MagicTerrain_V3
{
	public class ChunkContainer : MonoBehaviour
	{
		private MeshCollider meshCollider;

		private MeshRenderer meshRenderer;

		private MeshFilter meshFilter;

		internal Material material;
		
		public Chunk chunk { get; set; }

		public Action<Vector3, float, bool> OnEdit;

		private int lodIndex = 0;
		public int LodIndex
		{
			get => lodIndex;
			set
			{
				if (lodIndex == value) return;
				lodIndex = value;
				CreateChunkMesh();
			}
		}

		private void Awake()
		{
			meshCollider = GetComponent<MeshCollider>();
			meshRenderer = GetComponent<MeshRenderer>();
			meshFilter = GetComponent<MeshFilter>();
		}

		public void DisableContainer()
		{
			meshRenderer.enabled = false;
		}

		public void EnableContainer()
		{
			meshRenderer.enabled = true;
		}

		public void UnAssignChunk()
		{
			CheckContainerHasComponents();
			meshFilter.sharedMesh = null;
			meshCollider.sharedMesh = null;
		}

		public void CreateChunkMesh()
		{
			if (chunk?.Meshes == null)
			{
				Debug.LogWarning($"Chunk has no meshes");
				return;
			}
			if (chunk.Meshes.Length == 0)
			{
				Debug.LogError($"Critical Error, Chunk has no meshes");
				return;
			}
			
			CheckContainerHasComponents();
			var chunkMesh = chunk.Meshes[LodIndex];
			if (chunkMesh == null)
			{
				Debug.LogWarning($"Chunk has no mesh, at lod index {lodIndex}");
				return;
			}
			if (chunkMesh.vertexCount <= 0) return;
			meshFilter.sharedMesh = chunkMesh;
			meshCollider.sharedMesh = chunkMesh;
		}

		private void CheckContainerHasComponents()
		{
			if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
			if (meshRenderer.material != material) meshRenderer.material = material;
			if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
			if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
		}

		public void EditChunk(Vector3Int hitPoint, float radius, bool add)
		{
			OnEdit?.Invoke(new Vector3Int(hitPoint.x, hitPoint.y, hitPoint.z), radius, add);
		}
	}
}
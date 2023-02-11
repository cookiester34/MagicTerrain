using Scripts;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkContainer : MonoBehaviour
{
	internal bool markInactive;
	internal Chunk chunk;
	internal Vector3Int chunkPosition;
	internal bool chunkQueued;
	internal bool hasCollider;

	private MeshFilter meshFilter;
	private MeshRenderer meshRenderer;
	public MeshRenderer MeshRenderer => meshRenderer;
	private MeshCollider meshCollider;
	private Vector3 scale;

	public bool IsActive => !markInactive;

	public void SetScale(int newScale)
	{
		scale = new Vector3(newScale, newScale, newScale);
		transform.localScale = scale;
	}

	public void SetChunkPosition(Vector3Int position)
	{

		chunkPosition = new Vector3Int(position.x, position.y, position.z);
		transform.position = position;
	}

	public void SetChunkIndex()
	{
		CheckComponentsAreValid();

		if (!IsActive || chunk?.Meshes == null) return;
		meshFilter.sharedMesh = chunk.Meshes[0];

		meshRenderer.enabled = true;

		if (markInactive)
		{
			SetInactive();
		}
	}

	public void CreateCollider()
	{
		CheckComponentsAreValid();
		WaitForMesh();
	}

	private async void WaitForMesh()
	{
		while (chunk == null)
		{
			await Task.Yield();
		}

		while (chunk.Meshes == null)
		{
			await Task.Yield();
		}

		while (chunk.Meshes[0] == null)
		{
			await Task.Yield();
		}

		meshCollider.sharedMesh = chunk.Meshes[0];
		hasCollider = true;

	}

	private void CheckComponentsAreValid()
	{
		if (meshFilter == null)
		{
			meshFilter = gameObject.AddComponent<MeshFilter>();
		}

		if (meshRenderer == null)
		{
			meshRenderer = gameObject.AddComponent<MeshRenderer>();
		}

		if (meshCollider == null)
		{
			meshCollider = gameObject.AddComponent<MeshCollider>();
		}
	}

	public void SetMaterial(Material material)
	{
		CheckComponentsAreValid();
		meshRenderer.material = material;
	}

	public void MarkInactive()
	{
		markInactive = true;
		meshRenderer.enabled = false;
	}

	public void SetInactive()
	{
		markInactive = false;
		meshRenderer.enabled = false;
		meshFilter.sharedMesh = null;
		meshCollider.sharedMesh = null;
		chunkQueued = false;
		hasCollider = false;
	}

	public void SetActive()
	{
		markInactive = false;
		meshRenderer.enabled = true;
	}

	public void SetVisible(bool isVisible)
	{
		meshRenderer.enabled = isVisible;
	}

	public void EditChunk(List<EditedChunkPointValue> points, bool add = false)
	{
		chunk.EditChunk(points, add);
	}

	public void UpdateChunkMesh(bool generateCollider)
	{
		SetChunkIndex();
		if (generateCollider)
		{
			CreateCollider();
		}
	}
}
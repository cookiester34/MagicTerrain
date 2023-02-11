#region

using UnityEngine;

#endregion

public class VertexPush : MonoBehaviour
{
	public float pushSpeed = 1.0f;
	public float radius = 1.0f;
	public Camera camera;

	void Update()
	{
		Ray ray = camera.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit))
		{
			var mesh = hit.transform.GetComponent<MeshFilter>().mesh;
			Vector3 hitPoint = hit.point;
			Vector3[] vertices = mesh.vertices;
			int closestVertexIndex = -1;
			float closestDistanceSqr = Mathf.Infinity;
			for (int i = 0; i < vertices.Length; i++)
			{
				float distanceSqr = (vertices[i] - hitPoint).sqrMagnitude;
				if (distanceSqr < closestDistanceSqr)
				{
					closestDistanceSqr = distanceSqr;
					closestVertexIndex = i;
				}
			}
			if (closestVertexIndex != -1)
			{
				for (int i = 0; i < vertices.Length; i++)
				{
					float distanceSqr = (vertices[i] - hitPoint).sqrMagnitude;
					if (distanceSqr <= radius * radius)
					{
						vertices[i] += Vector3.up * pushSpeed * Time.deltaTime;
					}
				}
				mesh.vertices = vertices;
				mesh.RecalculateBounds();
			}
		}
	}
}
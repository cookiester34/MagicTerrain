using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public class PlanetController : MonoBehaviour
{
	[SerializeField]
	internal float planetSize;

	internal Vector3 planetCenter;
}
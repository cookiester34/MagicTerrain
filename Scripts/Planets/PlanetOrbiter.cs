using UnityEngine;

namespace Scripts.Planets
{
	public class PlanetOrbiter : MonoBehaviour
	{
		[SerializeField]
		private Transform pivotObject;
		
		[SerializeField]
		private float rotationSpeed = 0.001f;
		
		[SerializeField]
		private float rotationAngle = 45.0f;
		
		private void Update()
		{
			transform.RotateAround(pivotObject.position, Vector3.up, rotationSpeed * rotationAngle * Time.deltaTime);
		}
	}
}
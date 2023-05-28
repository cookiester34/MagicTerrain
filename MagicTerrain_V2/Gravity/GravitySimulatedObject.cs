using UnityEngine;

namespace MagicTerrain_V2.Gravity
{
	[RequireComponent(typeof(Rigidbody))]
	public class GravitySimulatedObject : MonoBehaviour
	{
		[field: SerializeField]
		public float MassKg { get; set; } = 5f;
		
		public bool IsAffectedByGravity { get; set; }
		public Vector3 GravityDirection { get; set; }
		public Rigidbody Rigidbody { get; private set; }

		private void Start()
		{
			Rigidbody = GetComponent<Rigidbody>();
			GravityManager.Instance.AddGravitySimulatedObject(this);
		}
	}
}
using UnityEngine;

namespace MagicTerrain_V2.Gravity
{
	public class GravityInflucener : MonoBehaviour
	{
		[field:SerializeField]
		public float GravityStrength { get; set; }
		
		[field:SerializeField]
		public float AirResistance { get; set; }

		protected void Start()
		{
			GravityManager.Instance.AddGravityInflucener(this);
		}
	}
}
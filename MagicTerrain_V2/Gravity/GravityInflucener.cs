using UnityEngine;

namespace MagicTerrain_V2.Gravity
{
	public class GravityInflucener : MonoBehaviour
	{
		[field:SerializeField]
		public float DensityKgPMCubbed { get; set; }
		
		[field:SerializeField]
		public float RadiusKm { get; set; }
		
		[field:SerializeField]
		public float AirResistance { get; set; }

		protected void Start()
		{
			GravityManager.Instance.AddGravityInflucener(this);
		}
	}
}
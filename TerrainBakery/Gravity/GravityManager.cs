using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerrainBakery.Gravity
{
	public class GravityManager : MonoBehaviour
	{
		public static GravityManager Instance { get; private set; }

		private void Awake()
		{
			Instance ??= this;
		}

		private class GravityObjects
		{
			public GravitySimulatedObject GravitySimulatedObject { get; set; }
			public GravityInflucener GravityInflucener { get; set; }
		}
		
		[SerializeField]
		private List<GravitySimulatedObject> gravitySimulatedObjects = new();
		
		[SerializeField]
		private List<GravityInflucener> gravityInfluceners = new();
		private readonly List<GravityObjects> gravityObjects = new();

		private int frame;

		public void AddGravitySimulatedObject(GravitySimulatedObject gravitySimulatedObject)
		{
			gravitySimulatedObjects.Add(gravitySimulatedObject);
		}
		
		public void AddGravityInflucener(GravityInflucener gravityInflucener)
		{
			gravityInfluceners.Add(gravityInflucener);
		}

		private void FixedUpdate()
		{
			foreach (var gravityObject in gravityObjects)
			{
				var gravityObjectGravitySimulatedObject = gravityObject.GravitySimulatedObject;
				var gravityObjectGravityInflucener = gravityObject.GravityInflucener;
				
				// Get the direction from sourceObject to targetObject
				var simulatedObjectPosition = gravityObjectGravitySimulatedObject.transform.position;
				var influcenerPosition = gravityObjectGravityInflucener.transform.position;
				Vector3 direction = (influcenerPosition - simulatedObjectPosition).normalized;
				gravityObjectGravitySimulatedObject.GravityDirection = direction;

				var distanceOfSimulatedObjectKm = Vector3.Distance(simulatedObjectPosition, influcenerPosition);
				var minimum = Mathf.Min(gravityObjectGravityInflucener.RadiusKm, distanceOfSimulatedObjectKm);
				
				var influencerVolume = 4.19f * Mathf.Pow(minimum, 3);
				var influencerMass = gravityObjectGravityInflucener.DensityKgPMCubbed * influencerVolume;

				var simulatedObjectMass = gravityObjectGravitySimulatedObject.MassKg;
				var gravitationalConstant = 6.67e-14f;

				var force = gravitationalConstant * (influencerMass * simulatedObjectMass /
				                                     Mathf.Pow(distanceOfSimulatedObjectKm, 2)) / 1000f;

				gravityObjectGravitySimulatedObject.Rigidbody.AddForce(direction * (Time.fixedDeltaTime * force), ForceMode.VelocityChange);

				ApplyResistance(gravityObjectGravitySimulatedObject, gravityObjectGravityInflucener);
			}

			frame++;
			if (frame % 30 == 0)
			{
				frame = 0;
				AssignGravityInfluencers();
			}
		}
		
		private void AssignGravityInfluencers()
		{
			if (gravityInfluceners.Count == 0 || gravitySimulatedObjects.Count == 0) return;
			
			gravityObjects.Clear();
			foreach (var gravitySimulatedObject in gravitySimulatedObjects)
			{
				var gravitySimulatedObjectPosition = gravitySimulatedObject.transform.position;
				//get the closest gravity influencer
				var closestInflucener = gravityInfluceners
					.OrderBy(influcener => Vector3.Distance(influcener.transform.position, gravitySimulatedObjectPosition))
					.FirstOrDefault();
				gravityObjects.Add(new GravityObjects{GravityInflucener = closestInflucener, GravitySimulatedObject = gravitySimulatedObject});
			}
		}
		
		private void ApplyResistance(GravitySimulatedObject gravitySimulatedObject, GravityInflucener gravityInflucener)
		{
			// Check if the object is moving downward (y component of velocity is negative)
			var rigidbody = gravitySimulatedObject.Rigidbody;
			if (!(rigidbody.velocity.y < 0)) return;
			
			// Calculate the resistance force direction (opposite to the current velocity)
			var resistanceDirection = -rigidbody.velocity.normalized;

			// Calculate the magnitude of the resistance force using a quadratic function
			var speed = rigidbody.velocity.magnitude;
			var resistanceMagnitude = (speed * gravityInflucener.AirResistance) / gravitySimulatedObject.MassKg;

			// Apply the resistance force to the Rigidbody
			rigidbody.AddForce(resistanceDirection * resistanceMagnitude, ForceMode.Force);
		}
	}
}
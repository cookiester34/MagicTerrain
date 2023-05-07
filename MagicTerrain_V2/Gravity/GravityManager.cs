using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicTerrain_V2.Gravity
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
			public GravityInflucener gravityInflucener { get; set; }
		}
		
		[SerializeField]
		private List<GravitySimulatedObject> gravitySimulatedObjects = new();
		
		[SerializeField]
		private List<GravityInflucener> gravityInfluceners = new();
		private List<GravityObjects> gravityObjects = new();

		private int frame;

		public void AddGravitySimulatedObject(GravitySimulatedObject gravitySimulatedObject)
		{
			gravitySimulatedObjects.Add(gravitySimulatedObject);
		}
		
		public void AddGravityInflucener(GravityInflucener gravityInflucener)
		{
			gravityInfluceners.Add(gravityInflucener);
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
				gravityObjects.Add(new GravityObjects{gravityInflucener = closestInflucener, GravitySimulatedObject = gravitySimulatedObject});
			}
		}

		private void FixedUpdate()
		{
			foreach (var gravityObject in gravityObjects)
			{
				var gravityObjectGravitySimulatedObject = gravityObject.GravitySimulatedObject;
				var gravityObjectGravityInflucener = gravityObject.gravityInflucener;
				
				// Get the direction from sourceObject to targetObject
				Vector3 direction = (gravityObjectGravityInflucener.transform.position - gravityObjectGravitySimulatedObject.transform.position).normalized;
				gravityObjectGravitySimulatedObject.GravityDirection = direction;
				gravityObjectGravitySimulatedObject.Rigidbody.AddForce(direction * Time.deltaTime * gravityObjectGravityInflucener.GravityStrength, ForceMode.VelocityChange);

				ApplyResistance(gravityObjectGravitySimulatedObject, gravityObjectGravityInflucener);

				ApplyRotation(gravityObjectGravitySimulatedObject, gravityObjectGravityInflucener);
			}

			frame++;
			if (frame % 30 == 0)
			{
				frame = 0;
				AssignGravityInfluencers();
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
			var resistanceMagnitude = (speed * gravityInflucener.AirResistance) / gravitySimulatedObject.ObjectWeight;

			// Apply the resistance force to the Rigidbody
			rigidbody.AddForce(resistanceDirection * resistanceMagnitude, ForceMode.Force);
		}

		private void ApplyRotation(GravitySimulatedObject gravitySimulatedObject, GravityInflucener gravityInflucener)
		{
			// Calculate the vector from the pivot point to the object
			var gravityInflucenerTransform = gravityInflucener.transform;
			var gravitySimulatedObjectTransform = gravitySimulatedObject.transform;
		}
	}
}
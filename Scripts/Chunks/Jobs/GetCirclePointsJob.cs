using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.Chunks.Jobs
{
	[BurstCompile]
	public struct GetCirclePointsJob : IJob
	{
		[ReadOnly]
		public Vector3 chunkPosition;
		[ReadOnly]
		public int scale;
		[ReadOnly]
		public Vector3 hitPosition;
		[ReadOnly]
		public bool add;
		[ReadOnly]
		public float radius;
		[ReadOnly]
		public Quaternion worldRotation;
		[ReadOnly]
		public Quaternion chunkRotation;
		[ReadOnly]
		public Vector3 worldPositon;

		public NativeArray<EditedChunkPointValue> points;

		private int arrayIndex;

		[BurstCompile]
		public void Execute()
		{
			arrayIndex = 0;
			var posInt = new Vector3(hitPosition.x, hitPosition.y, hitPosition.z);

			// Calculate the translation matrix
			Matrix4x4 translationMatrix = Matrix4x4.Translate(-worldPositon);

			// Calculate the rotation matrix
			Matrix4x4 rotationMatrix = Matrix4x4.Rotate(worldRotation);

			// Calculate the inverse translation matrix
			Matrix4x4 inverseTranslationMatrix = Matrix4x4.Translate(worldPositon);

			// Combine the matrices to create a single transformation matrix
			Matrix4x4 transformationMatrix = inverseTranslationMatrix * rotationMatrix * translationMatrix;

			// Multiply the point's position by the transformation matrix to get the rotated position
			Vector3 rotatedPosition = transformationMatrix.MultiplyPoint(chunkPosition);

			var chunkTranslation = Matrix4x4.Translate(rotatedPosition);
			var worldRotationMatrix = Matrix4x4.Rotate(worldRotation);
			var newRotation = chunkTranslation;


			var chunkScale = Matrix4x4.Scale(new Vector3(scale, scale, scale));
			var chunkTransformation = chunkScale * chunkTranslation.inverse;

			var radiusCeil = Mathf.CeilToInt(radius); // is 50
			for (var i = -radiusCeil; i <= radiusCeil; i++) // from -50 to 50
			{
				for (var j = -radiusCeil; j <= radiusCeil; j++)// from -50 to 50
				{
					for (var k = -radiusCeil; k <= radiusCeil; k++)// from -50 to 50
					{
						var gridPoint = new Vector3(posInt.x + i,posInt.y + j,posInt.z + k);
						var multiplyPoint = chunkTransformation.MultiplyPoint(
							new Vector3Int(
								(int)gridPoint.x,
								(int)gridPoint.y,
								(int)gridPoint.z));

						var distance = Vector3.Distance(posInt, gridPoint);
						var radiusEffector = add ? 1.05f : 0.9f;
						var t = 1f - Mathf.Exp(2f * (distance - radius * radiusEffector));
						var lerpedValue = Mathf.Lerp(add ? 1 : 0, add ? 0 : 1, t);
						if (Vector3.Distance(posInt, gridPoint) <= radius)
						{
							points[arrayIndex] = (new EditedChunkPointValue()
							{
								PointPosition = new Vector3Int((int)multiplyPoint.x,
									(int)multiplyPoint.y,
									(int)multiplyPoint.z),
								PointValue = lerpedValue
							});
							arrayIndex++;
						}
					}
				}
			}
		}
	}
}
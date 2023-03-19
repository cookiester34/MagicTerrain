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
		public Matrix4x4 worldMatrix;
		[ReadOnly]
		public int scale;
		[ReadOnly]
		public Vector3 hitPosition;
		[ReadOnly]
		public bool add;
		[ReadOnly]
		public float radius;
		
		public NativeArray<EditedChunkPointValue> points;
		
		private int arrayIndex;
			
		[BurstCompile]
		public void Execute()
		{
			arrayIndex = 0;
			var posInt = new Vector3(hitPosition.x, hitPosition.y, hitPosition.z);
			var roationMatrix = worldMatrix.rotation;
			var rotationToWorldMatrix =
				Matrix4x4.TRS(worldMatrix.GetColumn(3), roationMatrix, Vector3.one);
			var chunkTranslation = Matrix4x4.Translate(chunkPosition);
			var chunkScale = Matrix4x4.Scale(new Vector3(scale, scale, scale));
			var chunkTransformation = (chunkScale * chunkTranslation);
			
			var radiusCeil = Mathf.CeilToInt(radius);
			for (var i = -radiusCeil; i <= radiusCeil; i++)
			{
				for (var j = -radiusCeil; j <= radiusCeil; j++)
				{
					for (var k = -radiusCeil; k <= radiusCeil; k++)
					{
						var gridPoint = new Vector3(posInt.x + i,posInt.y + j,posInt.z + k);
						var multiplyPoint = (rotationToWorldMatrix * chunkTransformation).inverse.MultiplyPoint(
							new Vector3Int((int)gridPoint.x,
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
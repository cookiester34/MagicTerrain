using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MagicTerrain_V3.Jobs
{
	[BurstCompile]
	public struct GetCirclePointsJob : IJob, IChunkJob
	{
		[ReadOnly]
		public Vector3 hitPosition;
		[ReadOnly]
		public bool add;
		[ReadOnly]
		public float radius;

		public NativeArray<EditedNodePointValue> points;

		private int arrayIndex;

		[BurstCompile]
		public void Execute()
		{
			arrayIndex = 0;
			var radiusCeil = Mathf.CeilToInt(radius);
			
			for (var i = -radiusCeil; i <= radiusCeil; i++)
			{
				for (var j = -radiusCeil; j <= radiusCeil; j++)
				{
					for (var k = -radiusCeil; k <= radiusCeil; k++)
					{
						var gridPoint = hitPosition + new Vector3(i, j, k);
						var distance = Vector3.Distance(hitPosition, gridPoint);
						var radiusEffector = add ? 1.05f : 0.9f;
						var t = 1f - Mathf.Exp(2f * (distance - radius * radiusEffector));

						points[arrayIndex] = (new EditedNodePointValue()
						{
							PointPosition = new Vector3Int((int)gridPoint.x, (int)gridPoint.y, (int)gridPoint.z),
							PointTValue = t
						});

						arrayIndex++;
					}
				}
			}
		}
	}
}
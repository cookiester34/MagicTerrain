using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.Chunks.Jobs
{
	[BurstCompile]
	public struct EditTerrainMapJob : IJobParallelFor
	{
		[ReadOnly]
		public Vector3Int diferenceInPosition;

		[ReadOnly]
		public NativeArray<EditedChunkPointValue> points;

		[ReadOnly]
		public bool add;

		[ReadOnly]
		public int chunkSize;
		
		[NativeDisableParallelForRestriction]
		public NativeArray<bool> wasEdited;

		[NativeDisableParallelForRestriction]
		public NativeArray<float> terrainMap;

		[BurstCompile]
		public void Execute(int index)
		{
			var relativePosition = points[index].PointPosition + diferenceInPosition;

			if (relativePosition.x < 0 || relativePosition.y < 0 || relativePosition.z < 0
			    || relativePosition.x >= chunkSize || relativePosition.y >= chunkSize ||
			    relativePosition.z >= chunkSize) return; 
				
			var terrainMapIndex = relativePosition.x + chunkSize * (relativePosition.y + chunkSize * relativePosition.z);

			var isWithinBounds = terrainMapIndex >= 0 && terrainMapIndex < terrainMap.Length;

			if (!isWithinBounds) return;
				
			if (add)
			{
				if (terrainMap[terrainMapIndex] <= points[index].PointValue)
					return;
			}
			else
			{
				if (terrainMap[terrainMapIndex] >= points[index].PointValue)
					return;
			}

			wasEdited[0] = true;
			terrainMap[terrainMapIndex] = points[index].PointValue;
		}
	}
}
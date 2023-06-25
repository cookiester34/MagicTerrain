using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace TerrainBakery.Jobs
{
	[BurstCompile]
	public struct EditTerrainMapJob : IJobParallelFor, IChunkJob
	{
		[ReadOnly]
		public Vector3Int diferenceInPosition;

		[ReadOnly]
		public NativeArray<EditedNodePointValue> points;

		[ReadOnly]
		public bool add;

		[ReadOnly]
		public int chunkSize;

		[NativeDisableContainerSafetyRestriction]
		public NativeArray<bool> wasEdited;

		[NativeDisableContainerSafetyRestriction]
		public NativeArray<float> terrainMap;

		[BurstCompile]
		public void Execute(int index)
		{
			var editedChunkPointValue = points[index];
			//get the point relative to this chunk
			var relativePosition = editedChunkPointValue.PointPosition + diferenceInPosition;

			//check if the point is within the chunk
			if (relativePosition.x < 0 || relativePosition.y < 0 || relativePosition.z < 0
			    || relativePosition.x >= chunkSize || relativePosition.y >= chunkSize || relativePosition.z >= chunkSize) return;

			var terrainMapIndex = relativePosition.x + chunkSize * (relativePosition.y + chunkSize * relativePosition.z);

			var isWithinBounds = terrainMapIndex >= 0 && terrainMapIndex < terrainMap.Length;

			if (!isWithinBounds) return;

			var pointValue = editedChunkPointValue.PointTValue;
			var terrain = terrainMap[terrainMapIndex];
			var lerpValue = Mathf.Lerp(terrain, add ? 0 : 1, 0.04f * pointValue);
			terrainMap[terrainMapIndex] = lerpValue;
			wasEdited[0] = true;
		}
	}
}
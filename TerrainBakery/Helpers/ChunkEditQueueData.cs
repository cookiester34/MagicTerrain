using TerrainBakery.Jobs;
using Unity.Jobs;

namespace TerrainBakery.Helpers
{
	public struct ChunkEditJobData
	{
		public JobHandle GetCirclePointsJobHandle { get; }
		public GetCirclePointsJob GetCirclePointsJob { get; }
		public bool Add { get; }
		
		public ChunkEditJobData(JobHandle getCirclePointsJobHandle, GetCirclePointsJob getCirclePointsJob, bool add)
		{
			GetCirclePointsJobHandle = getCirclePointsJobHandle;
			GetCirclePointsJob = getCirclePointsJob;
			Add = add;
		}
	}
}
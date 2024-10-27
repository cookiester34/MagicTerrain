using MagicTerrain_V3.Jobs;
using Unity.Jobs;

namespace MagicTerrain_V3.Helpers
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
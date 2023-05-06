using MagicTerrain_V2.Jobs;
using System;
using Unity.Jobs;

namespace MagicTerrain_V2.Helpers
{
	[Serializable]
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
	
	[Serializable]
	public struct EditTerrainMapJobData
	{
		public JobHandle EditTerrainMapJobHandle { get; }
		public EditTerrainMapJob EditTerrainMapJob { get; }

		public EditTerrainMapJobData(JobHandle editTerrainMapJobHandle, EditTerrainMapJob editTerrainMapJob)
		{
			EditTerrainMapJobHandle = editTerrainMapJobHandle;
			EditTerrainMapJob = editTerrainMapJob;
		}
	}
}
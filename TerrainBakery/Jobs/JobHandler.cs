using Unity.Jobs;

namespace TerrainBakery.Jobs
{
	public class JobHandler
	{
		public JobHandle JobHandle { get; private set; }
		public IChunkJob ChunkJob { get; set; }
		public int JobFrameCount { get; private set; }
		public bool IsInProcess { get; set; }

		public void StartJob(JobHandle jobHandle, IChunkJob chunkJob)
		{
			JobHandle = jobHandle;
			ChunkJob = chunkJob;
			JobHandle.ScheduleBatchedJobs();
			JobFrameCount = 0;
			IsInProcess = true;
		}

		public bool CheckCompletion()
		{
			JobFrameCount++;
			if (JobHandle.IsCompleted || JobFrameCount >= 3)
			{
				//cannot have jobs existing longer than 4 frames,
				//if job has reached 4 frames complete it.
				JobHandle.Complete();
				IsInProcess = false;
			}

			return JobHandle.IsCompleted;
		}

		public void ForceCompletion()
		{
			if (!IsInProcess) return;
			JobHandle.Complete();
			IsInProcess = false;
			switch (ChunkJob)
			{
				case EditTerrainMapJob editTerrainMapJob:
					editTerrainMapJob.terrainMap.Dispose();
					editTerrainMapJob.wasEdited.Dispose();
					editTerrainMapJob.points.Dispose();
					break;
				case TerrainMapJob terrainMapJob:
					terrainMapJob.terrainMap.Dispose();
					break;
				case MeshDataJob meshDataJob:
					meshDataJob.cube.Dispose();
					meshDataJob.terrainMap.Dispose();
					meshDataJob.triCount.Dispose();
					meshDataJob.vertCount.Dispose();
					meshDataJob.vertices.Dispose();
					meshDataJob.triangles.Dispose();
					break;
			}
		}

		public void Dispose()
		{
			ForceCompletion();
			ChunkJob = null;
		}
	}
}
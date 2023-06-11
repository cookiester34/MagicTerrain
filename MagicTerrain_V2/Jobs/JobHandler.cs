using Unity.Jobs;

namespace MagicTerrain_V2.Jobs
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

		public void Dispose()
		{
			ChunkJob = null;
		}
	}
}
using SubModules.MagicTerrain.MagicTerrain_V2.Jobs;
using System;
using Unity.Jobs;

namespace SubModules.MagicTerrain.MagicTerrain_V2.Helpers
{
	[Serializable]
	public class ChunkCreationQueueData
	{
		public JobHandle TerrainMapJobHandle { get; set; }
		public JobHandle MarkChunkJobHandle { get; set; }
		public TerrainMapJob TerrainMapJob { get; set; }
		public MeshDataJob MeshDataJob { get; set; }
		

		public void SetTerrainMapJob(TerrainMapJob terrainMapJob)
		{
			TerrainMapJob = terrainMapJob;
		}
		
		public void SetTerrainMapJobHandle(JobHandle terrainMapJobHandle)
		{
			TerrainMapJobHandle = terrainMapJobHandle;
		}
		
		public void SetMeshDataJob(MeshDataJob meshDataJob)
		{
			MeshDataJob = meshDataJob;
		}
		
		public void SetMarkChunkJobHandle(JobHandle markChunkJobHandle)
		{
			MarkChunkJobHandle = markChunkJobHandle;
		}
	}
}
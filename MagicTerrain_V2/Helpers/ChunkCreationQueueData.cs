using SubModules.MagicTerrain.MagicTerrain_V2.Jobs;
using System;
using Unity.Jobs;

namespace SubModules.MagicTerrain.MagicTerrain_V2.Helpers
{
	[Serializable]
	public struct ChunkTerrainMapJobData
	{
		public JobHandle TerrainMapJobHandle { get; }
		public TerrainMapJob TerrainMapJob { get; }
		
		public ChunkTerrainMapJobData(JobHandle terrainMapJobHandle, TerrainMapJob terrainMapJob)
		{
			TerrainMapJobHandle = terrainMapJobHandle;
			TerrainMapJob = terrainMapJob;
		}
	}
	
	[Serializable]
	public struct ChunkMarchChunkJobData
	{
		public JobHandle MeshDataJobHandle { get; }
		public MeshDataJob MeshDataJob { get; }
		
		public ChunkMarchChunkJobData(JobHandle meshDataJobHandle, MeshDataJob meshDataJob)
		{
			MeshDataJobHandle = meshDataJobHandle;
			MeshDataJob = meshDataJob;
		}
	}
}
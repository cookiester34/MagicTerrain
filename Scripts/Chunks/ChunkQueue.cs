using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunkQueue
	{
		private ChunkManager chunkManager;
		private List<ChunkContainer> chunksInQueue = new();
		private PlanetController planetController;
		private List<Chunk> chunkQueue;
		private int chunkProccessingLimit;
		private int currentProccessingCount;

		public ChunkQueue(int queueLimit, ChunkManager chunkManager, PlanetController planetController = null)
		{
			this.chunkManager = chunkManager;
			this.planetController = planetController;
			chunkProccessingLimit = chunkManager.ChunkProccessingLimit;
		}

		public bool DoesQueueContain(ChunkContainer chunkContainer)
		{
			return chunksInQueue.Contains(chunkContainer);
		}

		public void AddChunkToQueue(ChunkContainer chunkContainer)
		{
			chunksInQueue.Add(chunkContainer);
		}

		public void CheckIfValidQueueReady(Vector3 playerPosition)
		{
			chunksInQueue = chunksInQueue.OrderBy(container => Vector3.Distance(container.chunkPosition, playerPosition)).ToList();

			//removed chunks not needed
			for (var index = chunksInQueue.Count - 1; index >= 0; index--)
			{
				var chunkContainer = chunksInQueue[index];
				if (!chunkContainer.markInactive) continue;

				chunkManager.knownKeys.Remove(chunkContainer.chunkPosition);
				chunksInQueue.RemoveAt(index);
				chunkContainer.SetInactive();
			}

			if (chunksInQueue.Any())
			{
				var chunksToQueueCount = chunkProccessingLimit - currentProccessingCount;
				for (int index = 0; index < chunksToQueueCount; index++)
				{
					ScheduleChunk();
				}
			}
		}

		private void ScheduleChunk()
		{
			if (chunksInQueue.Count <= 0) return;
			var chunkContainer = chunksInQueue[0];

			var chunkContainerChunkPosition = chunkContainer.chunkPosition;
			var chunk = new Chunk(new ChunkData
			{
				seed = chunkManager.Seed,
				chunkPosition = chunkContainerChunkPosition,
				chunkSize = chunkManager.ChunkSize,
				scale = chunkManager.ChunkScale,
				planetCenter = planetController == null ? Vector3.zero : planetController.planetCenter,
				planetSize = planetController == null ? 0 : planetController.planetSize,
				octaves = chunkManager.octaves,
				lacunarity = chunkManager.lacunarity,
				weightedStrength = chunkManager.weightedStrength,
				gain = chunkManager.gain,
				domainWarpAmp = chunkManager.domainWarpAmp,
				octavesCaves = chunkManager.octavesCaves,
				weightedStrengthCaves = chunkManager.weightedStrengthCaves,
				lacunarityCaves = chunkManager.lacunarityCaves,
				gainCaves = chunkManager.gainCaves,
				chunkManager = chunkManager
			},chunkManager.SmoothTerrain, chunkManager.FlatShaded);

			if (!chunkManager.Chunks.TryAdd(chunkContainerChunkPosition, chunk))
			{
				Debug.LogError($"Trying to create duplicate chunk at: {chunkContainerChunkPosition}");
				return;
			}

			chunkContainer.chunk = chunk;
			chunk.CurrentChunkContainer = chunkContainer;

			chunk.ScheduleChunkJobs(ChunkDone, false);
			currentProccessingCount++;
			chunksInQueue.RemoveAt(0);
		}

		private void ChunkDone() => currentProccessingCount--;
	}
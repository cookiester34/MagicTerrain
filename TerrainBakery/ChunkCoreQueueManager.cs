using System;
using System.Collections.Generic;
using System.Linq;
using TerrainBakery.Helpers;
using UnityEngine;

namespace TerrainBakery
{
	public partial class ChunkCore
	{
		private HashSet<Chunk> queuedChunks = new();
		private List<Chunk> chunksGenerating = new();
		
		private HashSet<Chunk> chunksEditing = new();
		private readonly Dictionary<Chunk, ChunkEditJobData> queuedNodesCirclePoints = new();

		private bool EmptyActionCalled;
		public event Action OnQueuesEmpty;

		public void ManageQueues(Vector3 playerPosition)
		{
			DequeNodes(playerPosition);

			ChecKGetCirclePointJobsQueue();

			CheckEditQueues();

			CheckGenerateQueues();

			if (!EmptyActionCalled && chunksGenerating.Count <= 0)
			{
				OnQueuesEmpty?.Invoke();
				EmptyActionCalled = true;
			}
		}
		
		private void DequeNodes(Vector3 playerPosition)
		{
			if (chunksGenerating.Count <= 25)
			{
				var playerTransformPosition = playerPosition;
				var orderedNodes = queuedChunks.OrderBy(node => Vector3.Distance(node.Position, playerTransformPosition)).ToArray();
				foreach (var chunk in orderedNodes)
				{
					if (chunksGenerating.Count > 20) break;
					if (chunk != null)
					{
						chunksGenerating.Add(chunk);
						chunk.CreateAndQueueTerrainMapJob(
							chunk.Position,
							RadiusKm,
							octaves,
							weightedStrength,
							lacunarity,
							gain,
							octavesCaves,
							weightedStrengthCaves,
							lacunarityCaves,
							gainCaves,
							domainWarpAmp,
							seed);
					}
					queuedChunks.Remove(chunk);
				}
			}
		}
		
		private void ChecKGetCirclePointJobsQueue()
		{
			if (queuedNodesCirclePoints.Count <= 0) return;

			List<Chunk> circleNodeToRemove = new();
			foreach (var (node, chunkEditJobData) in queuedNodesCirclePoints)
			{
				if (!chunkEditJobData.GetCirclePointsJobHandle.IsCompleted) continue;

				chunkEditJobData.GetCirclePointsJobHandle.Complete();

				var neighbourChunks = GetNeighbourChunks(node.Position);
				var isNeighbourChunkAlreadyQueued = neighbourChunks.Any(neighbourNode =>
				{
					return neighbourNode != node && chunksEditing.Contains(neighbourNode);
				});

				if (isNeighbourChunkAlreadyQueued)
				{
					circleNodeToRemove.Add(node);
					chunkEditJobData.GetCirclePointsJob.points.Dispose();
					Debug.LogError($"Neighbour Chunk at {node.Position} is already being processed");
					continue;
				}

				if (neighbourChunks.Any(chunk => chunk.LocalTerrainMap == null))
				{
					circleNodeToRemove.Add(node);
					chunkEditJobData.GetCirclePointsJob.points.Dispose();
					Debug.LogError($"Neighbour chunk at {node.Position} terrain map is null");
					continue;
				}

				var editedNodePointValues = chunkEditJobData.GetCirclePointsJob.points;
				
				if (DebugMode)
				{
					this.editedNodePointValues.Clear();
					this.editedNodePointValues.AddRange(editedNodePointValues);
					editedNodePointValuePosition = node.Position;
				}

				foreach (var neighbourNode in neighbourChunks)
				{
					var diferenceInPosition = node.Position - neighbourNode.Position;
					neighbourNode.CreateAndQueueEditTerrainMapJob(diferenceInPosition, editedNodePointValues, chunkEditJobData.Add);
					chunksEditing.Add(neighbourNode);
				}

				circleNodeToRemove.Add(node);
				chunkEditJobData.GetCirclePointsJob.points.Dispose();
			}

			foreach (var node in circleNodeToRemove)
			{
				queuedNodesCirclePoints.Remove(node);
			}
		}
		
		private void CheckEditQueues()
		{
			List<Chunk> nodesToRemove = new();
			foreach (var chunk in chunksEditing)
			{
				if (chunk.CheckJobComplete())
				{
					nodesToRemove.Add(chunk);
				}
			}

			foreach (var node in nodesToRemove)
			{
				chunksEditing.Remove(node);
			}
		}

		private void CheckGenerateQueues()
		{
			List<Chunk> nodesToRemove = new();
			foreach (var chunk in chunksGenerating)
			{
				if (chunk.CheckJobComplete())
				{
					nodesToRemove.Add(chunk);
				}
			}

			foreach (var node in nodesToRemove)
			{
				chunksGenerating.Remove(node);
			}
		}
	}
}
using MagicTerrain_V2.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicTerrain_V2
{
	public partial class ChunkCore
	{
		internal const float TERRAIN_SURFACE = 0.4f;

		private HashSet<Node> queuedNodes = new();
		private List<Node> nodeGenerating = new();

		private HashSet<Node> generatingNodes = new();
		private HashSet<Node> editingNodes = new();
		private readonly Dictionary<Node, ChunkEditJobData> queuedNodesCirclePoints = new();

		public Action OnQueuedChunksDone;
		private bool queuedChunksAlreadyDone;

		private void DequeNodes()
		{
			if (nodeGenerating.Count <= 25)
			{
				var playerTransformPosition = playerTransform.position;
				var orderedNodes = queuedNodes.OrderBy(node => Vector3.Distance(node.Position, playerTransformPosition)).ToArray();
				foreach (var node in orderedNodes)
				{
					queuedChunksAlreadyDone = false;
					if (nodeGenerating.Count > 20) break;
					if (node.Chunk != null)
					{
						nodeGenerating.Add(node);
						node.Chunk.CreateAndQueueTerrainMapJob(
							node.Position,
							TrueWorldSize,
							octaves,
							weightedStrength,
							lacunarity,
							gain,
							octavesCaves,
							weightedStrengthCaves,
							lacunarityCaves,
							gainCaves,
							domainWarpAmp,
							terrainMapSize,
							seed);
						generatingNodes.Add(node);
						node.Generating = true;
					}
					queuedNodes.Remove(node);
				}
			}

			foreach (var node in nodeGenerating.ToList())
			{
				if (!node.Generating)
				{
					nodeGenerating.Remove(node);
				}
			}

			if (queuedNodes.Count <= 0 && !queuedChunksAlreadyDone)
			{
				queuedChunksAlreadyDone = true;
				OnQueuedChunksDone?.Invoke();
			}
		}

		private void ChecKGetCirclePointJobsQueue()
		{
			if (queuedNodesCirclePoints.Count <= 0) return;

			List<Node> circleNodeToRemove = new();
			foreach (var (node, chunkEditJobData) in queuedNodesCirclePoints)
			{
				if (!chunkEditJobData.GetCirclePointsJobHandle.IsCompleted) continue;

				chunkEditJobData.GetCirclePointsJobHandle.Complete();

				var neighbourChunks = GetNeighbourChunks(node.Position);
				var isNeighbourChunkAlreadyQueued = neighbourChunks.Any(neighbourNode =>
				{
					return neighbourNode != node && editingNodes.Contains(neighbourNode);
				});

				if (isNeighbourChunkAlreadyQueued)
				{
					circleNodeToRemove.Add(node);
					chunkEditJobData.GetCirclePointsJob.points.Dispose();
					Debug.LogError($"Neighbour Chunk at {node.Position} is already being processed");
					continue;
				}

				if (neighbourChunks.Any(chunk => chunk.Chunk.LocalTerrainMap == null))
				{
					circleNodeToRemove.Add(node);
					chunkEditJobData.GetCirclePointsJob.points.Dispose();
					Debug.LogError($"Neighbour chunk at {node.Position} terrain map is null");
					continue;
				}

				var editedNodePointValues = chunkEditJobData.GetCirclePointsJob.points;
				this.editedNodePointValues.Clear();
				this.editedNodePointValues.AddRange(editedNodePointValues);
				editedNodePointValuePosition = node.Position;

				foreach (var neighbourNode in neighbourChunks)
				{
					var diferenceInPosition = node.Position - neighbourNode.Position;
					neighbourNode.Chunk.CreateAndQueueEditTerrainMapJob(diferenceInPosition, editedNodePointValues, chunkEditJobData.Add);
					editingNodes.Add(neighbourNode);
				}

				circleNodeToRemove.Add(node);
				chunkEditJobData.GetCirclePointsJob.points.Dispose();
			}

			foreach (var node in circleNodeToRemove)
			{
				queuedNodesCirclePoints.Remove(node);
			}
		}

		public void CheckEditQueues()
		{
			List<Node> nodesToRemove = new();
			foreach (var editingNode in editingNodes)
			{
				if (editingNode.Chunk.CheckJobComplete())
				{
					nodesToRemove.Add(editingNode);
				}
			}

			foreach (var node in nodesToRemove)
			{
				editingNodes.Remove(node);
			}
		}

		public void CheckGenerateQueues()
		{
			List<Node> nodesToRemove = new();
			foreach (var generateNode in generatingNodes)
			{
				if (generateNode.Chunk.CheckJobComplete())
				{
					nodesToRemove.Add(generateNode);
				}
			}

			foreach (var node in nodesToRemove)
			{
				generatingNodes.Remove(node);
			}
		}
	}
}
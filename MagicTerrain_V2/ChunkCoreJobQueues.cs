using MagicTerrain_V2.Helpers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicTerrain_V2
{
	public partial class ChunkCore
	{
		internal const float TERRAIN_SURFACE = 0.4f;
		
		private HashSet<Node> generatingNodes = new();
		private HashSet<Node> editingNodes = new();
		private readonly Dictionary<Node, ChunkEditJobData> queuedNodesCirclePoints = new();

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
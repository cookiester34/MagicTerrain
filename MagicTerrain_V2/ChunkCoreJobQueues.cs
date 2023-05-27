using MagicTerrain_V2.Helpers;
using MagicTerrain_V2.Jobs;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MagicTerrain_V2
{
	public partial class ChunkCore
	{
		private readonly List<Node> queuedNodes = new();
		private readonly Dictionary<Node, ChunkTerrainMapJobData> queuedNodesCheckTerrainMapCompletion = new();
		private readonly Dictionary<Node, ChunkMarchChunkJobData> queuedNodesCheckChunkJobCompletion = new();
		private readonly Dictionary<Node, ChunkEditJobData> queuedNodesCirclePoints = new();
		internal readonly Dictionary<Node, EditTerrainMapJobData> queuedNodesTerrainMapEdit = new();

		private void ChecKGetCirclePointJobsQueue()
		{
			if (queuedNodesCirclePoints.Count <= 0) return;

			List<Node> circleNodeToRemove = new();
			foreach (var (node, chunkEditJobData) in queuedNodesCirclePoints)
			{
				if (!chunkEditJobData.GetCirclePointsJobHandle.IsCompleted) continue;

				chunkEditJobData.GetCirclePointsJobHandle.Complete();

				var neighbourChunks = GetNeighbourChunks(node.Position);
				var isNeighbourChunkAlreadyQueued = false;
				foreach (var neighbourChunk in neighbourChunks)
				{
					if (queuedNodesTerrainMapEdit.ContainsKey(neighbourChunk))
					{
						isNeighbourChunkAlreadyQueued = true;
						break;
					}
				}

				if (isNeighbourChunkAlreadyQueued)
				{
					circleNodeToRemove.Add(node);
					chunkEditJobData.GetCirclePointsJob.points.Dispose();
					node.IsProccessing = false;
					continue;
				}

				if (neighbourChunks.Any(chunk => chunk.Chunk.LocalTerrainMap == null))
				{
					circleNodeToRemove.Add(node);
					chunkEditJobData.GetCirclePointsJob.points.Dispose();
					node.IsProccessing = false;
					continue;
				}

				var editedNodePointValues = chunkEditJobData.GetCirclePointsJob.points;
				this.editedNodePointValues.Clear();
				this.editedNodePointValues.AddRange(editedNodePointValues);
				editedNodePointValuePosition = node.Position;

				foreach (var neighbourNode in neighbourChunks)
				{
					var diferenceInPosition = node.Position - neighbourNode.Position;

					var arrayLength = editedNodePointValues.Length;
					var terrainMapEditJob = new EditTerrainMapJob()
					{
						diferenceInPosition = diferenceInPosition,
						points = new NativeArray<EditedNodePointValue>(editedNodePointValues, Allocator.Persistent),
						add = chunkEditJobData.Add,
						chunkSize = chunkSize + 1,
						terrainMap = new NativeArray<float>(neighbourNode.Chunk.LocalTerrainMap, Allocator.Persistent),
						wasEdited = new NativeArray<bool>(1, Allocator.Persistent),
						editedTerrainMapValues = new NativeArray<float>(arrayLength, Allocator.Persistent),
						editedTerrainMapIndices = new NativeArray<int>(arrayLength, Allocator.Persistent),
						arrayCount = new NativeArray<int>(1, Allocator.Persistent)
					};
					var jobHandler = terrainMapEditJob.Schedule(arrayLength, 60);
					JobHandle.ScheduleBatchedJobs();

					queuedNodesTerrainMapEdit.Add(neighbourNode,
						new EditTerrainMapJobData(jobHandler, terrainMapEditJob));
					neighbourNode.IsProccessing = true;
				}

				circleNodeToRemove.Add(node);
				chunkEditJobData.GetCirclePointsJob.points.Dispose();
			}

			foreach (var node in circleNodeToRemove)
			{
				queuedNodesCirclePoints.Remove(node);
			}
		}

		private void CheckEditTerrainMapJobsQueue()
		{
			if (queuedNodesTerrainMapEdit.Count <= 0) return;

			List<Node> terrainMapNodeToRemove = new();
			foreach (var (node, editTerrainMapJobData) in queuedNodesTerrainMapEdit)
			{
				if (!editTerrainMapJobData.EditTerrainMapJobHandle.IsCompleted) continue;

				editTerrainMapJobData.EditTerrainMapJobHandle.Complete();

				var editTerrainMapJob = editTerrainMapJobData.EditTerrainMapJob;
				var wasEdited = editTerrainMapJob.wasEdited[0];
				if (wasEdited)
				{
					List<Vector3Int> editPositions = new();

					foreach (var point in editTerrainMapJob.points)
					{
						editPositions.Add(point.PointPosition);
					}

					node.Chunk.LocalTerrainMap = editTerrainMapJob.terrainMap.ToArray();

					var meshDataJob = new MeshDataJob
					{
						chunkSize = chunkSize + 1,
						terrainMap = new NativeArray<float>(node.Chunk.LocalTerrainMap,
							Allocator.Persistent),
						terrainSurface = 0.7f,
						vertices = new NativeArray<Vector3>(900000, Allocator.Persistent),
						triangles = new NativeArray<int>(900000, Allocator.Persistent),
						cube = new NativeArray<float>(8, Allocator.Persistent),
						smoothTerrain = smoothTerrain,
						flatShaded = !smoothTerrain || flatShaded,
						triCount = new NativeArray<int>(1, Allocator.Persistent),
						vertCount = new NativeArray<int>(1, Allocator.Persistent)
					};
					var meshDataJobHandle = meshDataJob.Schedule();

					JobHandle.ScheduleBatchedJobs();
					queuedNodesCheckChunkJobCompletion.Add(node,
						new ChunkMarchChunkJobData(meshDataJobHandle, meshDataJob));
				}
				else
				{
					node.IsProccessing = false;
				}

				editTerrainMapJob.wasEdited.Dispose();
				editTerrainMapJob.terrainMap.Dispose();
				editTerrainMapJob.editedTerrainMapIndices.Dispose();
				editTerrainMapJob.editedTerrainMapValues.Dispose();
				editTerrainMapJob.arrayCount.Dispose();

				terrainMapNodeToRemove.Add(node);
			}

			foreach (var node in terrainMapNodeToRemove)
			{
				queuedNodesTerrainMapEdit.Remove(node);
			}
		}

		private void SchedualTerrainMapJobsQueue()
		{
			var playerPosition = playerTransform.position;
			if (queueDequeueLimit <= queuedNodesCheckTerrainMapCompletion.Count || queuedNodes.Count <= 0) return;

			var orderedEnumerable = queuedNodes.OrderBy(node =>
				Vector3.Distance(node.PositionReal, playerPosition));

			foreach (var node in orderedEnumerable)
			{
				if (queueDequeueLimit <= queuedNodesCheckTerrainMapCompletion.Count) break;

				if (node.ChunkContainer != null && node.ChunkContainer.Chunk != null)
				{
					node.IsQueued = true;
				}
				else
				{
					continue;
				}

				if (node.IsLoaded)
				{
					var terrainMapJob = new TerrainMapJob
					{
						chunkSize = chunkSize + 1,
						chunkPosition = node.Position,
						planetCenter = Vector3.zero,
						planetSize = TrueWorldSize,
						octaves = octaves,
						weightedStrength = weightedStrength,
						lacunarity = lacunarity,
						gain = gain,
						octavesCaves = octavesCaves,
						weightedStrengthCaves = weightedStrengthCaves,
						lacunarityCaves = lacunarityCaves,
						gainCaves = gainCaves,
						domainWarpAmp = domainWarpAmp,
						terrainMap = new NativeArray<float>(terrainMapSize, Allocator.Persistent),
						seed = seed
					};
					var terrainMapJobHandle = terrainMapJob.Schedule(chunkSize + 1, 200);
					JobHandle.ScheduleBatchedJobs();

					//TODO: Create a better way to handle this
					queuedNodesCheckTerrainMapCompletion.TryAdd(node,
						new ChunkTerrainMapJobData(terrainMapJobHandle, terrainMapJob));
				}

				//when chunk is done remove from queue
				queuedNodes.Remove(node);
			}
		}

		private void SchedualMeshCreationJobsQueue()
		{
			if (queuedNodesCheckTerrainMapCompletion.Count <= 0) return;

			List<Node> terrainMapNodeToRemove = new();
			foreach (var (node, creationQueueData) in queuedNodesCheckTerrainMapCompletion)
			{
				if (!creationQueueData.TerrainMapJobHandle.IsCompleted) continue;

				creationQueueData.TerrainMapJobHandle.Complete();

				node.Chunk.LocalTerrainMap = creationQueueData.TerrainMapJob.terrainMap.ToArray();
				node.Chunk.UnEditedLocalTerrainMap = node.Chunk.LocalTerrainMap.ToArray();
				
				if (node.Chunk.IsDirty)
				{
					node.Chunk.ApplyChunkEdits();
				}

				var meshDataJob = new MeshDataJob
				{
					chunkSize = chunkSize + 1,
					terrainMap = new NativeArray<float>(node.Chunk.LocalTerrainMap, Allocator.Persistent),
					terrainSurface = 0.7f,
					vertices = new NativeArray<Vector3>(900000, Allocator.Persistent),
					triangles = new NativeArray<int>(900000, Allocator.Persistent),
					cube = new NativeArray<float>(8, Allocator.Persistent),
					smoothTerrain = smoothTerrain,
					flatShaded = !smoothTerrain || flatShaded,
					triCount = new NativeArray<int>(1, Allocator.Persistent),
					vertCount = new NativeArray<int>(1, Allocator.Persistent)
				};
				var meshDataJobHandle = meshDataJob.Schedule();

				JobHandle.ScheduleBatchedJobs();
				queuedNodesCheckChunkJobCompletion.Add(node, new ChunkMarchChunkJobData(meshDataJobHandle, meshDataJob));
				creationQueueData.TerrainMapJob.terrainMap.Dispose();
				terrainMapNodeToRemove.Add(node);
			}

			foreach (var node in terrainMapNodeToRemove)
			{
				queuedNodesCheckTerrainMapCompletion.Remove(node);
			}
		}

		private void CheckMeshCreationJobQueue()
		{
			if (queuedNodesCheckChunkJobCompletion.Count <= 0) return;

			List<Node> chunkCreationNodeToRemove = new();
			foreach (var (node, creationQueueData) in queuedNodesCheckChunkJobCompletion)
			{
				if (!creationQueueData.MeshDataJobHandle.IsCompleted) continue;

				creationQueueData.MeshDataJobHandle.Complete();

				var chunkContainerChunk = node.ChunkContainer.Chunk;
				var tCount = creationQueueData.MeshDataJob.triCount[0];
				chunkContainerChunk.ChunkTriangles = new int[tCount];

				var meshDataJob = creationQueueData.MeshDataJob;

				for (var i = 0; i < tCount; i++)
				{
					chunkContainerChunk.ChunkTriangles[i] = meshDataJob.triangles[i];
				}

				var vCount = creationQueueData.MeshDataJob.vertCount[0];
				chunkContainerChunk.ChunkVertices = new Vector3[vCount];
				for (var i = 0; i < vCount; i++)
				{
					chunkContainerChunk.ChunkVertices[i] = meshDataJob.vertices[i];
				}

				chunkContainerChunk.BuildMesh();

				creationQueueData.MeshDataJob.vertices.Dispose();
				creationQueueData.MeshDataJob.triangles.Dispose();
				creationQueueData.MeshDataJob.cube.Dispose();
				creationQueueData.MeshDataJob.triCount.Dispose();
				creationQueueData.MeshDataJob.vertCount.Dispose();
				creationQueueData.MeshDataJob.terrainMap.Dispose();

				node.ChunkContainer.CreateChunkMesh(coreMaterial);
				node.IsQueued = false;
				node.IsProccessing = false;

				if (node.IsDisabled)
				{
					node.ReturnChunk();
				}

				chunkCreationNodeToRemove.Add(node);
			}

			foreach (var node in chunkCreationNodeToRemove)
			{
				queuedNodesCheckChunkJobCompletion.Remove(node);
			}
		}
	}
}
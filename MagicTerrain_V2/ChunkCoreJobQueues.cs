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
		private const float TERRAIN_SURFACE = 0.4f;
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
						points = new NativeArray<EditedNodePointValue>(editedNodePointValues, Allocator.TempJob),
						add = chunkEditJobData.Add,
						chunkSize = chunkSize + 1,
						terrainMap = new NativeArray<float>(neighbourNode.Chunk.LocalTerrainMap, Allocator.TempJob),
						wasEdited = new NativeArray<bool>(1, Allocator.TempJob)
					};
					var jobHandler = terrainMapEditJob.Schedule(arrayLength, 244);
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
							Allocator.TempJob),
						terrainSurface = TERRAIN_SURFACE,
						cube = new NativeArray<float>(8, Allocator.TempJob),
						smoothTerrain = smoothTerrain,
						flatShaded = !smoothTerrain || flatShaded,
						vertices = new NativeArray<Vector3>(900000, Allocator.TempJob),
						triangles = new NativeArray<int>(900000, Allocator.TempJob),
						triCount = new NativeArray<int>(7, Allocator.TempJob),
						vertCount = new NativeArray<int>(7, Allocator.TempJob),
						vertices1 = new NativeArray<Vector3>(900000, Allocator.TempJob),
						triangles1 = new NativeArray<int>(900000, Allocator.TempJob),
						vertices2 = new NativeArray<Vector3>(900000, Allocator.TempJob),
						triangles2 = new NativeArray<int>(900000, Allocator.TempJob),
						vertices3 = new NativeArray<Vector3>(900000, Allocator.TempJob),
						triangles3 = new NativeArray<int>(900000, Allocator.TempJob),
						vertices4 = new NativeArray<Vector3>(900000, Allocator.TempJob),
						triangles4 = new NativeArray<int>(900000, Allocator.TempJob)
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
				editTerrainMapJob.points.Dispose();
				editTerrainMapJob.terrainMap.Dispose();

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
						terrainMap = new NativeArray<float>(terrainMapSize, Allocator.TempJob),
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
				node.Chunk.UnEditedLocalTerrainMap ??= node.Chunk.LocalTerrainMap.ToArray();
				
				if (node.Chunk.IsDirty)
				{
					node.Chunk.ApplyChunkEdits();
				}

				var meshDataJob = new MeshDataJob
				{
					chunkSize = chunkSize + 1,
					terrainMap = new NativeArray<float>(node.Chunk.LocalTerrainMap, Allocator.TempJob),
					terrainSurface = TERRAIN_SURFACE,
					cube = new NativeArray<float>(8, Allocator.TempJob),
					smoothTerrain = smoothTerrain,
					flatShaded = !smoothTerrain || flatShaded,
					vertices = new NativeArray<Vector3>(900000, Allocator.TempJob),
					triangles = new NativeArray<int>(900000, Allocator.TempJob),
					triCount = new NativeArray<int>(7, Allocator.TempJob),
					vertCount = new NativeArray<int>(7, Allocator.TempJob),
					vertices1 = new NativeArray<Vector3>(900000, Allocator.TempJob),
					triangles1 = new NativeArray<int>(900000, Allocator.TempJob),
					vertices2 = new NativeArray<Vector3>(900000, Allocator.TempJob),
					triangles2 = new NativeArray<int>(900000, Allocator.TempJob),
					vertices3 = new NativeArray<Vector3>(900000, Allocator.TempJob),
					triangles3 = new NativeArray<int>(900000, Allocator.TempJob),
					vertices4 = new NativeArray<Vector3>(900000, Allocator.TempJob),
					triangles4 = new NativeArray<int>(900000, Allocator.TempJob)
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

				//TODO: investigate Null ref here
				var chunk = node.ChunkContainer.Chunk;
				var meshDataJob = creationQueueData.MeshDataJob;

				var triCount = creationQueueData.MeshDataJob.triCount.ToArray();
				var vertCount = creationQueueData.MeshDataJob.vertCount.ToArray();

				chunk.MeshDataSets ??= new Chunk.MeshData[7];
				//LOD0
				var tCount = triCount[0];
				chunk.MeshDataSets[0].chunkTriangles = new int[tCount];
				for (var i = 0; i < tCount; i++)
				{
					chunk.MeshDataSets[0].chunkTriangles[i] = meshDataJob.triangles[i];
				}
				var vCount = vertCount[0];
				chunk.MeshDataSets[0].chunkVertices = new Vector3[vCount];
				for (var i = 0; i < vCount; i++)
				{
					chunk.MeshDataSets[0].chunkVertices[i] = meshDataJob.vertices[i];
				}
				
				//LOD1
				var tCount1 = triCount[1];
				chunk.MeshDataSets[1].chunkTriangles = new int[tCount1];
				for (var i = 0; i < tCount1; i++)
				{
					chunk.MeshDataSets[1].chunkTriangles[i] = meshDataJob.triangles1[i];
				}
				var vCount1 = vertCount[1];
				chunk.MeshDataSets[1].chunkVertices = new Vector3[vCount1];
				for (var i = 0; i < vCount1; i++)
				{
					chunk.MeshDataSets[1].chunkVertices[i] = meshDataJob.vertices1[i];
				}
				
				//LOD2
				var tCount2 = triCount[2];
				chunk.MeshDataSets[2].chunkTriangles = new int[tCount2];
				for (var i = 0; i < tCount2; i++)
				{
					chunk.MeshDataSets[2].chunkTriangles[i] = meshDataJob.triangles2[i];
				}
				var vCount2 = vertCount[2];
				chunk.MeshDataSets[2].chunkVertices = new Vector3[vCount2];
				for (var i = 0; i < vCount2; i++)
				{
					chunk.MeshDataSets[2].chunkVertices[i] = meshDataJob.vertices2[i];
				}
				
				//LOD3
				var tCount3 = triCount[3];
				chunk.MeshDataSets[3].chunkTriangles = new int[tCount3];
				for (var i = 0; i < tCount3; i++)
				{
					chunk.MeshDataSets[3].chunkTriangles[i] = meshDataJob.triangles3[i];
				}
				var vCount3 = vertCount[3];
				chunk.MeshDataSets[3].chunkVertices = new Vector3[vCount3];
				for (var i = 0; i < vCount3; i++)
				{
					chunk.MeshDataSets[3].chunkVertices[i] = meshDataJob.vertices3[i];
				}
				
				//LOD4
				var tCount4 = triCount[4];
				chunk.MeshDataSets[4].chunkTriangles = new int[tCount4];
				for (var i = 0; i < tCount4; i++)
				{
					chunk.MeshDataSets[4].chunkTriangles[i] = meshDataJob.triangles4[i];
				}
				var vCount4 = vertCount[4];
				chunk.MeshDataSets[4].chunkVertices = new Vector3[vCount4];
				for (var i = 0; i < vCount4; i++)
				{
					chunk.MeshDataSets[4].chunkVertices[i] = meshDataJob.vertices4[i];
				}
				
				//LOD5
				// var tCount5 = triCount[5];
				// chunk.MeshDataSets[5].chunkTriangles = new int[tCount5];
				// for (var i = 0; i < tCount5; i++)
				// {
				// 	chunk.MeshDataSets[5].chunkTriangles[i] = meshDataJob.triangles5[i];
				// }
				// var vCount5 = vertCount[5];
				// chunk.MeshDataSets[5].chunkVertices = new Vector3[vCount5];
				// for (var i = 0; i < vCount5; i++)
				// {
				// 	chunk.MeshDataSets[5].chunkVertices[i] = meshDataJob.vertices5[i];
				// }
				
				//LOD6
				// var tCount6 = triCount[6];
				// chunk.MeshDataSets[6].chunkTriangles = new int[tCount6];
				// for (var i = 0; i < tCount6; i++)
				// {
				// 	chunk.MeshDataSets[6].chunkTriangles[i] = meshDataJob.triangles6[i];
				// }
				// var vCount6 = vertCount[6];
				// chunk.MeshDataSets[6].chunkVertices = new Vector3[vCount6];
				// for (var i = 0; i < vCount6; i++)
				// {
				// 	chunk.MeshDataSets[6].chunkVertices[i] = meshDataJob.vertices6[i];
				// }

				chunk.BuildMesh();
				
				creationQueueData.MeshDataJob.cube.Dispose();
				creationQueueData.MeshDataJob.triCount.Dispose();
				creationQueueData.MeshDataJob.vertCount.Dispose();
				creationQueueData.MeshDataJob.vertices.Dispose();
				creationQueueData.MeshDataJob.triangles.Dispose();
				creationQueueData.MeshDataJob.vertices1.Dispose();
				creationQueueData.MeshDataJob.triangles1.Dispose();
				creationQueueData.MeshDataJob.vertices2.Dispose();
				creationQueueData.MeshDataJob.triangles2.Dispose();
				creationQueueData.MeshDataJob.vertices3.Dispose();
				creationQueueData.MeshDataJob.triangles3.Dispose();
				creationQueueData.MeshDataJob.vertices4.Dispose();
				creationQueueData.MeshDataJob.triangles4.Dispose();
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
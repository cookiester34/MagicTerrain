using Scripts;
using Scripts.Chunks.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using System.Threading.Tasks;
using UnityEngine;

public class Chunk
{
	public Mesh[] Meshes { get; set; }
	public int ChunkSize { get; }
	public ChunkContainer CurrentChunkContainer { get; set; }
	private Vector3Int ChunkPosition { get; }

	private readonly float terrainSurface = 0.5f;
	private readonly int seed;
	private readonly bool smooth;
	private readonly bool flatShaded;
	private readonly PlanetController planetController;
	private readonly int octaves;
	private readonly float weightedStrength;
	private readonly float lacunarity;
	private readonly float gain;
	private readonly int octavesCaves;
	private readonly float weightedStrengthCaves;
	private readonly float lacunarityCaves;
	private readonly float gainCaves;
	private readonly float domainWarpAmp;
	private readonly int scale;

	private ChunkManager chunkManager;
	private PopulateTerrainMapJob populateTerrainJob;
	private CreateMeshDataJob createMeshJob;
	private float[] localTerrainMap;

	private NativeArray<float> terrainMap;
	private NativeArray<Vector3> vertices;
	private NativeArray<int> triangles;
	private NativeArray<float> cube;
	private NativeArray<int> triangleCount;
	private NativeArray<int> vertCount;

	//chunk queued info
	public bool IsProccessing { get; set; }

	//Flat[x + HEIGHT* (y + WIDTH* z)] = Original[x, y, z], assuming Original[HEIGHT,WIDTH,DEPTH]

	public Chunk(ChunkData chunkData, bool smooth, bool flatShaded = true)
	{
		ChunkSize = chunkData.chunkSize;
		ChunkSize++;
		seed = chunkData.seed;
		ChunkPosition = chunkData.chunkPosition / chunkData.scale;
		this.smooth = smooth;
		this.flatShaded = flatShaded;
		planetController = chunkData.planetController;
		octaves = chunkData.octaves;
		weightedStrength = chunkData.weightedStrength;
		lacunarity = chunkData.lacunarity;
		gain = chunkData.gain;
		domainWarpAmp = chunkData.domainWarpAmp;
		octavesCaves = chunkData.octavesCaves;
		weightedStrengthCaves = chunkData.weightedStrengthCaves;
		lacunarityCaves = chunkData.lacunarityCaves;
		gainCaves = chunkData.gainCaves;
		scale = chunkData.scale;
		chunkManager = chunkData.chunkManager;
	}

	public async void ScheduleChunkJobs(Action chunkDone, bool generateCollider, bool calculateTerrainMap = true)
	{
		IsProccessing = true;
		if (calculateTerrainMap)
		{
			var terrainMapHandle = CreateTerrainMap().Schedule(ChunkSize, 200);
			JobHandle.ScheduleBatchedJobs();

			while (!terrainMapHandle.IsCompleted)
			{
				await Task.Yield();
			}

			terrainMapHandle.Complete();
		}

		var marchChunkHandle = MarchChunk().Schedule();
		JobHandle.ScheduleBatchedJobs();

		while (!marchChunkHandle.IsCompleted)
		{
			await Task.Yield();
		}

		marchChunkHandle.Complete();
		CreateChunk();
		CurrentChunkContainer.UpdateChunkMesh(generateCollider);

		chunkDone?.Invoke();

		IsProccessing = false;
	}

	public PopulateTerrainMapJob CreateTerrainMap()
	{
		terrainMap = new NativeArray<float>(ChunkSize + ChunkSize * (ChunkSize + ChunkSize * ChunkSize),
			Allocator.Persistent);
		populateTerrainJob = new PopulateTerrainMapJob
		{
			chunkPosition = ChunkPosition,
			chunkSize = ChunkSize,
			terrainMap = terrainMap,
			seed = seed,
			octaves = octaves,
			weightedStrength = weightedStrength,
			lacunarity = lacunarity,
			gain = gain,
			domainWarpAmp = domainWarpAmp,
			octavesCaves = octavesCaves,
			weightedStrengthCaves = weightedStrengthCaves,
			lacunarityCaves = lacunarityCaves,
			gainCaves = gainCaves,
			isPlanet = planetController != null
		};
		if (planetController == null) return populateTerrainJob;
		populateTerrainJob.planetCenter = planetController.planetCenter;
		populateTerrainJob.planetSize = planetController.planetSize;
		return populateTerrainJob;
	}

	public CreateMeshDataJob MarchChunk()
	{
		vertices = new NativeArray<Vector3>(900000, Allocator.Persistent);
		triangles = new NativeArray<int>(900000, Allocator.Persistent);
		cube = new NativeArray<float>(8, Allocator.Persistent);
		triangleCount = new NativeArray<int>(1, Allocator.Persistent);
		vertCount = new NativeArray<int>(1, Allocator.Persistent);
		createMeshJob = new CreateMeshDataJob
		{
			chunkSize = ChunkSize,
			terrainMap = terrainMap,
			terrainSurface = terrainSurface,
			vertices = vertices,
			triangles = triangles,
			cube = cube,
			smoothTerrain = smooth,
			flatShaded = !smooth || flatShaded,
			triCount = triangleCount,
			vertCount = vertCount
		};
		return createMeshJob;
	}

	public void CreateChunk()
	{
		var tCount = triangleCount[0];
		var newTriangles = new int[tCount];
		for (var i = 0; i < tCount; i++)
		{
			newTriangles[i] = triangles[i];
		}

		var vCount = vertCount[0];
		var newVertices = new Vector3[vCount];
		for (var i = 0; i < vCount; i++)
		{
			newVertices[i] = vertices[i];
		}

		Meshes = new[]
		{
			new Mesh
			{
				vertices = newVertices,
				triangles = newTriangles
			}
		};
		foreach (var mesh in Meshes)
		{
			mesh.RecalculateNormals();
		}

		vertices.Dispose();
		triangles.Dispose();
		cube.Dispose();
		triangleCount.Dispose();
		vertCount.Dispose();

		localTerrainMap = terrainMap.ToArray();
		terrainMap.Dispose();
	}

	public void EditChunk(List<EditedChunkPointValue> points, bool add)
	{
		var neighbourChunks = chunkManager.GetNeighbourChunks(ChunkPosition * scale);

		if (neighbourChunks.Any(x => x.IsProccessing)) return;

		var chunkTranslation = Matrix4x4.Translate(ChunkPosition);
		var chunkScale = Matrix4x4.Scale(new Vector3(scale, scale, scale));
		var chunkTransformation = chunkScale * chunkTranslation;
		for (var index = 0; index < points.Count; index++)
		{
			var point = points[index];
			Vector3 multiplyPoint = chunkTransformation.inverse.MultiplyPoint(point.PointPosition);
			point.PointPosition = new Vector3Int((int) multiplyPoint.x, (int) multiplyPoint.y, (int) multiplyPoint.z);
			points[index] = point;
		}

		foreach (var neighbourChunk in neighbourChunks)
		{
			//Is this editing chunks it shouldn't be editing
			var diferenceInPosition = ChunkPosition - neighbourChunk.ChunkPosition;
			var wasEdited = false;
			foreach (var point in points)
			{
				var relativePosition = point.PointPosition + diferenceInPosition;

				if (relativePosition.x < 0 || relativePosition.y < 0 || relativePosition.z < 0
				    || relativePosition.x >= ChunkSize || relativePosition.y >= ChunkSize|| relativePosition.z >= ChunkSize) continue;

				var terrainMapIndex =
					relativePosition.x + ChunkSize * (relativePosition.y + ChunkSize * relativePosition.z);

				var isWithinBounds = terrainMapIndex >= 0 && terrainMapIndex < neighbourChunk.localTerrainMap.Length;

				if (!isWithinBounds) continue;

				if (add)
				{
					if (neighbourChunk.localTerrainMap[terrainMapIndex] <= point.PointValue)
						continue;
				}
				else
				{
					if (neighbourChunk.localTerrainMap[terrainMapIndex] >= point.PointValue)
						continue;
				}
				wasEdited = true;
				neighbourChunk.localTerrainMap[terrainMapIndex] = point.PointValue;
			}

			if (!wasEdited) continue;
			neighbourChunk.terrainMap = new NativeArray<float>(neighbourChunk.localTerrainMap, Allocator.Persistent);
			neighbourChunk.ScheduleChunkJobs(null, true, false);
		}
	}
}
using Scripts;
using Scripts.Chunks.Jobs;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class Chunk
{
	public Mesh[] Meshes { get => meshes; set => meshes = value; }
	public int ChunkSize { get; }
	public ChunkContainer CurrentChunkContainer { get; set; }
	private Vector3Int ChunkPosition => chunkPosition;

	private readonly float terrainSurface = 0.5f;
	private readonly int seed;
	private readonly bool smooth;
	private readonly bool flatShaded;
	private readonly float planetSize;
	private readonly Vector3 planetCenter;
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
	private JobHandle getCircleJobHandler;
	private float[] localTerrainMap;

	private NativeList<EditedChunkPointValue> points;
	private NativeArray<float> terrainMap;
	private NativeArray<Vector3> vertices;
	private NativeArray<int> triangles;
	private NativeArray<float> cube;
	private NativeArray<int> triangleCount;
	private NativeArray<int> vertCount;
	private Mesh[] meshes;

	[SerializeField]
	private Vector3Int chunkPosition;

	[SerializeField]
	private Vector3[] chunkVertices;

	[SerializeField]
	private int[] chunkTriangles;

	//chunk queued info
	public bool IsProccessing { get; set; }

	//Flat[x + HEIGHT* (y + WIDTH* z)] = Original[x, y, z], assuming Original[HEIGHT,WIDTH,DEPTH]

	public Chunk(ChunkData chunkData, bool smooth, bool flatShaded = true)
	{
		ChunkSize = chunkData.chunkSize;
		ChunkSize++;
		seed = chunkData.seed;
		chunkPosition = chunkData.chunkPosition / chunkData.scale;
		this.smooth = smooth;
		this.flatShaded = flatShaded;
		planetSize = chunkData.planetSize;
		planetCenter = chunkData.planetCenter;
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

	public async void ScheduleChunkJobs(Action chunkDone, bool generateCollider, bool calculateTerrainMap = true, bool CreateDataOnly = false)
	{
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

		if (!CreateDataOnly)
		{
			CurrentChunkContainer.UpdateChunkMesh(generateCollider);
		}

		chunkDone?.Invoke();

		IsProccessing = false;
	}

	private PopulateTerrainMapJob CreateTerrainMap()
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
			isPlanet = planetSize != 0
		};
		if (planetSize == 0) return populateTerrainJob;
		populateTerrainJob.planetCenter = planetCenter;
		populateTerrainJob.planetSize = planetSize;
		return populateTerrainJob;
	}

	private CreateMeshDataJob MarchChunk()
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
		chunkTriangles = new int[tCount];
		for (var i = 0; i < tCount; i++)
		{
			chunkTriangles[i] = triangles[i];
		}

		var vCount = vertCount[0];
		chunkVertices = new Vector3[vCount];
		for (var i = 0; i < vCount; i++)
		{
			chunkVertices[i] = vertices[i];
		}

		Meshes = new[]
		{
			new Mesh
			{
				vertices = chunkVertices,
				triangles = chunkTriangles
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

	public void EditChunk(Vector3 hitPoint, float radius, bool add)
	{
		var neighbourChunks = chunkManager.GetNeighbourChunks(ChunkPosition * scale);

		if (neighbourChunks.Any(x => x.IsProccessing)) return;

		IsProccessing = true;
		var arraySize = ((Mathf.CeilToInt(radius) * 2 + 1) * 3) - 2;
		points = new NativeList<EditedChunkPointValue>(arraySize, Allocator.Persistent);
		
		getCircleJobHandler = GetCirclePointJobs(hitPoint, radius, add).Schedule();
		JobHandle.ScheduleBatchedJobs();

		if (!chunkManager.ChunksToEdit.TryAdd(this, add))
		{
			Debug.LogError("Failed to add chunk to edit queue");
		}
	}

	public void CheckEditDone(bool add)
	{
		if (!getCircleJobHandler.IsCompleted) return;
		
		getCircleJobHandler.Complete();

		var pointsLength = points.Length;
		var editedChunkPointValuesArray = new EditedChunkPointValue[pointsLength];

		for (var i = 0; i < pointsLength; i++)
		{
			editedChunkPointValuesArray[i] = points[i];
		}

		var neighbourChunks = chunkManager.GetNeighbourChunks(ChunkPosition * scale);
		foreach (var neighbourChunk in neighbourChunks)
		{
			var diferenceInPosition = ChunkPosition - neighbourChunk.ChunkPosition;
			neighbourChunk.EditChunkJob(diferenceInPosition, editedChunkPointValuesArray, add);
		}

		points.Dispose();

		chunkManager.ChunksToEdit.Remove(this);
	}

	private async void EditChunkJob(Vector3Int diferenceInPosition, EditedChunkPointValue[] editedChunkPointValues, bool add)
	{
		var chunkPointValues = new NativeArray<EditedChunkPointValue>(editedChunkPointValues, Allocator.Persistent);
		terrainMap = new NativeArray<float>(localTerrainMap, Allocator.Persistent);
		var wasEdited = new NativeArray<bool>(1, Allocator.Persistent);
		var editTerrainMapJob = new EditTerrainMapJob()
		{
			diferenceInPosition = diferenceInPosition,
			points = chunkPointValues,
			add = add,
			chunkSize = ChunkSize,
			terrainMap = terrainMap,
			wasEdited = wasEdited
		};

		var editTerrainMapHandler = editTerrainMapJob.Schedule(chunkPointValues.Length, 60);

		while (!editTerrainMapHandler.IsCompleted)
		{
			await Task.Yield();
		}
		editTerrainMapHandler.Complete();

		chunkPointValues.Dispose();

		if (wasEdited[0])
		{
			ScheduleChunkJobs(null, true, false);
		}
		else
		{
			terrainMap.Dispose();
			IsProccessing = false;
		}
		wasEdited.Dispose();
	}

	private GetCirclePointsJob GetCirclePointJobs(Vector3 hitPoint, float radius, bool add)
	{
		var getCirclePointsJob = new GetCirclePointsJob()
		{
			hitPosition = hitPoint,
			add = add,
			radius = radius,
			points = points,
			chunkPosition = ChunkPosition,
			scale = scale
		};
		return getCirclePointsJob;
	}
}
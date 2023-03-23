using Scripts;
using Scripts.Chunks.Jobs;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class Chunk
{
	public bool HasMeshes => meshes != null && meshes.Length > 0;
	public Mesh[] Meshes { get => meshes; set => meshes = value; }
	public int ChunkSize { get; }
	public ChunkContainer CurrentChunkContainer { get; set; }
	private Vector3Int ChunkPositionReal => chunkPositionReal;

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
	private JobHandle editTerrainMapHandler;

	private NativeArray<EditedChunkPointValue> points;
	private NativeArray<float> terrainMap;
	private NativeArray<Vector3> vertices;
	private NativeArray<int> triangles;
	private NativeArray<float> cube;
	private NativeArray<int> triangleCount;
	private NativeArray<int> vertCount;
	private NativeArray<EditedChunkPointValue> chunkPointValues;
	private NativeArray<bool> wasEdited;

	private Mesh[] meshes;

	[SerializeField]
	private Vector3Int chunkPositionReal;

	[SerializeField]
	private Vector3Int chunkPositionRelative;

	[SerializeField]
	private float[] localTerrainMap;

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
		chunkPositionReal = chunkData.chunkPositionReal / chunkData.scale;
		chunkPositionRelative = chunkData.chunkPositionRelative;
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
			chunkPosition = ChunkPositionReal,
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

		CreateMesh();

		vertices.Dispose();
		triangles.Dispose();
		cube.Dispose();
		triangleCount.Dispose();
		vertCount.Dispose();

		localTerrainMap = terrainMap.ToArray();
		terrainMap.Dispose();
	}

	public void CreateMesh()
	{
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
	}

	public void EditChunk(Vector3 hitPoint, float radius, bool add)
	{
		var neighbourChunks = chunkManager.GetNeighbourChunks(chunkPositionRelative * scale);

		if (neighbourChunks.Any(x => x.IsProccessing)) return;

		IsProccessing = true;
		var ceilToInt = Mathf.CeilToInt(radius) * 2 + 1;
		var arraySize = ceilToInt * ceilToInt * ceilToInt;
		points = new NativeArray<EditedChunkPointValue>(arraySize, Allocator.Persistent);

		getCircleJobHandler = GetCirclePointJobs(hitPoint, chunkManager.transform.rotation, radius, add).Schedule();
		if (!chunkManager.ChunkPointsCalculating.TryAdd(this, add))
		{
			Debug.LogError("Failed to add chunk to edit queue, cancelling edit");
			points.Dispose();
			return;
		}

		JobHandle.ScheduleBatchedJobs();
	}

	public void CheckEditedPointsDone(bool add)
	{
		if (!getCircleJobHandler.IsCompleted) return;

		getCircleJobHandler.Complete();

		var neighbourChunks = chunkManager.GetNeighbourChunks(chunkPositionRelative * scale);
		foreach (var neighbourChunk in neighbourChunks)
		{
			var diferenceInPosition = ChunkPositionReal - neighbourChunk.ChunkPositionReal;
			neighbourChunk.EditChunkJob(diferenceInPosition, points, add);
		}

		points.Dispose();

		chunkManager.ChunkPointsCalculating.Remove(this);
	}

	private void EditChunkJob(Vector3Int diferenceInPosition, NativeArray<EditedChunkPointValue> editedChunkPointValues, bool add)
	{
		chunkPointValues = new NativeArray<EditedChunkPointValue>(editedChunkPointValues, Allocator.Persistent);
		terrainMap = new NativeArray<float>(localTerrainMap, Allocator.Persistent);
		wasEdited = new NativeArray<bool>(1, Allocator.Persistent);
		var editTerrainMapJob = new EditTerrainMapJob()
		{
			diferenceInPosition = diferenceInPosition,
			points = chunkPointValues,
			add = add,
			chunkSize = ChunkSize,
			terrainMap = terrainMap,
			wasEdited = wasEdited
		};

		editTerrainMapHandler = editTerrainMapJob.Schedule(chunkPointValues.Length, 60);
		chunkManager.ChunksEditing.Add(this);
	}

	public bool CheckIfEditDone()
	{
		if (!editTerrainMapHandler.IsCompleted) return false;

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

		chunkManager.ChunksEditing.Remove(this);
		return true;
	}

	private GetCirclePointsJob GetCirclePointJobs(Vector3 hitPoint, Quaternion transformRotation, float radius,
		bool add)
	{
		var getCirclePointsJob = new GetCirclePointsJob()
		{
			hitPosition = hitPoint,
			worldRotation = chunkManager.transform.rotation,
			chunkRotation = CurrentChunkContainer.transform.rotation,
			worldPositon = CurrentChunkContainer.transform.position,
			add = add,
			radius = radius,
			points = points,
			chunkPosition = chunkPositionReal,
			scale = scale
		};
		return getCirclePointsJob;
	}
}
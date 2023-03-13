using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
	[SerializeField]
	private int chunkPoolCount = 20;

	[SerializeField]
	private int chunkProccessingLimit = 5;
	public int ChunkProccessingLimit => chunkProccessingLimit;

	[SerializeField]
	private Material material;

	[SerializeField]
	private Transform player;

	[SerializeField]
	private Vector3 playerLastPositionRelative;

	[SerializeField]
	private float chunkUpdateDistance = 10f;

	[SerializeField]
	private int verticalViewDistance = 200;

	[SerializeField]
	private int horizontalViewDistance = 200;

	[SerializeField]
	private bool debugMode;

	[SerializeField]
	internal int octaves = 3;

	[SerializeField]
	internal float weightedStrength = 0f;

	[SerializeField]
	internal float lacunarity = 2f;

	[SerializeField]
	internal float gain = 0.5f;

	[SerializeField]
	public int octavesCaves = 4;

	[SerializeField]
	public float weightedStrengthCaves = 1f;

	[SerializeField]
	public float lacunarityCaves = 2f;

	[SerializeField]
	public float gainCaves = 1f;

	[SerializeField]
	internal float domainWarpAmp = 1.0f;

	[SerializeField]
	private int chunkSize = 20;
	public int ChunkSize => chunkSize;

	[SerializeField]
	private int chunkScale = 1;
	public int ChunkScale => chunkScale;

	[SerializeField]
	private int seed = 1337;
	public int Seed => seed;

	[SerializeField]
	private bool smoothTerrain;
	public bool SmoothTerrain => smoothTerrain;

	[SerializeField]
	private bool flatShaded;
	public bool FlatShaded => flatShaded;

	[SerializeField]
	private Camera camera;

	public Dictionary<Vector3Int, Chunk> Chunks => chunks;
	public Dictionary<Chunk, bool> ChunkPointsCalculating { get; } = new();
	public HashSet<Chunk> ChunksEditing { get; } = new();

	internal HashSet<Vector3Int> knownKeys = new();

	private PlanetController planetController;
	private Dictionary<Vector3Int, Chunk> chunks = new();
	private List<ChunkContainer> chunkContainers = new();
	private Dictionary<Vector3Int, ChunkContainer> activeContainers = new();
	private ChunkQueue chunkQueue;
	private bool forceUpdate = true;
	private int chunkIncrement;
	private Vector3 planetChunkManagerCenter;

	//Flat[x + HEIGHT* (y + WIDTH* z)] = Original[x, y, z], assuming Original[HEIGHT,WIDTH,DEPTH]

	private void OnDrawGizmos()
	{
		if (camera != null)
		{
			Gizmos.color = Color.magenta;
			Gizmos.matrix = camera.transform.localToWorldMatrix;
			Gizmos.DrawFrustum(transform.position, camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane,
				camera.aspect);
		}

		if (!debugMode) return;
		foreach (var chunkContainer in chunkContainers.Where(chunkContainer => chunkContainer.chunk != null))
		{
			Gizmos.color = chunkContainer.hasCollider ? Color.blue : chunkContainer.IsActive ? chunkContainer.markInactive ? Color.magenta : chunkContainer.chunkQueued ? Color.yellow : Color.green : Color.red;
			if (chunkContainer.chunk != null)
			{
				if (chunkContainer.chunk.IsProccessing)
				{
					Gizmos.color = Color.white;
				}
			}
			else if (chunkContainer.chunk == null)
			{
				Gizmos.color = Color.red;
			}
			var chunkChunkSize = chunkContainer.chunk.ChunkSize * chunkScale - 1;
			var chunkChunkSizeHalfed = chunkChunkSize / 2;
			var position = chunkContainer.chunkPositionReal +
			               new Vector3(chunkChunkSizeHalfed, chunkChunkSizeHalfed, chunkChunkSizeHalfed);
			Gizmos.DrawCube(position, new Vector3(5, 5, 5));
		}
	}

	private void Awake()
	{
		planetController = GetComponent<PlanetController>();
		chunkQueue = new ChunkQueue(ChunkProccessingLimit, this, planetController);
	}

	private void Start()
	{
		for (var index = 0; index < chunkPoolCount; index++)
		{
			var chunkContainer = new GameObject("Chunk").AddComponent<ChunkContainer>();
			chunkContainer.SetScale(chunkScale);
			chunkContainer.SetMaterial(material);
			chunkContainer.SetInactive();
			chunkContainer.transform.SetParent(transform);
			chunkContainers.Add(chunkContainer);
			chunkContainer.transform.position = Vector3.zero;
		}

		chunkIncrement = chunkSize * chunkScale;
		if (planetController != null)
		{
			planetController.planetCenterRaw = transform.position;
		}
		planetChunkManagerCenter = transform.position;
	}

	private void Update()
	{
		planetChunkManagerCenter = transform.position;

		if (planetController != null)
		{
			planetController.planetCenter = transform.position;
		}

		var chunksPointCalculating = ChunkPointsCalculating.Keys.ToArray();
		foreach (var chunk in chunksPointCalculating)
		{
			chunk.CheckEditedPointsDone(ChunkPointsCalculating[chunk]);
		}

		var chunksToEdit = ChunksEditing.ToArray();
		foreach (var chunk in chunksToEdit)
		{
			chunk.CheckIfEditDone();
		}

		chunkQueue.CheckIfValidQueueReady(player.position);

		if (camera != null)
		{
			//frustum culling
			var planes = GeometryUtility.CalculateFrustumPlanes(camera);
			foreach (var activeContainer in activeContainers)
			{
				activeContainer.Value.SetVisible(IsRendererVisible(planes, activeContainer.Value.MeshRenderer));
			}
		}
		else
		{
			foreach (var activeContainer in activeContainers)
			{
				activeContainer.Value.SetVisible(true);
			}
		}

		foreach (var chunkContainer in activeContainers)
		{
			chunkContainer.Value.SetPlanetCenter(transform.position);
		}

		var playerPositionRelative = transform.worldToLocalMatrix.MultiplyPoint(player.position);
		var currentDistance = Vector3.Distance(playerLastPositionRelative, playerPositionRelative);
		if (!(currentDistance >= chunkUpdateDistance) && !forceUpdate) return;
		playerLastPositionRelative = transform.worldToLocalMatrix.MultiplyPoint(player.position);

		forceUpdate = false;

		var playerPosition = new Vector3Int(
			Mathf.RoundToInt(playerLastPositionRelative.x).RoundOff(chunkIncrement),
			Mathf.RoundToInt(playerLastPositionRelative.y).RoundOff(chunkIncrement),
			Mathf.RoundToInt(playerLastPositionRelative.z).RoundOff(chunkIncrement));

		Dictionary<Vector3Int, ChunkContainer> visibleContainers = new();

		for (var x = playerPosition.x - horizontalViewDistance; x < playerPosition.x + horizontalViewDistance; x += chunkIncrement)
		{
			for (var y = playerPosition.y - verticalViewDistance; y < playerPosition.y + verticalViewDistance; y += chunkIncrement)
			{
				for (var z = playerPosition.z - horizontalViewDistance; z < playerPosition.z + horizontalViewDistance; z += chunkIncrement)
				{
					var position = Vector3Int.RoundToInt(new Vector3(x, y, z));
					if (planetController != null)
					{
						var distanceFromCenter = Vector3.Distance(position, Vector3.zero);
						var isWithinPlanet = distanceFromCenter <= planetController.planetSize * (2 * chunkScale);
						if (!isWithinPlanet)
						{
							continue;
						}
					}

					var localSpaceChunkPosition = position;
					var rotatedPosition = Matrix4x4.Rotate(transform.rotation).MultiplyPoint(position);
					var worldSpaceChunkPosition = new Vector3(rotatedPosition.x, rotatedPosition.y, rotatedPosition.z) + transform.position;
					var worldSpaceChunkPositionInt = Vector3Int.RoundToInt(worldSpaceChunkPosition);
					var chunkContainer = knownKeys.Contains(localSpaceChunkPosition)
						? LoadOrCreateChunk(localSpaceChunkPosition, worldSpaceChunkPositionInt, worldSpaceChunkPosition)
						: CreateChunk(localSpaceChunkPosition, worldSpaceChunkPositionInt, worldSpaceChunkPosition);
					chunkContainer.transform.rotation = transform.rotation;
					visibleContainers.Add(localSpaceChunkPosition, chunkContainer);
				}
			}
		}

		foreach (var activeContainer in activeContainers)
		{
			if (visibleContainers.ContainsKey(activeContainer.Key)) continue;
			if (chunkQueue.DoesQueueContain(activeContainer.Value))
			{
				activeContainer.Value.MarkInactive();
			}
			else
			{
				if (activeContainer.Value == null) continue;

				activeContainer.Value.SetInactive();
			}
		}

		activeContainers = visibleContainers;
	}

	private bool IsRendererVisible(Plane[] planes, Renderer renderer)
	{
		// Check if the renderer is within the view frustum of the camera
		var visible = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
		return visible;
	}

	private ChunkContainer LoadOrCreateChunk(Vector3Int currentChunkPosition, Vector3Int realPosition, Vector3 worldSpaceChunkPosition)
	{
		if (activeContainers.TryGetValue(currentChunkPosition, out var chunkContainer))
		{
			chunkContainer.UpdateChunkPosition(worldSpaceChunkPosition);
			CheckChunkDistance(chunkContainer);
			return chunkContainer;
		}

		if (chunks.TryGetValue(currentChunkPosition, out var chunk))
		{
			var container = GetOrCreateInactiveContainer();

			container.chunk = chunk;
			chunk.CurrentChunkContainer = container;
			container.SetActive();
			container.SetChunkPosition(currentChunkPosition, realPosition, worldSpaceChunkPosition);
			container.SetChunkIndex();
			CheckChunkDistance(container);
			return container;
		}

		return CreateChunk(currentChunkPosition, realPosition, worldSpaceChunkPosition);
	}

	private ChunkContainer CreateChunk(Vector3Int currentChunkPosition, Vector3Int realPosition, Vector3 worldSpaceChunkPosition)
	{
		knownKeys.Add(currentChunkPosition);

		var chunkContainer = GetOrCreateInactiveContainer();

		chunkContainer.SetActive();
		chunkContainer.SetChunkPosition(currentChunkPosition, realPosition, worldSpaceChunkPosition);

		chunkContainer.chunkQueued = true;
		chunkQueue.AddChunkToQueue(chunkContainer);
		return chunkContainer;
	}

	private void CheckChunkDistance(ChunkContainer chunkContainer)
	{
		var distance = Vector3.Distance(chunkContainer.chunkPositionReal, player.position);
		if (distance < chunkIncrement * 2)
		{
			chunkContainer.CreateCollider();
		}
	}

	private ChunkContainer GetOrCreateInactiveContainer()
	{
		foreach (var chunkContainer in chunkContainers.Where(chunkContainer => !chunkContainer.IsActive))
		{
			return chunkContainer;
		}

		var newChunkContainer = new GameObject("Chunk").AddComponent<ChunkContainer>();
		newChunkContainer.SetScale(chunkScale);
		newChunkContainer.SetMaterial(material);
		newChunkContainer.transform.SetParent(transform);
		chunkContainers.Add(newChunkContainer);
		return newChunkContainer;
	}

	public List<Chunk> GetNeighbourChunks(Vector3Int chunkPosition)
	{
		List<Chunk> foundChunks = new();
		for (var x = chunkPosition.x - chunkIncrement; x <= chunkPosition.x + chunkIncrement; x+= chunkIncrement)
		{
			for (var y = chunkPosition.y - chunkIncrement; y <= chunkPosition.y + chunkIncrement; y+= chunkIncrement)
			{
				for (var z = chunkPosition.z - chunkIncrement; z <= chunkPosition.z + chunkIncrement; z+= chunkIncrement)
				{
					if (chunks.TryGetValue(new Vector3Int(x, y, z), out var foundChunk))
					{
						foundChunks.Add(foundChunk);
					}
				}
			}
		}

		return foundChunks;
	}
}
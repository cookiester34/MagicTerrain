using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Chunks
{
	[CreateAssetMenu(fileName = "PlanetGenerator", menuName = "Magic Terrain/PlanetGenerator")]
	public class PreGeneratePlanet : ScriptableObject, ISerializationCallbackReceiver
	{
		[SerializeField]
		internal int planetSize = 200;

		[SerializeField]
		internal int octaves = 3;

		[SerializeField]
		internal float weightedStrength = 0f;

		[SerializeField]
		internal float lacunarity = 2f;

		[SerializeField]
		internal float gain = 0.5f;

		[SerializeField]
		internal int octavesCaves = 4;

		[SerializeField]
		internal float weightedStrengthCaves = 1f;

		[SerializeField]
		internal float lacunarityCaves = 2f;

		[SerializeField]
		internal float gainCaves = 1f;

		[SerializeField]
		internal float domainWarpAmp = 1.0f;

		[SerializeField]
		internal int chunkSize = 20;

		[SerializeField]
		internal int chunkScale = 1;

		[SerializeField]
		internal int seed = 1337;

		[SerializeField]
		internal bool smoothTerrain;

		[SerializeField]
		internal bool flatShaded;
		
		internal Dictionary<Vector3Int, Chunk> chunks = new();
		[SerializeField]
		internal List<Vector3Int> chunksKeys = new();
		[SerializeField]
		internal List<Chunk> chunksValues = new();

		private int chunkIncrement;

		public async void GeneratePlanet()
		{
			Debug.Log("Starting Chunk");
			chunks.Clear();
			chunkIncrement = chunkSize * chunkScale;

			var viewDistance = planetSize * 10;
			while (!IsDivisibleByChunkIncrement(viewDistance))
			{
				Debug.Log("Reducing Chunk Increment");
				viewDistance--;
			}
			
			for (var x = -viewDistance;
			     x < viewDistance;
			     x += chunkIncrement)
			{
				for (var y = -viewDistance;
				     y < viewDistance;
				     y += chunkIncrement)
				{
					for (var z = -viewDistance;
					     z < viewDistance;
					     z += chunkIncrement)
					{
						var distanceFromCenter = Vector3.Distance(new Vector3(x, y, z), Vector3.zero);
						var isWithinPlanet = distanceFromCenter <= planetSize * (2 * chunkScale);
						if (!isWithinPlanet)
						{
							continue;
						}

						var currentChunkPosition = new Vector3Int(x, y, z);
						var chunk = new Chunk(new ChunkData
						{
							seed = seed,
							chunkPositionReal = currentChunkPosition,
							chunkSize = chunkSize,
							scale = chunkScale,
							planetCenter = Vector3.zero,
							planetSize = planetSize,
							octaves = octaves,
							lacunarity = lacunarity,
							weightedStrength = weightedStrength,
							gain = gain,
							domainWarpAmp = domainWarpAmp,
							octavesCaves = octavesCaves,
							weightedStrengthCaves = weightedStrengthCaves,
							lacunarityCaves = lacunarityCaves,
							gainCaves = gainCaves,
							chunkManager = null
						},smoothTerrain, flatShaded);

						chunk.ScheduleChunkJobs(null, false, true, true);

						chunks.Add(currentChunkPosition, chunk);
					}
				}

				await Task.Delay(10); // Wait here to allow chunks to generate
				Debug.Log($"Total Number of chunks {chunks.Count}.");
			}

			Debug.Log($"Finished Generating Planet with {chunks.Count} chunks");
		}
		
		private bool IsDivisibleByChunkIncrement(int number)
		{
			return number % chunkIncrement == 0;
		}

		public void OnBeforeSerialize()
		{
			chunksKeys.Clear();
			chunksValues.Clear();
			foreach (var (key, value) in chunks)
			{
				chunksKeys.Add(key);
				chunksValues.Add(value);
			}
		}

		public void OnAfterDeserialize()
		{
			chunks.Clear();
			for (int i = 0; i < chunksKeys.Count; i++)
			{
				chunks.Add(chunksKeys[i], chunksValues[i]);
			}
		}
	}
}
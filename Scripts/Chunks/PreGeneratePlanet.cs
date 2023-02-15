using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Chunks
{
	[CreateAssetMenu(fileName = "PlanetGenerator", menuName = "Magic Terrain/PlanetGenerator")]
	public class PreGeneratePlanet : ScriptableObject
	{
		[SerializeField]
		private int planetSize = 200;

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

		[SerializeField]
		private int chunkScale = 1;

		[SerializeField]
		private int seed = 1337;

		[SerializeField]
		private bool smoothTerrain;

		[SerializeField]
		private bool flatShaded;

		[SerializeField]
		private List<Chunk> chunks = new();

		public void GeneratePlanet()
		{
			var chunkIncrement = chunkSize * chunkScale;

			for (var x = -planetSize;
			     x < planetSize;
			     x += chunkIncrement)
			{
				for (var y = -planetSize;
				     y < planetSize;
				     y += chunkIncrement)
				{
					for (var z = -planetSize;
					     z < planetSize;
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
							chunkPosition = currentChunkPosition,
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

						chunks.Add(chunk);
					}
				}
			}
		}
	}
}
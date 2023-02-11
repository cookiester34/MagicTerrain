using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.Chunks.Jobs
{
	[BurstCompile]
	public struct PopulateTerrainMapJob : IJobParallelFor
	{
		[ReadOnly]
		public int chunkSize;

		[ReadOnly]
		public Vector3Int chunkPosition;

		[ReadOnly]
		public bool isPlanet;

		[ReadOnly]
		public Vector3 planetCenter;

		[ReadOnly]
		public float planetSize;

		//planet values
		[ReadOnly]
		public int octaves;

		[ReadOnly]
		public float weightedStrength;

		[ReadOnly]
		public float lacunarity;

		[ReadOnly]
		public float gain;

		//Cave Values
		[ReadOnly]
		public int octavesCaves;

		[ReadOnly]
		public float weightedStrengthCaves;

		[ReadOnly]
		public float lacunarityCaves;

		[ReadOnly]
		public float gainCaves;

		[ReadOnly]
		public float domainWarpAmp;

		[NativeDisableParallelForRestriction]
		public NativeArray<float> terrainMap;

		public int seed;

		//Flat[x + HEIGHT* (y + WIDTH* z)] = Original[x, y, z], assuming Original[HEIGHT,WIDTH,DEPTH]

		[BurstCompile]
		public void Execute(int index)
		{
			// The data points for terrain are stored at the corners of our "cubes", so the terrainMap needs to be 1 larger
			// than the width/height of our mesh.
			for (var z = 0; z < chunkSize; z++)
			{
				for (var y = 0; y < chunkSize; y++)
				{
					// Get a terrain height using regular old Perlin noise.

					float xPos = chunkPosition.x + index;
					float yPos = chunkPosition.y + y;
					float zPos = chunkPosition.z + z;
					var noiseValue = FastNoiseLite.GetNoise(
						xPos * 1.5f + 0.01f,
						zPos * 1.5f + 0.01f,
						yPos * 1.5f + 0.01f,
						seed,
						octaves,
						weightedStrength,
						lacunarity,
						gain);
					//warp the planets coordinates to get hills and wobbles
					FastNoiseLite.DomainWarp(ref xPos, ref yPos, ref zPos, seed, octaves, lacunarity, gain,
						domainWarpAmp);
					var caveNoiseValue = FastNoiseLite.GetNoise(
						xPos * 0.4f + 0.01f,
						zPos * 0.4f + 0.01f,
						yPos * 0.4f + 0.01f,
						seed,
						octavesCaves,
						weightedStrengthCaves,
						lacunarityCaves,
						gainCaves);

					if (isPlanet)
					{
						var distance = Vector3.Distance(new Vector3(xPos, yPos, zPos), planetCenter);
						var t = 1f - Mathf.Exp(-0.3f * (distance - planetSize));
						var value = Mathf.Lerp(caveNoiseValue > 0.47f ? caveNoiseValue : noiseValue, 1f, t);
						terrainMap[index + chunkSize * (y + chunkSize * z)] = value;
						continue;
					}
					else
					{
						var surfaceNoise = FastNoiseLite.GetNoise(
							xPos * 1.5f + 0.01f,
							zPos * 1.5f + 0.01f,
							seed,
							octaves,
							weightedStrength,
							lacunarity,
							gain);
						var distance = Vector3.Distance(new Vector3(xPos, yPos, zPos), new Vector3(xPos, 0, zPos));
						var t = 1f - Mathf.Exp(-0.3f * (distance - 50f));
						var value = Mathf.Lerp(surfaceNoise > 0.47f ? surfaceNoise : noiseValue, 1f, t);
						terrainMap[index + chunkSize * (y + chunkSize * z)] = value;
						continue;
					}

					// Set the value of this point in the terrainMap.
					terrainMap[index + chunkSize * (y + chunkSize * z)] = noiseValue;
				}
			}
		}
	}
}
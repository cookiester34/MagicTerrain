using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static LookupTable;

[BurstCompile]
public struct PopulateWormMapJob : IJobParallelFor
{
	[ReadOnly]
	public int chunkSize;

	[ReadOnly]
	public Vector3Int chunkPosition;

	//Cave Values
	[ReadOnly]
	public int octavesCaves;

	[ReadOnly]
	public float lacunarityCaves;

	[ReadOnly]
	public float gainCaves;

	[ReadOnly]
	public float domainWarpAmp;

	[NativeDisableParallelForRestriction]
	public NativeArray<Vector3Int> wormMap;

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

				//warp the planets coordinates to get hills and wobbles
				FastNoiseLite.DomainWarp(
					ref xPos,
					ref yPos,
					ref zPos,
					seed,
					octavesCaves,
					lacunarityCaves,
					gainCaves,
					domainWarpAmp);
				var _x = FastNoiseLite.GetNoise(
					xPos + 0.01f,
					zPos + 0.01f,
					yPos + 0.01f,
					seed,
					1,
					0,
					1,
					1);
				_x = GetRoundedValue(_x);
				var _y = FastNoiseLite.GetNoise(
					xPos + 0.01f,
					zPos + 0.01f,
					yPos + 0.01f,
					seed + 1,
					1,
					0,
					1,
					1);
				_y = GetRoundedValue(_y);
				var _z = FastNoiseLite.GetNoise(
					xPos + 0.01f,
					zPos + 0.01f,
					yPos + 0.01f,
					seed + 2,
					1,
					0,
					1,
					1);
				_z = GetRoundedValue(_z);

				//inside the planet
				// Set the value of this point in the terrainMap.
				wormMap[index + chunkSize * (y + chunkSize * z)] = new Vector3Int((int)_x, (int)_y, (int)_z);
			}
		}
	}

	private static float GetRoundedValue(float val)
	{
		switch (val)
		{
			case < -0.4f:
				val = -1f;
				break;
			case > 0.4f:
				val = 1f;
				break;
			default:
				val = 0f;
				break;
		}

		return val;
	}
}

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
				FastNoiseLite.DomainWarp(ref xPos, ref yPos, ref zPos, seed, octaves, lacunarity, gain, domainWarpAmp);
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

[BurstCompile]
public struct CreateMeshDataJob : IJob
{
	[ReadOnly]
	public bool smoothTerrain;

	[ReadOnly]
	public bool flatShaded;

	[ReadOnly]
	public int chunkSize;

	[ReadOnly]
	public NativeArray<float> terrainMap;

	[ReadOnly]
	public float terrainSurface;

	public NativeArray<Vector3> vertices;
	public NativeArray<int> triangles;
	public NativeArray<float> cube;
	public NativeArray<int> triCount;
	public NativeArray<int> vertCount;

	[BurstCompile]
	public void Execute()
	{
		// Loop through each "cube" in our terrain.
		for (var x = 0; x < chunkSize - 1; x++)
		{
			for (var y = 0; y < chunkSize - 1; y++)
			{
				for (var z = 0; z < chunkSize - 1; z++)
				{
					// Create an array of floats representing each corner of a cube and get the value from our terrainMap.
					for (var i = 0; i < 8; i++)
					{
						var terrain = SampleTerrainMap(new int3(x, y, z) + CornerTable[i]);
						cube[i] = terrain;
					}

					// Pass the value into our MarchCube function.
					var position = new float3(x, y, z);
					// Get the configuration index of this cube.
					// Starting with a configuration of zero, loop through each point in the cube and check if it is below the terrain surface.
					var cubeIndex = 0;
					for (var i = 0; i < 8; i++)
					{
						// If it is, use bit-magic to the set the corresponding bit to 1. So if only the 3rd point in the cube was below
						// the surface, the bit would look like 00100000, which represents the integer value 32.
						if (cube[i] > terrainSurface)
							cubeIndex |= 1 << i;
					}


					// If the configuration of this cube is 0 or 255 (completely inside the terrain or completely outside of it) we don't need to do anything.
					if (cubeIndex is 0 or 255)
						continue;

					// Loop through the triangles. There are never more than 5 triangles to a cube and only three vertices to a triangle.
					MarchCube(cubeIndex, position);
				}
			}
		}
	}

	private void MarchCube(int cubeIndex, float3 position)
	{
		var edgeIndex = 0;
		for (var i = 0; i < 5; i++)
		{
			for (var p = 0; p < 3; p++)
			{
				// Get the current indie. We increment triangleIndex through each loop.
				//x * 16 + y = val
				var indice = TriangleTable[cubeIndex * 16 + edgeIndex];

				// If the current edgeIndex is -1, there are no more indices and we can exit the function.
				if (indice == -1)
					return;

				// Get the vertices for the start and end of this edge.
				//x * 2 + y = val
				var vert1 = position + CornerTable[EdgeIndexes[indice * 2 + 0]];
				var vert2 = position + CornerTable[EdgeIndexes[indice * 2 + 1]];

				Vector3 vertPosition;
				if (smoothTerrain)
				{
					// Get the terrain values at either end of our current edge from the cube array created above.
					var vert1Sample = cube[EdgeIndexes[indice * 2 + 0]];
					var vert2Sample = cube[EdgeIndexes[indice * 2 + 1]];

					// Calculate the difference between the terrain values.
					var difference = vert2Sample - vert1Sample;

					// If the difference is 0, then the terrain passes through the middle.
					if (difference == 0)
						difference = terrainSurface;
					else
						difference = (terrainSurface - vert1Sample) / difference;

					// Calculate the point along the edge that passes through.
					vertPosition = vert1 + difference * (vert2 - vert1);
				}
				else
				{
					// Get the midpoint of this edge.
					vertPosition = (vert1 + vert2) / 2f;
				}

				// Add to our vertices and triangles list and increment the edgeIndex.
				if (flatShaded)
				{
					var vCount = vertCount[0];
					vertices[vCount] = vertPosition;
					triangles[triCount[0]] = vCount;
					vertCount[0]++;
				}
				else
				{
					triangles[triCount[0]] = VertForIndice(vertPosition);
				}

				edgeIndex++;
				triCount[0]++;
			}
		}
	}

	private float SampleTerrainMap(int3 corner)
	{
		return terrainMap[corner.x + chunkSize * (corner.y + chunkSize * corner.z)];
	}

	private int VertForIndice(Vector3 vert)
	{
		// Loop through all the vertices currently in the vertices list.
		var vCount = vertCount[0];
		for (var index = 0; index < vCount; index++)
		{
			// If we find a vert that matches ours, then simply return this index.
			if (vertices[index] == vert)
				return index;
		}

		// If we didn't find a match, add this vert to the list and return last index.
		vertices[vCount] = vert;
		vertCount[0]++;
		return vCount;
	}
}
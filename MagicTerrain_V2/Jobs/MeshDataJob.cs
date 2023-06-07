using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static LookupTable;

namespace MagicTerrain_V2.Jobs
{
	[BurstCompile]
	public struct MeshDataJob : IJob, IChunkJob
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

		public NativeArray<float> cube;
		
		public NativeArray<int> triCount;
		public NativeArray<int> vertCount;
		
		public NativeArray<Vector3> vertices;
		public NativeArray<int> triangles;

		public NativeArray<Vector3> vertices1;
		public NativeArray<int> triangles1;
		
		public NativeArray<Vector3> vertices2;
		public NativeArray<int> triangles2;
		
		public NativeArray<Vector3> vertices3;
		public NativeArray<int> triangles3;
		
		public NativeArray<Vector3> vertices4;
		public NativeArray<int> triangles4;

		[BurstCompile]
		public void Execute()
		{
			for (int lodIndex = 0; lodIndex < 5; lodIndex++)
			{
				var lodIncrement = LodTable[lodIndex];
				
				// Loop through each "cube" in our terrain.
				for (var x = 0; x < chunkSize - 1; x+=lodIncrement)
				for (var y = 0; y < chunkSize - 1; y+=lodIncrement)
				for (var z = 0; z < chunkSize - 1; z+=lodIncrement)
				{
					// Create an array of floats representing each corner of a cube and get the value from our terrainMap.
					for (var i = 0; i < 8; i++)
					{
						var terrain = SampleTerrainMap(new int3(x, y, z) + CornerTable[i] * lodIncrement);
						cube[i] = terrain;
					}

					// Pass the value into our MarchCube function.
					var position = new float3(x, y, z);
					// Get the configuration index of this cube.
					// Starting with a configuration of zero, loop through each point in the cube and check if it is below the terrain surface.
					var cubeIndex = 0;
					for (var i = 0; i < 8; i++)
						// If it is, use bit-magic to the set the corresponding bit to 1. So if only the 3rd point in the cube was below
						// the surface, the bit would look like 00100000, which represents the integer value 32.
						if (cube[i] > terrainSurface)
							cubeIndex |= 1 << i;


					// If the configuration of this cube is 0 or 255 (completely inside the terrain or completely outside of it) we don't need to do anything.
					if (cubeIndex is 0 or 255)
						continue;

					// Loop through the triangles. There are never more than 5 triangles to a cube and only three vertices to a triangle.
					MarchCube(cubeIndex, position, lodIncrement, lodIndex);
				}
			}
		}

		private void MarchCube(int cubeIndex, float3 position, int lodIncrement, int lodIndex)
		{
			var edgeIndex = 0;
			for (var i = 0; i < 5; i++)
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
				var vert1 = position + CornerTable[EdgeIndexes[indice * 2 + 0]] * lodIncrement;
				var vert2 = position + CornerTable[EdgeIndexes[indice * 2 + 1]] * lodIncrement;

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
					var vCount = vertCount[lodIndex];
					var tricount = triCount[lodIndex];
					switch (lodIndex)
					{
						case 0:
							vertices[vCount] = vertPosition;
							triangles[tricount] = vCount;
							break;
						case 1:
							vertices1[vCount] = vertPosition;
							triangles1[tricount] = vCount;
							break;
						case 2:
							vertices2[vCount] = vertPosition;
							triangles2[tricount] = vCount;
							break;
						case 3:
							vertices3[vCount] = vertPosition;
							triangles3[tricount] = vCount;
							break;
						case 4:
							vertices4[vCount] = vertPosition;
							triangles4[tricount] = vCount;
							break;
					}
					vertCount[lodIndex]++;
				}
				else
				{
					var tricount = triCount[lodIndex];
					switch (lodIndex)
					{
						case 0:
							triangles[tricount] = VertForIndice(vertPosition, lodIndex);
							break;
						case 1:
							triangles1[tricount] = VertForIndice(vertPosition, lodIndex);
							break;
						case 2:
							triangles2[tricount] = VertForIndice(vertPosition, lodIndex);
							break;
						case 3:
							triangles3[tricount] = VertForIndice(vertPosition, lodIndex);
							break;
						case 4:
							triangles4[tricount] = VertForIndice(vertPosition, lodIndex);
							break;
					}
				}

				edgeIndex++;
				triCount[lodIndex]++;
			}
		}

		private float SampleTerrainMap(int3 corner)
		{
			return terrainMap[corner.x + chunkSize * (corner.y + chunkSize * corner.z)];
		}

		private int VertForIndice(Vector3 vert, int lodIndex)
		{
			// Loop through all the vertices currently in the vertices list.
			var vCount = vertCount[lodIndex];
			for (var index = 0; index < vCount; index++)
			{
				// If we find a vert that matches ours, then simply return this index.
				switch (lodIndex)
				{
					case 0:
						if (vertices[index] == vert) return index;
						break;
					case 1:
						if (vertices1[index] == vert) return index;
						break;
					case 2:
						if (vertices2[index] == vert) return index;
						break;
					case 3:
						if (vertices3[index] == vert) return index;
						break;
					case 4:
						if (vertices4[index] == vert) return index;
						break;
				}
			}

			// If we didn't find a match, add this vert to the list and return last index.
			switch (lodIndex)
			{
				case 0:
					vertices[vCount] = vert;
					vertCount[lodIndex]++;
					return vCount;
				case 1:
					vertices1[vCount] = vert;
					vertCount[lodIndex]++;
					return vCount;
				case 2:
					vertices2[vCount] = vert;
					vertCount[lodIndex]++;
					return vCount;
				case 3:
					vertices3[vCount] = vert;
					vertCount[lodIndex]++;
					return vCount;
				case 4:
					vertices4[vCount] = vert;
					vertCount[lodIndex]++;
					return vCount;
			}

			//should never reach this point
			return 0;
		}
	}
}
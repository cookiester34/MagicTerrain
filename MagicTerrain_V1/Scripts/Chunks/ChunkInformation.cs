using Unity.Collections;
using UnityEngine;

public struct ChunkInformation
{
	public NativeArray<int> lods;
	public NativeArray<float> terrainMap;
	public MeshInformation meshInformation;
}

public struct MeshInformation
{
	public NativeArray<Vector3> vertices;
	public NativeArray<int> triangles;
}
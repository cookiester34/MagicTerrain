using UnityEngine;

public struct ChunkData
{
	public int seed;
	public int chunkSize;
	public Vector3Int chunkPosition;
	public int scale;
	public float planetSize;
	public Vector3 planetCenter;
	public int octaves;
	public float weightedStrength;
	public float lacunarity;
	public float gain;
	public float domainWarpAmp;
	public int octavesCaves;
	public float weightedStrengthCaves;
	public float lacunarityCaves;
	public float gainCaves;
	public ChunkManager chunkManager;
}
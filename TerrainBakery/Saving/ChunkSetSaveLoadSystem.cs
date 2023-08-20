using System;
using TerrainBakery.Saving;
using System.Collections.Generic;
using System.IO;
using TerrainBakery;
using TerrainBakery.Helpers;
using UnityEngine;

public class ChunkSetSaveLoadSystem
{
	private readonly int chunkSetSize;
	private readonly string savePathDirectory;
	
	internal Dictionary<Vector3Int, ChunkSet> ChunkSets { get; } = new();
	private TerrainObjectPool<ChunkContainer> chunkContainerPool;
	private TerrainPool<Chunk> chunkPool;
	private Material coreMaterial;
	private ChunkCore chunkCore;

	public ChunkSetSaveLoadSystem(ChunkCore chunkCore, string planetName, int poolCount, int chunkSetSize,
		object[] chunkParameters,
		Material coreMaterial)
	{
		this.chunkCore = chunkCore;
		this.chunkSetSize = chunkSetSize;
		this.coreMaterial = coreMaterial;
		
		chunkContainerPool = new TerrainObjectPool<ChunkContainer>(poolCount, new[]{typeof(MeshRenderer), typeof(MeshCollider), typeof(MeshFilter)}, ApplyMaterialToChunkContainer);
		chunkPool = new TerrainPool<Chunk>(poolCount, chunkParameters);
		savePathDirectory = Path.Combine(Application.dataPath, $"TerrainBakerySaves/{planetName}");
		Debug.Log(savePathDirectory);
		if (!Directory.Exists(savePathDirectory))
		{
			Directory.CreateDirectory(savePathDirectory);
		}
	}

	private void ApplyMaterialToChunkContainer(ChunkContainer chunkContainer)
	{
		chunkContainer.material = coreMaterial;
	}

	private Vector3Int RoundVectorDownToNearestChunkSet(Vector3 position)
	{
		var x = Mathf.FloorToInt(position.x / chunkSetSize) * chunkSetSize;
		var y = Mathf.FloorToInt(position.y / chunkSetSize) * chunkSetSize;
		var z = Mathf.FloorToInt(position.z / chunkSetSize) * chunkSetSize;
		return new Vector3Int(x, y, z);
	}

	public Chunk RequestChunkFromPool()
	{
		return chunkPool.GetPoolObject();
	}

	public void ReturnChunkToPool(Chunk chunk)
	{
		chunk.Dispose();
		chunkContainerPool.ReturnPoolObject(chunk.ChunkContainer);
		chunkPool.ReturnPoolObject(chunk);
	}

	public Chunk RequestChunk(Vector3Int chunkPosition)
	{
		if (TryLoadChunkSet(RoundVectorDownToNearestChunkSet(chunkPosition), out var chunkSet))
		{
			var requestedChunk = chunkSet.RequestChunk(chunkPosition);
			requestedChunk.Position = chunkPosition;
			if (requestedChunk.ChunkContainer == null)
			{
				var requestedChunkChunkContainer = chunkContainerPool.GetPoolObject();
				requestedChunkChunkContainer.OnEdit += requestedChunk.EditChunk;
				requestedChunkChunkContainer.chunk = requestedChunk;
				requestedChunk.ChunkContainer = requestedChunkChunkContainer;
			}
			return requestedChunk;
		}

		throw new Exception($"No chunk was produced, a critical error, for chunk at position: {chunkPosition}");
	}

	public void SaveOutOfRangeChunkSets(Vector3 playerPosition)
	{
		List<Vector3Int> keysToRemove = new();
		var setSize = chunkSetSize;
		foreach (var (key, chunkSet) in ChunkSets)
		{
			var distance = Vector3.Distance(playerPosition, key);
			if (distance < setSize) continue;
			if (!chunkSet.CanBeUnloaded()) continue;
			
			var savePath = Path.Combine(savePathDirectory, $"{chunkSet.ChunkSetPosition}.mtcs");
			var file = File.Create(savePath);
			using var writer = new BinaryWriter(file);
			chunkSet.Serialize(writer);
			writer.Flush();
			file.Close();
			keysToRemove.Add(key);
		}

		foreach (var key in keysToRemove)
		{
			ChunkSets.Remove(key);
		}
	}

	public void SaveAllChunkSets()
	{
		foreach (var (_, chunkSet) in ChunkSets)
		{
			var savePath = Path.Combine(savePathDirectory, $"{chunkSet.ChunkSetPosition}.mtcs");
			var file = File.Create(savePath);
			using var writer = new BinaryWriter(file);
			chunkSet.Serialize(writer);
			writer.Flush();

			file.Close();
		}

		ChunkSets.Clear();
	}

	private bool TryLoadChunkSet(Vector3Int chunkSetPosition, out ChunkSet foundChunkSet)
	{
		//if we have the chunkSet return it
		if (ChunkSets.TryGetValue(chunkSetPosition, out foundChunkSet))
		{
			return true;
		}
		
		var savePath = Path.Combine(savePathDirectory, $"{chunkSetPosition}.mtcs");
		foundChunkSet = new ChunkSet(chunkCore, chunkSetPosition, this);
		
		//Check if the file exists if not create a new one
		if (!File.Exists(savePath))
		{
			if (ChunkSets.TryAdd(chunkSetPosition, foundChunkSet)) return true;
			Debug.LogError($"chunkset already exists at {chunkSetPosition}");
			return false;
		}

		//if the file does exist load it
		var file = File.Open(savePath, FileMode.Open);
		using var reader = new BinaryReader(file);
		foundChunkSet.Deserialize(reader);
		if (!ChunkSets.TryAdd(chunkSetPosition, foundChunkSet))
		{
			Debug.LogError($"chunkset already exists at {chunkSetPosition}");
			file.Close();
			return false;
		}

		file.Close();
		return true;
	}
}
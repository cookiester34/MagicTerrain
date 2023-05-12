using MagicTerrain_V2;
using MagicTerrain_V2.Saving;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class ChunkSetSaveLoadSystem
{
	public static Dictionary<Vector3Int, ChunkSet> ChunkSets { get; } = new();

	private static int chunkSetSize = 100;

	private static string savePathDirectory = Path.Combine(Application.dataPath, "MagicTerrainTestSaves");
	
	public static void InitializeChunkSetSaveLoadSystem(int size)
	{
		chunkSetSize = size;
		if (!Directory.Exists(savePathDirectory))
		{
			Directory.CreateDirectory(savePathDirectory);
		}
	}

	public static void AddChunkToChunkSet(Vector3Int chunkPosition, Chunk chunk)
	{
		var chunksetPosition = RoundVectorDownToNearestChunkSet(chunkPosition);

		if (!ChunkSets.ContainsKey(chunksetPosition))
		{
			if (!TryLoadChunkSet(chunksetPosition))
			{
				Debug.LogError($"Failed to load or create chunkset at {chunksetPosition}");
				return;
			}
		}
		if (!ChunkSets[chunksetPosition].Chunks.TryAdd(chunkPosition, chunk))
		{
			Debug.LogError($"Failed to add chunk to chunk set at {chunksetPosition}, chunk already exists");
		}
	}

	public static bool TryGetChunk(Vector3Int chunkPosition, out Chunk chunk)
	{
		var chunksetPosition = RoundVectorDownToNearestChunkSet(chunkPosition);
		if (!ChunkSets.ContainsKey(chunksetPosition))
		{
			if (!TryLoadChunkSet(chunksetPosition))
			{
				Debug.LogError($"Failed to load or create chunkset at {chunksetPosition}");
				chunk = null;
				return false;
			}
		}

		if (ChunkSets.TryGetValue(chunksetPosition, out var chunkSet))
		{
			return chunkSet.Chunks.TryGetValue(chunkPosition, out chunk);
		}
		chunk = null;
		return false;
	}
	
	public static Vector3Int RoundVectorDownToNearestChunkSet(Vector3 position)
	{
		var x = Mathf.FloorToInt(position.x / chunkSetSize) * chunkSetSize;
		var y = Mathf.FloorToInt(position.y / chunkSetSize) * chunkSetSize;
		var z = Mathf.FloorToInt(position.z / chunkSetSize) * chunkSetSize;
		return new Vector3Int(x, y, z);
	}
	
	public static void SaveOutOfRangeChunkSets(Vector3 playerPosition)
	{
		var range = chunkSetSize * 2;
		List<Vector3Int> keysToRemove = new();
		foreach (var (key, chunkSet) in ChunkSets)
		{
			var distance = Vector3.Distance(playerPosition, key);
			if (distance <= range) continue;
			
			var savePath = Path.Combine(savePathDirectory, $"{chunkSet.ChunkSetPosition}.mtcs");
			var file = File.Create(savePath);
			var formatter = BinaryFormatter;
			formatter.Serialize(file, chunkSet);
			file.Close();
				
			keysToRemove.Add(key);
		}

		foreach (var key in keysToRemove)
		{
			ChunkSets.Remove(key);
		}
	}
	
	public static bool TryLoadChunkSet(Vector3Int chunkSetPosition)
	{
		var savePath = Path.Combine(Application.persistentDataPath, $"{chunkSetPosition}.mtcs");
		if (!File.Exists(savePath))
		{
			Debug.Log($"No file found at {savePath}, Creating new ChunkSet");
			var newChunkSet = new ChunkSet(chunkSetPosition);
			if (ChunkSets.TryAdd(chunkSetPosition, newChunkSet)) return true;
			Debug.LogError($"chunkset already exists at {chunkSetPosition}");
			return false;
		}
		var file = File.Open(savePath, FileMode.Open);
		var formatter = new BinaryFormatter();
		var chunkSet = (ChunkSet) formatter.Deserialize(file);
		if (!ChunkSets.TryAdd(chunkSetPosition, chunkSet))
		{
			Debug.LogError($"chunkset already exists at {chunkSetPosition}");
			file.Close();
			return false;
		}
		file.Close();
		return true;
	}
	
	private static BinaryFormatter binaryFormatter;
	private static BinaryFormatter BinaryFormatter
	{
		get
		{
			if (binaryFormatter != null) return binaryFormatter;
			
			binaryFormatter = new BinaryFormatter();
 
			var surrogateSelector = new SurrogateSelector();
 
			{ // Vector3
				Vector3SerializationSurrogate vector3SS = new Vector3SerializationSurrogate();
				surrogateSelector.AddSurrogate(
					typeof(Vector3),
					new StreamingContext(StreamingContextStates.All),
					vector3SS);
			}
				
			{ // Vector3
				Vector3IntSerializationSurrogate vector3IntSS = new Vector3IntSerializationSurrogate();
				surrogateSelector.AddSurrogate(
					typeof(Vector3Int),
					new StreamingContext(StreamingContextStates.All),
					vector3IntSS);
			}
 
			binaryFormatter.SurrogateSelector = surrogateSelector;

			return binaryFormatter;
		}
	}
}
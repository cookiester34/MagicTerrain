using MagicTerrain_V2;
using MagicTerrain_V2.Saving;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

	public static void AddChunkToChunkSet(ChunkCore chunkCore, Vector3Int chunkPosition, Chunk chunk)
	{
		var chunksetPosition = RoundVectorDownToNearestChunkSet(chunkPosition);

		if (!ChunkSets.ContainsKey(chunksetPosition))
		{
			if (!TryLoadChunkSet(chunkCore, chunksetPosition))
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

	public static bool TryGetChunk(ChunkCore chunkCore, Vector3Int chunkPosition, out Chunk chunk)
	{
		var chunksetPosition = RoundVectorDownToNearestChunkSet(chunkPosition);
		if (!ChunkSets.ContainsKey(chunksetPosition))
		{
			if (!TryLoadChunkSet(chunkCore, chunksetPosition))
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

	public static void SaveOutOfRangeChunkSets(Vector3 playerPosition, float viewDistance)
	{
		var range = chunkSetSize;
		List<Vector3Int> keysToRemove = new();
		foreach (var (key, chunkSet) in ChunkSets)
		{
			var distance = Vector3.Distance(playerPosition, key);
			if (distance <= range) continue;

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

	public static void SaveAllChunkSets()
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

	public static bool TryLoadChunkSet(ChunkCore chunkCore, Vector3Int chunkSetPosition)
	{
		var savePath = Path.Combine(savePathDirectory, $"{chunkSetPosition}.mtcs");
		var chunkSet = new ChunkSet(chunkSetPosition, chunkCore);
		if (!File.Exists(savePath))
		{
			if (ChunkSets.TryAdd(chunkSetPosition, chunkSet)) return true;
			Debug.LogError($"chunkset already exists at {chunkSetPosition}");
			return false;
		}

		var file = File.Open(savePath, FileMode.Open);
		using var reader = new BinaryReader(file);
		chunkSet.Deserialize(reader);
		if (!ChunkSets.TryAdd(chunkSetPosition, chunkSet))
		{
			Debug.LogError($"chunkset already exists at {chunkSetPosition}");
			file.Close();
			return false;
		}

		file.Close();
		return true;
	}

	public static byte[] Compress(this float[] array) => CompressNativeTypeArray(array, sizeof(float));
	public static byte[] Compress(this int[] array) => CompressNativeTypeArray(array, sizeof(int));

	private static byte[] CompressNativeTypeArray<T>(T[] array, int size)
	{
		var arrayBytes = new byte[array.Length * size];
		Buffer.BlockCopy(array, 0, arrayBytes, 0, arrayBytes.Length);
		// Compress the byte array using gzip
		using var ms = new MemoryStream();
		using (var gzip = new GZipStream(ms, CompressionMode.Compress))
		{
			gzip.Write(arrayBytes, 0, arrayBytes.Length);
		}
		var compressedEditedValuesBytes = ms.ToArray();

		return compressedEditedValuesBytes;
	}

	public static float[] UncompressFloatArray (this byte[] array) => UncompressArray<float>(array, sizeof(float));
	public static int[] UncompressIntArray (this byte[] array) => UncompressArray<int>(array, sizeof(int));

	private static T[] UncompressArray<T>(byte[] byteArray, int size)
	{
		byte[] decompressedEditedValuesBytes;
		using (var ms = new MemoryStream(byteArray))
		{
			using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
			{
				using (var output = new MemoryStream())
				{
					gzip.CopyTo(output);
					decompressedEditedValuesBytes = output.ToArray();
				}
			}
		}
		// Convert the decompressed byte array back to a float array
		var editedPoints = new T[decompressedEditedValuesBytes.Length / size];
		Buffer.BlockCopy(decompressedEditedValuesBytes, 0, editedPoints, 0, decompressedEditedValuesBytes.Length);
		return editedPoints;
	}
}
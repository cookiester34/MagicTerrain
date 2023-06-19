using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MagicTerrain_V2.Saving
{
	[Serializable]
	public struct ChunkSet
	{
		[SerializeField]
		private Vector3Int chunkSetPosition;
		public Vector3Int ChunkSetPosition => chunkSetPosition;

		public Dictionary<Vector3Int, Chunk> Chunks { get; }

		private ChunkCore chunkCore;

		public ChunkSet(Vector3Int chunkSetPosition, ChunkCore chunkCore)
		{
			Chunks = new();
			this.chunkSetPosition = chunkSetPosition;
			this.chunkCore = chunkCore;
		}

		public void Dispose()
		{
			foreach (var (key, chunk) in Chunks)
			{
				chunk.Dispose();
			}
			Chunks.Clear();
		}

		public void Serialize(BinaryWriter writer)
		{
			//figure out which chunks have been edited
			Dictionary<Vector3Int, Chunk> editedChunks = new();
			foreach (var (key, chunk) in Chunks)
			{
				if (chunk.UnEditedLocalTerrainMap != null)
				{
					for (int i = 0; i < chunk.UnEditedLocalTerrainMap.Length; i++)
					{
						var nonEditedValue = chunk.UnEditedLocalTerrainMap[i];
						var editedValue = chunk.LocalTerrainMap[i];
						if (nonEditedValue == editedValue) continue;

						chunk.EditedPoints[i] = editedValue;
					}
				}
				editedChunks[key] = chunk;
			}
			writer.Write(editedChunks.Count);
			foreach (var (key, chunk) in editedChunks)
			{
				chunk.CompressChunkData();

				// key
				writer.Write(key.x);
				writer.Write(key.y);
				writer.Write(key.z);

				// value
				writer.Write(chunk.compressedEditedKeys.Length);
				for (var index = 0; index < chunk.compressedEditedKeys.Length; index++)
				{
					writer.Write(chunk.compressedEditedKeys[index]);
				}

				writer.Write(chunk.compressedEditedValues.Length);
				for (var index = 0; index < chunk.compressedEditedValues.Length; index++)
				{
					writer.Write(chunk.compressedEditedValues[index]);
				}

				chunk.Dispose();
				Chunks.Remove(key);
			}
		}

		public void Deserialize(BinaryReader reader)
		{
			var chunkCount = reader.ReadInt32();

			for (int i = 0; i < chunkCount; i++)
			{
				// key
				var x = reader.ReadInt32();
				var y = reader.ReadInt32();
				var z = reader.ReadInt32();

				var key = new Vector3Int(x, y, z);
				var chunk = new Chunk(ChunkCore.TERRAIN_SURFACE, chunkCore.smoothTerrain, chunkCore.flatShaded);
				chunk.ChunkSize = chunkCore.chunkSize;
				chunk.WasEdited = true;
				Chunks.Add(key, chunk);

				var editKeysCount = reader.ReadInt32();
				chunk.compressedEditedKeys = new byte[editKeysCount];
				for (int j = 0; j < editKeysCount; j++)
				{
					var index = reader.ReadByte();
					chunk.compressedEditedKeys[j] = index;
				}

				var editValuesCount = reader.ReadInt32();
				chunk.compressedEditedValues = new byte[editValuesCount];
				for (int j = 0; j < editValuesCount; j++)
				{
					var value = reader.ReadByte();
					chunk.compressedEditedValues[j] = value;
				}

				chunk.UncompressChunkData();
			}
		}
	}
}
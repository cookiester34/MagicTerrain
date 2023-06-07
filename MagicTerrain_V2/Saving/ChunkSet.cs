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

		public void Serialize(BinaryWriter writer)
		{
			//figure out which chunks have been edited
			Dictionary<Vector3Int, Chunk> editedChunks = new();
			foreach (var (key, chunk) in Chunks)
			{
				var wasEdited = false;
				if (chunk.UnEditedLocalTerrainMap != null)
				{
					for (int i = 0; i < chunk.UnEditedLocalTerrainMap.Length; i++)
					{
						var nonEditedValue = chunk.UnEditedLocalTerrainMap[i];
						var editedValue = chunk.LocalTerrainMap[i];
						if (nonEditedValue == editedValue) continue;

						chunk.EditedPoints[i] = editedValue;
						wasEdited = true;
					}
				}
				if (wasEdited)
				{
					editedChunks[key] = chunk;
				}
			}
			writer.Write(editedChunks.Count);
			foreach (var (key, chunk) in editedChunks)
			{
				// key
				writer.Write(key.x);
				writer.Write(key.y);
				writer.Write(key.z);

				// value
				writer.Write(chunk.EditedPoints.Count);
				foreach (var edit in chunk.EditedPoints)
				{
					writer.Write(edit.Key);
					writer.Write(edit.Value);
				}
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

				var editCount = reader.ReadInt32();
				for (int j = 0; j < editCount; j++)
				{
					var index = reader.ReadInt32();
					var value = reader.ReadSingle();
					chunk.EditedPoints[index] = value;
				}
			}
		}
	}
}
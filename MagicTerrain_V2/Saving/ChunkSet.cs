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

		public ChunkSet(Vector3Int chunkSetPosition)
		{
			Chunks = new();
			this.chunkSetPosition = chunkSetPosition;
		}

		public void MarkChunksAsDirty()
		{
			foreach (var (_, chunk) in Chunks)
			{
				chunk.IsDirty = true;
			}
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
						var value = chunk.UnEditedLocalTerrainMap[i];
						var editedValue = chunk.LocalTerrainMap[i];
						if (value == editedValue) continue;

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
				writer.Write((short)chunk.EditedPoints.Count);
				foreach (var edit in chunk.EditedPoints)
				{
					writer.Write((short)edit.Key);
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
				var chunk = new Chunk();
				Chunks.Add(key, chunk);

				var editCount = (int)reader.ReadInt16();
				for (int j = 0; j < editCount; j++)
				{
					var index = (int)reader.ReadInt16();
					var value = reader.ReadSingle();
					chunk.EditedPoints[index] = value;
				}
			}
		}
	}
}
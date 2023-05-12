using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTerrain_V2.Saving
{
	[Serializable]
	public struct ChunkSet : ISerializationCallbackReceiver
	{
		[SerializeField]
		private Vector3Int chunkSetPosition;
		public Vector3Int ChunkSetPosition => chunkSetPosition;

		[SerializeField]
		private List<Vector3Int> keys;
		[SerializeField]
		private List<Chunk> data;
		public Dictionary<Vector3Int, Chunk> Chunks { get; }

		public ChunkSet(Vector3Int chunkSetPosition)
		{
			Chunks = new();
			this.chunkSetPosition = chunkSetPosition;
			keys = new();
			data = new();
		}

		public void OnBeforeSerialize()
		{
			keys.Clear();
			data.Clear();
			foreach (var chunk in Chunks)
			{
				keys.Add(chunk.Key);
				data.Add(chunk.Value);
			}
		}

		public void OnAfterDeserialize()
		{
			Chunks.Clear();
			for (int i = 0; i < keys.Count; i++)
			{
				Chunks.Add(keys[i], data[i]);
			}
		}
	}
}
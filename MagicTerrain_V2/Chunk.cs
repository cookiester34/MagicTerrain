using MagicTerrain_V2.Saving;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Chunk
	{
		public float[] LocalTerrainMap { get;  set; }
		public int[] ChunkTriangles { get;  set; }
		public Vector3[] ChunkVertices { get;  set; }
		public Mesh[] Meshes { get; private set; }
		public bool IsDirty { get; set; }
		public bool EditsHaveBeenApplied { get; set; }
		public bool Hasdata => LocalTerrainMap != null;

		public Dictionary<int, float> EditedPoints { get; set; }= new();
		public bool WasEdited { get; set; }
		public int ChunkSize { get;  set; }

		public void BuildMesh()
		{
			Meshes ??= new Mesh[1];
			Meshes[0] = new Mesh();
			Meshes[0].vertices = ChunkVertices;
			Meshes[0].triangles = ChunkTriangles;
			Meshes[0].RecalculateNormals();
		}

		public void AddChunkEdit(int[] pointIndices, float[] pointValues, int count)
		{
			WasEdited = true;
			for (var i = 0; i < count; i++)
			{
				var index = pointIndices[i];
				EditedPoints[index] = pointValues[i];
			}
		}

		public void CompressChunkData()
		{
		}

		public void UncompressChunkData()
		{
		}

		public void ApplyChunkEdits()
		{
			if (EditedPoints == null || EditsHaveBeenApplied) return;
			foreach (var editedPoint in EditedPoints)
			{
				LocalTerrainMap[editedPoint.Key] = editedPoint.Value;
			}
			EditsHaveBeenApplied = true;
		}
	}
}
using MagicTerrain_V2.Helpers;
using MagicTerrain_V2.Jobs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Chunk
	{
		public float[] LocalTerrainMap { get;  set; }
		public float[] UnEditedLocalTerrainMap { get; set; }
		
		public int[] ChunkTriangles { get;  set; }
		public Vector3[] ChunkVertices { get;  set; }
		public Mesh[] Meshes { get; private set; }
		public bool IsDirty { get; set; }
		public bool EditsHaveBeenApplied { get; set; }
		public bool Hasdata => LocalTerrainMap != null;

		public Dictionary<int, float> EditedPoints { get; set; }= new();
		public int ChunkSize { get;  set; }
		public ChunkCore ChunkCore { get; set; }

		public void BuildMesh()
		{
			Meshes ??= new Mesh[1];
			Meshes[0] = new Mesh();
			Meshes[0].vertices = ChunkVertices;
			Meshes[0].triangles = ChunkTriangles;
			Meshes[0].RecalculateNormals();
		}

		public void CompressChunkData()
		{
		}

		public void UncompressChunkData()
		{
		}

		public void ApplyChunkEdits()
		{
			if (EditsHaveBeenApplied) return;
			foreach (var editedPoint in EditedPoints)
			{
				LocalTerrainMap[editedPoint.Key] = editedPoint.Value;
			}
			EditsHaveBeenApplied = true;
			IsDirty = false;
		}
	}
}
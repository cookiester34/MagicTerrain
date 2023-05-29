using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Chunk
	{
		public class MeshData
		{
			public int[] chunkTriangles;
			public Vector3[] chunkVertices;
		}
		
		public MeshData[] MeshDataSets { get; set; }
		public float[] LocalTerrainMap { get;  set; }
		public float[] UnEditedLocalTerrainMap { get; set; }
		public Mesh[] Meshes { get; private set; }
		public bool IsDirty { get; set; }
		public bool EditsHaveBeenApplied { get; set; }
		public bool Hasdata => LocalTerrainMap != null;
		public Dictionary<int, float> EditedPoints { get; set; }= new();
		public int ChunkSize { get;  set; }
		public ChunkCore ChunkCore { get; set; }
		public bool WasEdited { get; set; }

		public Chunk()
		{
			MeshDataSets = new MeshData[5];
			for (int i = 0; i < MeshDataSets.Length; i++)
			{
				MeshDataSets[i] = new MeshData();
			}
		}

		public void BuildMesh()
		{
			Meshes ??= new Mesh[5];
			for (var index = 0; index < Meshes.Length; index++)
			{
				Meshes[index] ??= new Mesh();
				Meshes[index].Clear();
				Meshes[index].vertices = MeshDataSets[index].chunkVertices;
				Meshes[index].triangles = MeshDataSets[index].chunkTriangles;
				Meshes[index].RecalculateNormals();
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
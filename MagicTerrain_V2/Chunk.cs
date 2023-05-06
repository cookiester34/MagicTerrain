﻿using System;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Chunk
	{
		[field:SerializeField]
		public float[] LocalTerrainMap { get;  set; }
		
		public int[] ChunkTriangles { get;  set; }
		public Vector3[] ChunkVertices { get;  set; }
		public Mesh[] Meshes { get; private set; }
		public bool Hasdata => LocalTerrainMap != null;
		
		public void BuildMesh()
		{
			Meshes ??= new Mesh[1];
			Meshes[0] = new Mesh();
			Meshes[0].vertices = ChunkVertices;
			Meshes[0].triangles = ChunkTriangles;
			Meshes[0].RecalculateNormals();
		}
	}
}
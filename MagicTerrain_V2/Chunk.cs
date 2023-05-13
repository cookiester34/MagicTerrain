using System;
using UnityEngine;

namespace MagicTerrain_V2
{
	[Serializable]
	public class Chunk
	{
		[field:NonSerialized]
		public float[] LocalTerrainMap { get;  set; }
		[field:NonSerialized]
		public int[] ChunkTriangles { get;  set; }
		[field:NonSerialized]
		public Vector3[] ChunkVertices { get;  set; }
		[field:NonSerialized]
		public Mesh[] Meshes { get; private set; }
		public bool Hasdata => LocalTerrainMap != null;
		
		public bool IsDirty { get; set; }
		
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
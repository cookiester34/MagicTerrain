using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class Chunk
	{
		public int[] ChunkTriangles { get;  set; }
		public Vector3[] ChunkVertices { get;  set; }
		public float[] LocalTerrainMap { get;  set; }
		public Vector3 Position { get; }
		public Mesh[] Meshes { get; private set; }
		public bool Hasdata => LocalTerrainMap != null;

		public Chunk(Vector3 position)
		{
			Position = position;
		}

		public void BuildMesh()
		{
			Meshes = new Mesh[1];
			Meshes[0] = new Mesh();
			Meshes[0].vertices = ChunkVertices;
			Meshes[0].triangles = ChunkTriangles;
			Meshes[0].RecalculateNormals();
		}
	}
}
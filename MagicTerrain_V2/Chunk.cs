using System;
using System.IO;
using System.IO.Compression;
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
		[field:NonSerialized]
		public bool IsDirty { get; set; }
		[field:NonSerialized]
		public bool wasEdited { get; set; }
		
		public bool Hasdata => LocalTerrainMap != null;
		
		private byte[] compressedTerrainMapBytes;

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
			if (LocalTerrainMap == null || !wasEdited) return;
			
			var localTerrainMapBytes = new byte[LocalTerrainMap.Length * sizeof(float)];
			Buffer.BlockCopy(LocalTerrainMap, 0, localTerrainMapBytes, 0, localTerrainMapBytes.Length);

			// Compress the byte array using gzip
			using (var ms = new MemoryStream())
			{
				using (var gzip = new GZipStream(ms, CompressionMode.Compress))
				{
					gzip.Write(localTerrainMapBytes, 0, localTerrainMapBytes.Length);
				}
				compressedTerrainMapBytes = ms.ToArray();
			}
		}

		public void UncompressChunkData()
		{
			if (compressedTerrainMapBytes == null) return;
			
			byte[] decompressedBytes;
			using (var ms = new MemoryStream(compressedTerrainMapBytes))
			{
				using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
				{
					using (var output = new MemoryStream())
					{
						gzip.CopyTo(output);
						decompressedBytes = output.ToArray();
					}
				}
			}
			
			// Convert the decompressed byte array back to a float array
			LocalTerrainMap = new float[decompressedBytes.Length / sizeof(float)];
			Buffer.BlockCopy(decompressedBytes, 0, LocalTerrainMap, 0, decompressedBytes.Length);
		}
	}
}
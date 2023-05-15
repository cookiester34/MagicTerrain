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
		[field:NonSerialized]
		public int chunkSize { get;  set; }
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
		public bool WasEdited { get; set; }


		//this is the only data that will get saved
		public List<float> editedValues { get; set; } = new();
		public List<int> editedTerrainMapIndexs { get; set; } = new();

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

		public void AddChunkEdit(List<Vector3Int> points, Vector3Int diferenceInPosition)
		{
			foreach (var pointPosition in points)
			{
				//get the point relative to this chunk
				var relativePosition = pointPosition + diferenceInPosition;

				//check if the point is within the chunk
				if (relativePosition.x < 0 || relativePosition.y < 0 || relativePosition.z < 0
				    || relativePosition.x >= chunkSize || relativePosition.y >= chunkSize || relativePosition.z >= chunkSize) return;

				var terrainMapIndex = relativePosition.x + chunkSize * (relativePosition.y + chunkSize * relativePosition.z);

				editedValues.Add(LocalTerrainMap[terrainMapIndex]);
				editedTerrainMapIndexs.Add(terrainMapIndex);
			}
		}

		public void CompressChunkData()
		{
			if (EditedPoints == null) return; //editedValues

			var localTerrainMapBytes = new byte[EditedPoints.Count * 8];
			Buffer.BlockCopy(EditedPoints.ToArray(), 0, localTerrainMapBytes, 0, localTerrainMapBytes.Length);

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
			var editedPoints = new ChunkEditData[decompressedBytes.Length / 8];
			Buffer.BlockCopy(decompressedBytes, 0, editedPoints, 0, decompressedBytes.Length);

			foreach (var editData in editedPoints)
			{
				LocalTerrainMap[editData.ArrayPosition] = editData.Value;
			}

			WasEdited = true;
			EditedPoints.AddRange(editedPoints);
		}
	}
}
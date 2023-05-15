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
		[field:NonSerialized]
		public bool EditsHaveBeenApplied { get; set; }
		
		//this is the only data that will get saved
		[field:NonSerialized]
		public List<float> editedValues { get; set; } = new();
		[field:NonSerialized]
		public List<int> editedTerrainMapIndexs { get; set; } = new();

		public bool Hasdata => LocalTerrainMap != null;

		public int chunkSize { get;  set; }
		private byte[] compressedEditedValuesBytes;
		private byte[] compressedTerrainMapIndicesBytes;

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
			WasEdited = true;
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
			if (editedValues == null) return; //editedValues

			var localTerrainMapValuesBytes = new byte[editedValues.Count * 4];
			Buffer.BlockCopy(editedValues.ToArray(), 0, localTerrainMapValuesBytes, 0, localTerrainMapValuesBytes.Length);

			// Compress the byte array using gzip
			using (var ms = new MemoryStream())
			{
				using (var gzip = new GZipStream(ms, CompressionMode.Compress))
				{
					gzip.Write(localTerrainMapValuesBytes, 0, localTerrainMapValuesBytes.Length);
				}
				compressedEditedValuesBytes = ms.ToArray();
			}

			var localTerrainMapIndicesBytes = new byte[editedTerrainMapIndexs.Count * 4];
			Buffer.BlockCopy(editedTerrainMapIndexs.ToArray(), 0, localTerrainMapIndicesBytes, 0, localTerrainMapIndicesBytes.Length);

			// Compress the byte array using gzip
			using (var ms = new MemoryStream())
			{
				using (var gzip = new GZipStream(ms, CompressionMode.Compress))
				{
					gzip.Write(localTerrainMapIndicesBytes, 0, localTerrainMapIndicesBytes.Length);
				}
				compressedTerrainMapIndicesBytes = ms.ToArray();
			}
		}

		public void UncompressChunkData()
		{
			if (compressedEditedValuesBytes == null) return;

			byte[] decompressedEditedValuesBytes;
			using (var ms = new MemoryStream(compressedEditedValuesBytes))
			{
				using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
				{
					using (var output = new MemoryStream())
					{
						gzip.CopyTo(output);
						decompressedEditedValuesBytes = output.ToArray();
					}
				}
			}
			// Convert the decompressed byte array back to a float array
			var editedPoints = new float[decompressedEditedValuesBytes.Length / 4];
			Buffer.BlockCopy(decompressedEditedValuesBytes, 0, editedPoints, 0, decompressedEditedValuesBytes.Length);

			byte[] decompressedTerrainMapIndicesBytes;
			using (var ms = new MemoryStream(compressedTerrainMapIndicesBytes))
			{
				using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
				{
					using (var output = new MemoryStream())
					{
						gzip.CopyTo(output);
						decompressedTerrainMapIndicesBytes = output.ToArray();
					}
				}
			}
			// Convert the decompressed byte array back to a float array
			var editedPointIndices = new int[decompressedTerrainMapIndicesBytes.Length / 4];
			Buffer.BlockCopy(decompressedTerrainMapIndicesBytes, 0, editedPointIndices, 0, decompressedTerrainMapIndicesBytes.Length);

			WasEdited = true;
			editedValues ??= new List<float>();
			editedValues.AddRange(editedPoints);
			editedTerrainMapIndexs ??= new List<int>();
			editedTerrainMapIndexs.AddRange(editedPointIndices);
		}
		
		public void ApplyChunkEdits()
		{
			if (editedValues == null || EditsHaveBeenApplied) return;
			if (editedValues.Count > 0)
			{
				for (var i = 0; i < editedValues.Count; i++)
				{
					LocalTerrainMap[editedTerrainMapIndexs[i]] = editedValues[i];
				}

				EditsHaveBeenApplied = true;
			}
		}
	}
}
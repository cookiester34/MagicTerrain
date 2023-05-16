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
		public bool EditsHaveBeenApplied { get; set; }

		//this is the only data that will get saved
		[field:NonSerialized]
		public List<float> EditedValues { get; set; } = new();
		[field:NonSerialized]
		public List<int> EditedTerrainMapIndices { get; set; } = new();

		public bool Hasdata => LocalTerrainMap != null;

		public bool WasEdited { get; set; }
		public int ChunkSize { get;  set; }
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

		public void AddChunkEdit(int[] pointIndices, float[] pointValues, int count)
		{
			WasEdited = true;
			for (var i = 0; i < count; i++)
			{
				var containsPoint = false;
				var pointIndex = pointIndices[i];
				for (var index = 0; index < EditedTerrainMapIndices.Count; index++)
				{
					var terrainMapIndex = EditedTerrainMapIndices[index];
					if (terrainMapIndex == pointIndex)
					{
						EditedValues[index] = pointValues[i];
						containsPoint = true;
						break;
					}
				}

				if (containsPoint) continue;
				EditedValues.Add(pointValues[i]);
				EditedTerrainMapIndices.Add(pointIndex);
			}
		}

		public void CompressChunkData()
		{
			if (EditedValues == null) return; //editedValues

			compressedEditedValuesBytes = EditedValues.ToArray().Compress();

			compressedTerrainMapIndicesBytes = EditedTerrainMapIndices.ToArray().Compress();
		}

		public void UncompressChunkData()
		{
			if (compressedEditedValuesBytes == null) return;

			WasEdited = true;
			EditedValues ??= new List<float>();
			EditedValues.AddRange(compressedEditedValuesBytes.UncompressFloatArray());
			EditedTerrainMapIndices ??= new List<int>();
			EditedTerrainMapIndices.AddRange(compressedTerrainMapIndicesBytes.UncompressIntArray());
		}

		public void ApplyChunkEdits()
		{
			if (EditedValues == null || EditsHaveBeenApplied) return;
			if (EditedValues.Count > 0)
			{
				for (var i = 0; i < EditedValues.Count; i++)
				{
					LocalTerrainMap[EditedTerrainMapIndices[i]] = EditedValues[i];
				}

				EditsHaveBeenApplied = true;
			}
		}
	}
}
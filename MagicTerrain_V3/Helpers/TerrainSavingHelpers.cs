using System.IO.Compression;
using System.IO;
using System;

namespace MagicTerrain_V3.Helpers
{
	public static class TerrainSavingHelpers
	{
		public static byte[] Compress(this float[] array) => CompressNativeTypeArray(array, sizeof(float));
		public static byte[] Compress(this int[] array) => CompressNativeTypeArray(array, sizeof(int));

		private static byte[] CompressNativeTypeArray<T>(T[] array, int size)
		{
			var arrayBytes = new byte[array.Length * size];
			Buffer.BlockCopy(array, 0, arrayBytes, 0, arrayBytes.Length);
			// Compress the byte array using gzip
			using var ms = new MemoryStream();
			using (var gzip = new GZipStream(ms, CompressionMode.Compress))
			{
				gzip.Write(arrayBytes, 0, arrayBytes.Length);
			}
			var compressedEditedValuesBytes = ms.ToArray();

			return compressedEditedValuesBytes;
		}

		public static float[] UncompressFloatArray (this byte[] array) => UncompressArray<float>(array, sizeof(float));
		public static int[] UncompressIntArray (this byte[] array) => UncompressArray<int>(array, sizeof(int));

		private static T[] UncompressArray<T>(byte[] byteArray, int size)
		{
			byte[] decompressedEditedValuesBytes;
			using (var ms = new MemoryStream(byteArray))
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
			var editedPoints = new T[decompressedEditedValuesBytes.Length / size];
			Buffer.BlockCopy(decompressedEditedValuesBytes, 0, editedPoints, 0, decompressedEditedValuesBytes.Length);
			return editedPoints;
		}
	}
}
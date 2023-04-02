using UnityEngine;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class Chunk
	{
		public Vector3 position;
		public int size;

		public Chunk(Vector3 position, int size)
		{
			this.position = position;
			this.size = size;
		}
	}
}
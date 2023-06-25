using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainBakery.Helpers
{
	public class TerrainObjectPool<PoolType> where PoolType : MonoBehaviour
	{
		private readonly List<PoolType> unusedPool = new();
		private readonly List<PoolType> usedPool = new();
		
		private readonly Transform poolParent;
		private readonly Type[] componentsRequired;
		private Action<PoolType> applyMaterialToChunkContainer;

		public TerrainObjectPool(int initialPoolCount, Type[] componentsRequired = null,
			Action<PoolType> applyMaterialToChunkContainer = null)
		{
			this.componentsRequired = componentsRequired;
			this.applyMaterialToChunkContainer = applyMaterialToChunkContainer;
			
			poolParent = new GameObject($"Pool<{typeof(PoolType).Name}>").transform;
			for (var i = 0; i < initialPoolCount; i++)
			{
				var newPoolObject = CreateNewPoolObject();
				unusedPool.Add(newPoolObject);
			}
		}

		private PoolType CreateNewPoolObject()
		{
			var poolObject = new GameObject($"{typeof(PoolType).Name}-Object", componentsRequired);
			var newObject = poolObject.AddComponent<PoolType>();
			applyMaterialToChunkContainer?.Invoke(newObject);
			poolObject.transform.SetParent(poolParent);
			return newObject;
		}

		public PoolType GetPoolObject()
		{
			if (unusedPool.Count > 0)
			{
				var poolObject = unusedPool[0];
				unusedPool.RemoveAt(0);
				usedPool.Add(poolObject);
				return poolObject;
			}

			var newObject = CreateNewPoolObject();
			usedPool.Add(newObject);
			return newObject;
		}

		public void ReturnPoolObject(PoolType poolObject)
		{
			if (poolObject == null) return;
			usedPool.Remove(poolObject);
			unusedPool.Add(poolObject);
		}
	}

	public class TerrainPool<PoolType> where PoolType : class
	{
		private readonly List<PoolType> unusedPool = new();
		private readonly List<PoolType> usedPool = new();
		
		private readonly object[] parameters;
		
		public TerrainPool(int initialPoolCount, params object[] parameters)
		{
			this.parameters = parameters;
			
			for (var i = 0; i < initialPoolCount; i++)
			{
				var newPoolObject = Activator.CreateInstance(typeof(PoolType), parameters);
				unusedPool.Add((PoolType)newPoolObject);
			}
		}
		
		private PoolType CreateNewPoolObject()
		{
			var newPoolObject = Activator.CreateInstance(typeof(PoolType), parameters);
			return (PoolType)newPoolObject;
		}

		public PoolType GetPoolObject()
		{
			if (unusedPool.Count > 0)
			{
				var poolObject = unusedPool[0];
				unusedPool.RemoveAt(0);
				usedPool.Add(poolObject);
				return poolObject;
			}

			var newObject = CreateNewPoolObject();
			usedPool.Add(newObject);
			return newObject;
		}

		public void ReturnPoolObject(PoolType poolObject)
		{
			usedPool.Remove(poolObject);
			unusedPool.Add(poolObject);
		}
	}
}
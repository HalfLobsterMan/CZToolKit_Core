#region 注 释

/***
 *
 *  Title: ""
 *      主题: 对象池基类，只有最基本的功能
 *  Description:
 *      功能:
 *      	1.生成一个对象
 *      	2.回收一个对象
 *  Date:
 *  Version:
 *  Writer: 半只龙虾人
 *  Github: https://github.com/haloman9527
 *  Blog: https://www.haloman.net/
 *
 */

#endregion

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Moyo
{
    public class CustomPoolAttribute : Attribute
    {
        public readonly Type unitType;

        public CustomPoolAttribute(Type unitType)
        {
            this.unitType = unitType;
        }
    }
    
    public static partial class ObjectPools
    {
        private class ObjectPool<T> : BaseObjectPool<T> where T : class, new()
        {
            protected override T Create()
            {
                return new T();
            }
        }
    }

    public static partial class ObjectPools
    {
        private static bool s_Initialized;
        private static Dictionary<int, IObjectPool> s_Pools = new Dictionary<int, IObjectPool>(64);

        static ObjectPools()
        {
            Init();
        }
        
        public static void Init(bool force = false)
        {
            if (s_Initialized && !force)
            {
                return;
            }

            s_Pools.Clear();

            var baseType = typeof(IObjectPool);
            foreach (var type in TypesCache.AllTypes)
            {
                if (!baseType.IsAssignableFrom(type))
                {
                    continue;
                }

                var attribute = type.GetCustomAttribute<CustomPoolAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                var pool = Activator.CreateInstance(type);
                s_Pools.Add(attribute.unitType.GetHashCode(), pool as IObjectPool);
            }
        }

        private static IObjectPool GetOrCreatePool(Type unitType)
        {
            if (!s_Pools.TryGetValue(unitType.GetHashCode(), out var pool))
            {
                var poolType = typeof(ObjectPool<>).MakeGenericType(unitType);
                s_Pools[unitType.GetHashCode()] = pool = (IObjectPool)Activator.CreateInstance(poolType);
            }

            return pool;
        }

        private static IObjectPool<T> GetOrCreatePool<T>() where T : class, new()
        {
            var unitType = TypeCache<T>.TYPE;
            if (!s_Pools.TryGetValue(TypeCache<T>.HASH, out var pool))
            {
                s_Pools[unitType.GetHashCode()] = pool = new ObjectPool<T>();
            }

            return (IObjectPool<T>)pool;
        }

        public static IObjectPool GetPool(Type unitType)
        {
            s_Pools.TryGetValue(unitType.GetHashCode(), out var pool);
            return pool;
        }

        public static void RegisterPool(IObjectPool pool)
        {
            s_Pools.Add(pool.UnitType.GetHashCode(), pool);
        }

        public static void ReleasePool(Type unitType)
        {
            var pool = GetPool(unitType);
            if (pool == null)
                return;

            pool.Dispose();
            s_Pools.Remove(unitType.GetHashCode());
        }

        public static T Spawn<T>() where T : class, new()
        {
            var unit = GetOrCreatePool<T>().Spawn();
            if (unit is IPoolableObject poolableObject)
            {
                poolableObject.OnSpawn();
            }

            return unit;
        }

        public static object Spawn(Type unitType)
        {
            var unit = GetOrCreatePool(unitType).Spawn();
            if (unit is IPoolableObject poolableObject)
            {
                poolableObject.OnSpawn();
            }

            return unit;
        }

        public static void Recycle(Type unitType, object unit)
        {
            var pool = GetPool(unitType);
            if (pool == null)
                return;

            if (unit is IPoolableObject poolableObject)
            {
                poolableObject.OnRecycle();
            }

            pool.Recycle(unit);
        }

        public static void Recycle(object unit)
        {
            Recycle(unit.GetType(), unit);
        }
    }
}
using UnityEngine;

namespace Pooling
{
    public abstract class PoolablePool<T> : ComponentPool<T> where T : Component, IPoolable
    {
        protected override VariantPool<T> CreateVariantPool()
        {
            return new PoolableVariantPool<T>();
        }
    }
}

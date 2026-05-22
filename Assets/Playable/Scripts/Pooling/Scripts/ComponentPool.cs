using UnityEngine;

namespace Pooling
{
    public abstract class ComponentPool<T> : ObjectPool<T> where T : Component
    {
        protected override VariantPool<T> CreateVariantPool()
        {
            return new ComponentVariantPool<T>();
        }
    }
}
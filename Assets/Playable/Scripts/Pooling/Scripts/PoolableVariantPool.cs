using UnityEngine;

namespace Pooling
{
    public class PoolableVariantPool<T> : ComponentVariantPool<T> where T : Component, IPoolable
    {
        protected override void OnReturn(T instance)
        {
            instance.CleanUp();
            base.OnReturn(instance);
        }
    }
}
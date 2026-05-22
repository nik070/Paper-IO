using UnityEngine;

namespace Pooling
{
    public class ComponentVariantPool<T> : VariantPool<T> where T : Component
    {
        protected override void OnGet(T instance)
        {
            base.OnGet(instance);
            instance.gameObject.SetActive(true);
        }

        protected override void OnReturn(T instance)
        {
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(Container);
            base.OnReturn(instance);
        }

        protected override void Destroy(T instance)
        {
            Object.Destroy(instance.gameObject);
        }
    }
}
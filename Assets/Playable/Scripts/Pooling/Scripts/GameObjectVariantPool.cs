using UnityEngine;

namespace Pooling
{
    public class GameObjectVariantPool : VariantPool<GameObject>
    {
        protected override void OnGet(GameObject instance)
        {
            base.OnGet(instance);
            instance.SetActive(true);
        }

        protected override void OnReturn(GameObject instance)
        {
            instance.SetActive(false);
            instance.transform.SetParent(Container);
            base.OnReturn(instance);
        }

        protected override void Destroy(GameObject instance)
        {
            Object.Destroy(instance);
        }
    }
}
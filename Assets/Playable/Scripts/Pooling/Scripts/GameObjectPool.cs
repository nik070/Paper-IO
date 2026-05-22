using UnityEngine;

namespace Pooling
{
    public class GameObjectPool : ObjectPool<GameObject>
    {
        [SerializeField] private GameObjectsPoolConfig _config;

        protected override ObjectPoolConfig Config => _config;

        protected override VariantPool<GameObject> CreateVariantPool()
        {
            return new GameObjectVariantPool();
        }
    }
}
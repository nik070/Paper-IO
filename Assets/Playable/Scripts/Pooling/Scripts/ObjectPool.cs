using System.Collections.Generic;
using UnityEngine;

namespace Pooling
{
    public abstract class ObjectPool<T> : MonoBehaviour, IPool where T : Object
    {
        private readonly Dictionary<string, VariantPool<T>> _variantPools = new Dictionary<string, VariantPool<T>>();

        protected abstract ObjectPoolConfig Config { get; }

        public bool IsDisposed { get; private set; }

        public string Name => name;
        public virtual bool IsEnabled => true;

        public void Init()
        {
            if (IsEnabled == false)
            {
                return;
            }

            ObjectPoolConfig config = Config;
            Debug.Log($"[{typeof(T).Name}][{Name}] Initializing pool. VariantsCount={config.Variants.Count}");

            foreach (VariantPoolConfig variantConfig in config.Variants)
            {
                string variantName = string.IsNullOrEmpty(variantConfig.Variant) ? "Default" : variantConfig.Variant;
                Transform variantTransform = GetOrCreateTransform(variantName, config.Variants.Count);
                VariantPool<T> variantPool = CreateVariantPool();
                variantPool.Init(variantConfig, variantTransform);
                variantPool.Prewarm();
                _variantPools[variantName] = variantPool;
            }
        }

        public List<T> GetActiveItems(string variant = "")
        {
            if (_variantPools.TryGetValue(variant, out VariantPool<T> variantPool) == false)
            {
                Debug.LogError($"[{typeof(T).Name}][{Name}] Variant pool doesn't exist: {variant}");
                return null;
            }

            return variantPool.ActiveItems;
        }

        protected abstract VariantPool<T> CreateVariantPool();

        private Transform GetOrCreateTransform(string variantName, int variantsCount)
        {
            if (variantsCount <= 1)
            {
                return transform;
            }

            var variantLayer = new GameObject(variantName);
            variantLayer.transform.SetParent(transform);

            return variantLayer.transform;
        }

        public void Prewarm()
        {
            if (IsEnabled == false)
            {
                return;
            }

            foreach (VariantPool<T> variantPool in _variantPools.Values)
            {
                variantPool.Prewarm();
            }
        }

        public void Dispose()
        {
            foreach (VariantPool<T> variantPool in _variantPools.Values)
            {
                variantPool.Dispose();
            }

            IsDisposed = true;
        }

        public T Get(string variant = "Default")
        {
            if (_variantPools.TryGetValue(variant, out VariantPool<T> variantPool) == false)
            {
                Debug.LogError($"[{typeof(T).Name}][{Name}] Variant pool doesn't exist: {variant}");
                return null;
            }

            return variantPool.Get();
        }

        public void ReturnAll()
        {
            if (IsDisposed)
            {
                Debug.LogError($"[{typeof(T).Name}][{Name}] Trying to use already disposed pool.");
                return;
            }

            foreach (VariantPool<T> variantPool in _variantPools.Values)
            {
                variantPool.ReturnAll();
            }
        }

        public void Return(T instance, string variant = "Default")
        {
            if (IsDisposed)
            {
                Debug.LogError($"[{typeof(T).Name}][{Name}] Trying to use already disposed pool.");
                return;
            }

            if (_variantPools.TryGetValue(variant, out VariantPool<T> variantPool) == false)
            {
                Debug.LogError($"[{typeof(T).Name}][{Name}] Variant pool doesn't exist: {variant}");
                return;
            }

            variantPool.Return(instance);
        }

#if UNITY_EDITOR
        public PoolData CreatePoolData()
        {
            ObjectPoolConfig config = Config;
            var variants = new List<PoolVariantData>(config.Variants.Count);
            foreach (VariantPoolConfig variantConfig in config.Variants)
            {
                var setup = new PoolSetupData
                {
                    InitialSize = variantConfig.InitialSize,
                    Increment = variantConfig.Increment,
                    GrowCap = variantConfig.GrowCap,
                    PoolCap = variantConfig.PoolCap
                };

                var variant = new PoolVariantData
                {
                    Name = string.IsNullOrEmpty(variantConfig.Variant) ? "Default" : variantConfig.Variant,
                    Setup = setup
                };

                variants.Add(variant);
            }

            return new PoolData
            {
                Name = name,
                IsEnabled = IsEnabled,
                Variants = variants
            };
        }

        public void UpdatePoolData(PoolData poolData)
        {
            if (poolData.IsEnabled == false)
            {
                return;
            }

            foreach (PoolVariantData variantData in poolData.Variants)
            {
                VariantPool<T> variantPool = _variantPools[variantData.Name];
                variantPool.UpdatePoolData(variantData);
            }
        }
#endif
    }
}

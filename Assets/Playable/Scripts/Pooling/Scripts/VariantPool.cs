using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pooling
{
    public abstract class VariantPool<T> where T : Object
    {
        private VariantPoolConfig _config;
        protected Transform Container;
        private string _containerName;
        private bool _isGrowing;

        private readonly Queue<T> _inactiveItems = new Queue<T>();
        private readonly List<T> _activeItems = new List<T>();

        public Queue<T> InactiveItems => _inactiveItems;
        public List<T> ActiveItems => _activeItems;

        public void Init(VariantPoolConfig config, Transform container)
        {
            _config = config;
            Container = container;
            _containerName = Container.name;
        }

        public void Prewarm()
        {
            Debug.Log($"[{typeof(T).Name}][{_containerName}] Prewarming pool. InitialSize={_config.InitialSize}");
            InitItems(_config.InitialSize);
        }

        public void Dispose()
        {
            ReturnAll();
        }

        public T Get()
        {
            T instance = _inactiveItems.Count > 0 ? _inactiveItems.Dequeue() : Create();
            if (instance == false)
            {
                Debug.LogError($"[{typeof(T).Name}][{_containerName}] Failed to get instance");
                return null;
            }

            _activeItems.Add(instance);

            OnGet(instance);

            TryGrowing();

            return instance;
        }

        protected virtual void OnGet(T instance)
        {
        }

        public void ReturnAll()
        {
            for (int i = _activeItems.Count - 1; i >= 0; i--)
            {
                T instance = _activeItems[i];
                if (instance)
                {
                    Return(instance);
                }
            }

            _activeItems.Clear();
        }

        public void Return(T instance)
        {
            if (instance == null)
            {
                Debug.LogError($"[{typeof(T).Name}][{_containerName}] Trying to return null instance");
                return;
            }

            if (_activeItems.Contains(instance) == false)
            {
                Debug.LogError($"[{typeof(T).Name}][{_containerName}] Trying to return a non-active instance");
                return;
            }

            _activeItems.Remove(instance);

            if (_config.PoolCap > 0 && _inactiveItems.Count < _config.PoolCap)
            {
                OnReturn(instance);
                _inactiveItems.Enqueue(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        protected virtual void OnReturn(T instance)
        {
        }

        private T Create()
        {
            if (_config.Prefab == null)
            {
                Debug.LogError($"[{typeof(T).Name}][{_containerName}] Prefab is null. Cannot create instance.");
                return null;
            }

            GameObject instance = Object.Instantiate(_config.Prefab, Container);
            if (instance is T result)
            {
                return result;
            }

            return instance.GetComponent<T>();
        }

        protected abstract void Destroy(T instance);

        private void TryGrowing()
        {
            if (_isGrowing || _inactiveItems.Count > 0 || _config.Increment <= 0)
            {
                return;
            }

            int diff = _config.GrowCap - _activeItems.Count;
            if (diff <= 0)
            {
                return;
            }

            diff = Mathf.Min(diff, _config.Increment);

            Debug.Log($"[{typeof(T).Name}][{_containerName}] Growing pool. IncrementSize={diff}");
            InitItems(diff);
        }

        private void InitItems(int count)
        {
            _isGrowing = true;

            for (int i = 0; i < count; i++)
            {
                T instance = Create();
                _inactiveItems.Enqueue(instance);
                OnReturn(instance);
            }

            _isGrowing = false;
        }

#if UNITY_EDITOR
        public void UpdatePoolData(PoolVariantData variant)
        {
            UpdateCapacity(variant.Capacity);
            UpdateStatistics(variant.Statistics);
            UpdateHistogram(variant.Histogram);
        }

        private void UpdateCapacity(PoolCapacityData capacity)
        {
            capacity.InactiveCount = _inactiveItems.Count;
            capacity.ActiveCount = _activeItems.Count;
            capacity.TotalCount = _activeItems.Count + _inactiveItems.Count;
            if (capacity.TotalCount > 0)
            {
                capacity.UsagePercent = 100 * capacity.ActiveCount / capacity.TotalCount;
            }
        }

        private void UpdateStatistics(PoolStatisticsData statistics)
        {
            if (statistics.MaxActiveCount < _activeItems.Count)
            {
                statistics.MaxActiveCount = _activeItems.Count;
            }

            int totalCount = _activeItems.Count + _inactiveItems.Count;
            if (statistics.MaxTotalCount < totalCount)
            {
                statistics.MaxTotalCount = totalCount;
            }
        }

        private void UpdateHistogram(PoolHistogramData histogram)
        {
            histogram.Add(_activeItems.Count, Time.deltaTime);
        }
#endif
    }
}

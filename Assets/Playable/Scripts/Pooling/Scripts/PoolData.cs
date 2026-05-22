using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pooling
{
    [Serializable]
    public class PoolData
    {
        public string Name;
        public bool IsEnabled;
        public List<PoolVariantData> Variants;
    }

    [Serializable]
    public class PoolVariantData
    {
        public string Name;
        public PoolSetupData Setup;
        public PoolCapacityData Capacity = new PoolCapacityData();
        public PoolStatisticsData Statistics = new PoolStatisticsData();
        public PoolHistogramData Histogram = new PoolHistogramData();
    }

    [Serializable]
    public class PoolSetupData
    {
        public int InitialSize;
        public int Increment;
        public int GrowCap;
        public int PoolCap;
    }

    [Serializable]
    public class PoolCapacityData
    {
        public int InactiveCount;
        public int ActiveCount;
        public int TotalCount;
        public int UsagePercent;
    }

    [Serializable]
    public class PoolStatisticsData
    {
        public int MaxActiveCount;
        public int MaxTotalCount;
    }

    [Serializable]
    public class PoolHistogramData
    {
        private int _maxIndex = -1;
        private float _totalTime;
        private Dictionary<int, float> _usageTime = new Dictionary<int, float>();
        public List<HistogramEntry> Histogram = new List<HistogramEntry>();

        public void Add(int index, float time)
        {
            for (var i = _maxIndex + 1; i <= index; i++)
            {
                _usageTime[i] = 0;
                Histogram.Add(new HistogramEntry { ActiveCount = i });
            }

            if (_maxIndex < index)
            {
                _maxIndex = index;
            }

            _usageTime[index] += time;
            _totalTime += time;

            foreach (var entry in Histogram)
            {
                var usageTime = _usageTime[entry.ActiveCount];
                entry.UsagePercent = Mathf.RoundToInt(100f * usageTime / _totalTime);
            }
        }
    }

    [Serializable]
    public class HistogramEntry
    {
        public int ActiveCount;
        public int UsagePercent;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pooling
{
    public class ObjectPoolConfig : ScriptableObject
    {
        public List<VariantPoolConfig> Variants = new List<VariantPoolConfig>();
    }

    [Serializable]
    public class VariantPoolConfig
    {
        public string Variant;
        public GameObject Prefab;
        public int InitialSize;
        public int InitialPackSize;
        public int Increment;
        public int IncrementPackSize;
        public int GrowCap;
        public int PoolCap;
    }
}

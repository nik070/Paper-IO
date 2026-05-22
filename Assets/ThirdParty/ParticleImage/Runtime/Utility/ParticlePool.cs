using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AssetKits.ParticleImage
{
    public static class ParticlePool
    {
        private static ParticleData[] pool;
        private static Stack<int> availableIndices;

        private static bool isInitialized;
        private static int currentCapacity;

        public static void Initialize(int capacity)
        {
            if (isInitialized) return;

            if (capacity <= 0)
            {
                return;
            }

            pool = new ParticleData[capacity];
            availableIndices = new Stack<int>(capacity);
            currentCapacity = capacity;

            for (int i = capacity - 1; i >= 0; --i)
            {
                pool[i] = new ParticleData();
                availableIndices.Push(i);
            }

            isInitialized = true;
        }
        
        private static bool ExpandPool()
        {
            int doubledCapacity = currentCapacity * 2;
            int newCapacity = doubledCapacity;

            ParticleData[] newPool = new ParticleData[newCapacity];

            Array.Copy(pool, newPool, currentCapacity);

            int oldCapacity = currentCapacity;
            for (int i = oldCapacity; i < newCapacity; i++)
            {
                newPool[i] = new ParticleData();
                availableIndices.Push(i);
            }

            pool = newPool;
            currentCapacity = newCapacity;

            return true;
        }

        public static bool TryGetParticleIndex(out int index)
        {
            if (!isInitialized)
            {
                Initialize(512);
                index = -1;
                return false;
            }

            if (availableIndices.Count > 0)
            {
                index = availableIndices.Pop();
                return true;
            }

            if (ExpandPool())
            {
                if (availableIndices.Count > 0)
                {
                    index = availableIndices.Pop();
                    return true;
                }

                index = -1;
                return false;
            }
            
            index = -1;
            return false;
        }

        public static void ReturnParticleIndex(int index)
        {
            if (!isInitialized || index < 0 || index >= pool.Length) return;
            pool[index].Reset();
            availableIndices.Push(index);
        }

        public static ref ParticleData GetParticleRef(int index)
        {
            if (!isInitialized || index < 0 || index >= pool.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index), "Index is out of bounds for the particle pool.");
            }
            return ref pool[index];
        }
    }
}

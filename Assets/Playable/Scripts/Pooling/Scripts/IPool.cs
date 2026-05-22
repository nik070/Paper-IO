using System;

namespace Pooling
{
    public interface IPool : IDisposable
    {
        void Prewarm();

#if UNITY_EDITOR
        PoolData CreatePoolData();
        void UpdatePoolData(PoolData poolData);
#endif
    }

    public interface IPoolable
    {
        void CleanUp();
    }
}

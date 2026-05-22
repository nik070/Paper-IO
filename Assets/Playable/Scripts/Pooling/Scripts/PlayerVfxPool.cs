using Effects;
using UnityEngine;

namespace Pooling
{
    public class PlayerVfxPool : PoolablePool<VfxInstanceView>
    {
        [SerializeField] private PlayerVfxPoolConfig _config;

        protected override ObjectPoolConfig Config => _config;
    }
}

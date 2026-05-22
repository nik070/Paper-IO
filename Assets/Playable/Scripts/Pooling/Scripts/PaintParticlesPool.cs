using UnityEngine;

namespace Pooling
{
    public class PaintParticlesPool : ComponentPool<ParticleSystem>
    {
        [SerializeField] private PaintParticlesPoolConfig _config;

        protected override ObjectPoolConfig Config => _config;
    }
}

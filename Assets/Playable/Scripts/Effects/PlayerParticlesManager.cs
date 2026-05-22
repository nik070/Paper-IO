using Core;
using Pooling;
using UnityEngine;
using Utility;

namespace Effects
{
    public class PlayerParticlesManager : SingletonBehaviour<PlayerParticlesManager>
    {
        [SerializeField] private PlayerVfxPool _vfxPool;

        public void Init()
        {
            _vfxPool.Init();
        }

        public void PlayDeathParticles(Vector3 position, Color color, Color additiveColor)
        {
            if (_vfxPool == null)
            {
                return;
            }

            string variant = nameof(PlayerVfxType.Death);
            VfxInstanceView vfx = _vfxPool.Get(variant);

            vfx.transform.SetParent(transform);
            vfx.transform.position = position.WithZ(vfx.transform.position.z);
            vfx.SetColor(color);
            vfx.SetAdditiveColor(additiveColor);
            vfx.Play();

            DelayCall(vfx.Duration, () => Release(vfx, variant));
        }

        public void PlayCutTrailParticles(Vector3 position, Color color, Color additiveColor)
        {
            if (_vfxPool == null)
            {
                return;
            }

            string variant = nameof(PlayerVfxType.CutTrail);
            VfxInstanceView vfx = _vfxPool.Get(variant);

            vfx.transform.SetParent(transform);
            vfx.transform.position = position.WithZ(vfx.transform.position.z);
            vfx.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            vfx.SetColor(color);
            vfx.SetAdditiveColor(additiveColor);

            vfx.Play();
            DelayCall(vfx.Duration, () => Release(vfx, variant));
        }

        public void PlayKillPaintCircleParticles(Vector3 position, float radius, Color color)
        {
            if (_vfxPool == null)
            {
                return;
            }

            string variant = nameof(PlayerVfxType.KillPaintCircle);
            VfxInstanceView vfx = _vfxPool.Get(variant);

            vfx.transform.SetParent(transform);
            vfx.transform.position = position.WithZ(vfx.transform.position.z);
            vfx.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            vfx.transform.localScale = 0.28f * radius * Vector3.one;
            vfx.SetColor(color);
            vfx.Play();

            DelayCall(vfx.Duration, () => Release(vfx, variant));
        }

        private void Release(VfxInstanceView vfx, string variant)
        {
            if (vfx == null)
            {
                return;
            }

            if (_vfxPool != null && _vfxPool.IsDisposed == false)
            {
                _vfxPool.Return(vfx, variant);
            }
            else
            {
                Destroy(vfx.gameObject);
            }
        }
    }

    public enum PlayerVfxType : int
    {
        Death = 0,
        CutTrail = 1,
        KillPaintCircle = 2,
        DissolvedTrail = 3,
        Dying = 4
    }
}

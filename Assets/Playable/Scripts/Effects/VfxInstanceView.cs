using System.Collections.Generic;
using Ara;
using DG.Tweening;
using Pooling;
using UnityEngine;

namespace Effects
{
    public enum ColorType
    {
        ZoneTextureColor,
        TrailColor
    }

    public class VfxInstanceView : MonoBehaviour, IPoolable
    {
        private static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
        private static readonly int StencilReadMask = Shader.PropertyToID("_StencilReadMask");
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");

        private readonly List<Material> _cachedCutMaterials = new List<Material>();
        private readonly List<Material> _cachedZoneMaskedMat = new List<Material>();

        [SerializeField] private bool _disableGameObjectOnEmissionDisabled;
        [SerializeField] private ParticleSystem _topLevelParticleSystem;
        [SerializeField] private List<MeshRenderer> _meshRenderers;
        [SerializeField] private List<SpriteRenderer> _spriteRenderers;
        [SerializeField] private List<ParticleSystem> _particleSystems;
        [SerializeField] private List<ParticleSystem> _particleSystemsNotRecolorable;
        [SerializeField] private List<TrailRenderer> _lineRenderers;
        [SerializeField] private List<ParticleSystem> _particleSystemsAdditive;
        [SerializeField] private List<TrailRenderer> _trailRenderersAdditive;
        [SerializeField] private ColorType _colorType = ColorType.ZoneTextureColor;
        [SerializeField] private float _fadeOutDuration = 5f;

        [Header("Ara Trails")]
        [SerializeField] private List<AraTrail> _araTrails;

        [Header("Stencil Related Fields")]
        [Tooltip("Needs a shader with stencil support, Comp = NotEqual")] [SerializeField]
        private List<Renderer> _renderersCutByTrail;

        [Tooltip("Needs a shader with stencil support, Comp = Equal")] [SerializeField]
        private List<Renderer> _renderersMaskedByMyZone;

        [SerializeField] private List<AraTrail> _araTrailsMaskedByMyZone;

        [Tooltip("Needs a shader with stencil support, Comp = Equal")] [SerializeField]
        private List<Renderer> _renderersMaskedByOppoZone;

        [SerializeField] private List<AraTrail> _araTrailsMaskedByOppoZone;
        public float Duration => _topLevelParticleSystem.main.duration;

        private ParticleSystem[] _particleSystemsInHierarchy;

        private void Awake()
        {
            _particleSystemsInHierarchy = GetComponentsInChildren<ParticleSystem>(true);
            if (_particleSystemsInHierarchy == null)
            {
                _particleSystemsInHierarchy = new ParticleSystem[0];
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in _cachedCutMaterials)
            {
                Destroy(material);
            }

            foreach (Material material in _cachedZoneMaskedMat)
            {
                Destroy(material);
            }
        }

        public void CleanUp()
        {
            transform.DOKill();
            CleanupAraTrails();
        }

        public float AdaptSimulationSpeed(float desiredDuration)
        {
            float newSpeed = Duration / desiredDuration;

            foreach (ParticleSystem theSystem in _particleSystems)
            {
                ParticleSystem.MainModule main = theSystem.main;
                main.simulationSpeed = newSpeed;
            }

            return newSpeed;
        }

        public void CleanupAraTrails()
        {
            // Ara Trails are not automatically cleaned up and it's possible for some of the
            // points to still be valid when the trail is used again so we will explicitly
            // clean it up
            for (int i = 0; i < _araTrails.Count; i++)
            {
                _araTrails[i].Clear();
            }
        }

        public void FadeOutAndDestroy()
        {
            foreach (SpriteRenderer x in _spriteRenderers)
            {
                x.DOFade(0, _fadeOutDuration);
            }

            SetEmissionEnabled(false);
            _topLevelParticleSystem.transform.SetParent(null);
            _topLevelParticleSystem.transform.DOScale(0, _fadeOutDuration)
                .OnComplete(() => Destroy(_topLevelParticleSystem.gameObject));
        }

        public void Play()
        {
            // Activate first: callers like VfxController keep one-shot FX (Spawn / Closing) inactive
            // between plays so their ParticleSystems don't tick while idle. Activating here means
            // every Play() works regardless of whether the caller already toggled active state.
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // Nested PS are not always listed on _particleSystems; Luna kept child emission on while idle.
            SyncHierarchyParticleEmission(true, false);
            _topLevelParticleSystem.Play();
        }

        /// <summary>
        ///     Created of an A/B of testing dark playgrounds.
        ///     The VFX prefabs we have replaced will have renderers assigned in the additive lists
        /// </summary>
        /// <param name="additiveColor"></param>
        public void SetAdditiveColor(Color additiveColor)
        {
            VFXUtilities.SetParticlesColor(_particleSystemsAdditive, additiveColor);
            VFXUtilities.SetTrailRenderersColor(_trailRenderersAdditive, additiveColor);
        }


        public void SetColor(Color zoneTextureColor, Color trailColor = default)
        {
            Color color = _colorType == ColorType.TrailColor && trailColor != default
                ? trailColor
                : zoneTextureColor;

            _meshRenderers.ForEach(x => x.material.color = color.With(a: x.material.color.a));
            _spriteRenderers.ForEach(x => x.color = color.With(a: x.color.a));

            VFXUtilities.SetParticlesColor(_particleSystems, color);
            VFXUtilities.SetTrailRenderersColor(_lineRenderers, color);
            VFXUtilities.SetAraTrailRenderersColor(_araTrailsMaskedByMyZone, color);
            VFXUtilities.SetAraTrailRenderersColor(_araTrailsMaskedByOppoZone, color);
        }

        public void SetEmissionEnabled(bool isEnabled, bool instantly = false)
        {
            if (_disableGameObjectOnEmissionDisabled)
            {
                gameObject.SetActive(isEnabled);
            }

            SyncHierarchyParticleEmission(isEnabled, instantly);

            // WipePhantomParticles leaves PS in Stopped state — restart the top-level PS so
            // emission.enabled = true actually emits.
            if (isEnabled && _topLevelParticleSystem != null && !_topLevelParticleSystem.isPlaying)
            {
                _topLevelParticleSystem.Play(true);
            }

            foreach (TrailRenderer x in _lineRenderers)
            {
                x.enabled = isEnabled;

                if (!isEnabled)
                {
                    x.Clear();
                }
            }

            foreach (TrailRenderer x in _trailRenderersAdditive)
            {
                x.enabled = isEnabled;

                if (!isEnabled)
                {
                    x.Clear();
                }
            }

            foreach (AraTrail x in _araTrails)
            {
                x.enabled = isEnabled;
                if (!isEnabled)
                {
                    x.Clear();
                }
            }

            _meshRenderers.ForEach(x => x.enabled = isEnabled);
            _spriteRenderers.ForEach(x => x.enabled = isEnabled);
        }

        public void SetScaleFactor(float scaleFactor)
        {
            _topLevelParticleSystem.transform.localScale = Vector3.one * scaleFactor;
        }

        public void SetSimulationSpeed(float simulationSpeed)
        {
            ParticleSystem.MainModule mainModule = _topLevelParticleSystem.main;
            mainModule.simulationSpeed = simulationSpeed;
        }

        public void SetStencilRef(int id)
        {
            if (_cachedCutMaterials.Count == 0)
            {
                foreach (Renderer rendererCutByTrail in _renderersCutByTrail)
                {
                    _cachedCutMaterials.Add(rendererCutByTrail.material);
                }
            }

            foreach (Material material in _cachedCutMaterials)
            {
                material.SetFloat(StencilRef, id);
            }

            #region Materials masked by zones

            // Since we're trying to mess with stencil we can't use MaterialPropertyBlocks
            if (_cachedZoneMaskedMat.Count == 0)
            {
                foreach (Renderer rendererMaskedByMyZone in _renderersMaskedByMyZone)
                {
                    _cachedZoneMaskedMat.Add(rendererMaskedByMyZone.material);
                }

                foreach (AraTrail araTrailMaskedByMyZone in _araTrailsMaskedByMyZone)
                {
                    Material araMaterial = araTrailMaskedByMyZone.materials[0];
                    Material instancedMaterial = Instantiate(araMaterial);
                    araTrailMaskedByMyZone.materials[0] = instancedMaterial;
                    _cachedZoneMaskedMat.Add(instancedMaterial);
                }

                foreach (Renderer rendererMaskedByOppoZone in _renderersMaskedByOppoZone)
                {
                    _cachedZoneMaskedMat.Add(rendererMaskedByOppoZone.material);
                }

                foreach (AraTrail araTrailMaskedByOppoZone in _araTrailsMaskedByOppoZone)
                {
                    Material araMaterial = araTrailMaskedByOppoZone.materials[0];
                    Material instancedMaterial = Instantiate(araMaterial);
                    araTrailMaskedByOppoZone.materials[0] = instancedMaterial;
                    _cachedZoneMaskedMat.Add(instancedMaterial);
                }
            }

            // Apply a mask to the id in order to leave only the last 5 bits
            // which are the ones in which the player id is stored
            int playerIdRef = id & 0b00011111;
            // Make sure that the bit 5 is turned on so that the effect is only visible
            // on the player zone and not his trail
            int zoneIdRef = playerIdRef | 0b00100000;
            foreach (Renderer render in _renderersMaskedByMyZone)
            {
                render.material.SetFloat(StencilRef, zoneIdRef);
                // 63 here means that we're looking at the first 6 bits: 5 for the id and 1 to check if it's indeed the zone
                render.material.SetFloat(StencilReadMask, 63);
                // 3 means Comp Equal: we're looking for a specific stencil to draw this which is the zoneIdRef
                render.material.SetFloat(StencilComp, 3);
            }

            foreach (AraTrail araTrail in _araTrailsMaskedByMyZone)
            {
                araTrail.materials[0].SetFloat(StencilRef, zoneIdRef);
                // 63 here means that we're looking at the first 6 bits: 5 for the id and 1 to check if it's indeed the zone
                araTrail.materials[0].SetFloat(StencilReadMask, 63);
                // 3 means Comp Equal: we're looking for a specific stencil to draw this which is the zoneIdRef
                araTrail.materials[0].SetFloat(StencilComp, 3);
            }

            zoneIdRef = id | 0b01000000;
            foreach (Renderer render in _renderersMaskedByOppoZone)
            {
                render.material.SetFloat(StencilRef, zoneIdRef);
            }

            foreach (AraTrail araTrail in _araTrailsMaskedByOppoZone)
            {
                araTrail.materials[0].SetFloat(StencilRef, zoneIdRef);
            }

            #endregion
        }

        public void Stop()
        {
            _topLevelParticleSystem.Stop();
            foreach (TrailRenderer lineRenderer in _lineRenderers)
            {
                lineRenderer.Clear();
            }

            CleanupAraTrails();
        }

        private void SyncHierarchyParticleEmission(bool isEnabled, bool instantly)
        {
            if (_particleSystemsInHierarchy == null)
            {
                _particleSystemsInHierarchy = GetComponentsInChildren<ParticleSystem>(true);
                if (_particleSystemsInHierarchy == null)
                {
                    _particleSystemsInHierarchy = new ParticleSystem[0];
                }
            }

            for (int i = 0; i < _particleSystemsInHierarchy.Length; i++)
            {
                ToggleEmission(_particleSystemsInHierarchy[i], isEnabled, instantly);
            }
        }

        private void ToggleEmission(ParticleSystem x, bool isEnabled, bool instantly)
        {
            ParticleSystem.EmissionModule emission = x.emission;
            emission.enabled = isEnabled;

            if (instantly && !isEnabled)
            {
                x.Clear();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using DG.Tweening;
using Gameplay;
using UnityEngine;

namespace Effects
{
    [Serializable]
    public class ZoneTransition
    {
        public object Key;
        public bool IsFriendly;
        public Color Color;

        public ZoneTransition(object key, bool isFriendly, Color color)
        {
            Key = key;
            IsFriendly = isFriendly;
            Color = color;
        }
    }

    public class VfxController : MonoBehaviour
    {
        private bool ClosingZoneFxsEnabled { get; set; }

        private VfxInstanceView InsideEnemyZoneFx
        {
            get
            {
                if (!_insideEnemyZoneFx)
                {
                    _insideEnemyZoneFx = InstantiateAtParentWorld(_insideEnemyZoneFxPrefab, transform);
                }

                return _insideEnemyZoneFx;
            }
        }

        private VfxInstanceView InsideFriendlyZoneFx
        {
            get
            {
                if (!_insideOwnZoneFx)
                {
                    _insideOwnZoneFx = InstantiateAtParentWorld(_insideFriendlyZoneFxPrefab, transform);
                }

                return _insideOwnZoneFx;
            }
        }

        private VfxInstanceView OutsideZoneFx
        {
            get
            {
                if (!_outsideZoneFx && _outsideZoneFxPrefab)
                {
                    _outsideZoneFx = InstantiateAtParentWorld(_outsideZoneFxPrefab, transform);
                }

                return _outsideZoneFx;
            }
        }

        private VfxInstanceView TinyClosingZoneFx
        {
            get
            {
                if (!_closingZoneTinyFx)
                {
                    _closingZoneTinyFx = InstantiateAtParentWorld(_closingZoneTinyFxPrefab, _rotatedFxsParent);
                }

                return _closingZoneTinyFx;
            }
        }

        private VfxInstanceView SmallClosingZoneFx
        {
            get
            {
                if (!_closingZoneSmallFx)
                {
                    _closingZoneSmallFx = InstantiateAtParentWorld(_closingZoneSmallFxPrefab, _rotatedFxsParent);
                }

                return _closingZoneSmallFx;
            }
        }

        private VfxInstanceView MediumClosingZoneFx
        {
            get
            {
                if (!_closingZoneMediumFx)
                {
                    _closingZoneMediumFx = InstantiateAtParentWorld(_closingZoneMediumFxPrefab, _rotatedFxsParent);
                }

                return _closingZoneMediumFx;
            }
        }

        private VfxInstanceView BigClosingZoneFx
        {
            get
            {
                if (!_closingZoneBigFx)
                {
                    _closingZoneBigFx = InstantiateAtParentWorld(_closingZoneBigFxPrefab, _rotatedFxsParent);
                }

                return _closingZoneBigFx;
            }
        }

        private VfxInstanceView SpawnFx
        {
            get
            {
                if (!_spawnFX && _spawnFxPrefab)
                {
                    _spawnFX = InstantiateAtParentWorld(_spawnFxPrefab, transform.parent);
                }

                return _spawnFX;
            }
        }

        // Luna/WebGL defers world-matrix propagation when reparenting on Instantiate, so a child
        // PS with playOnAwake + simulationSpace=World emits its first batch at world (0,0,0) and
        // those world-space particles persist for their lifetime ("smoke at map center under
        // bots"). Pinning world position via the explicit overload + wiping any phantom particles
        // emitted before the matrix settles avoids the leak.
        //
        // Deferred-active: every spawned VFX starts inactive so its ParticleSystems and
        // ParticleSystemRenderers don't tick / submit draws while idle. They are reactivated by
        // SetEmissionEnabled(true) (with _disableGameObjectOnEmissionDisabled=1) for the looping
        // zone FX, or by VfxInstanceView.Play() for one-shot FX (Spawn / Closing variants).
        private VfxInstanceView InstantiateAtParentWorld(VfxInstanceView prefab, Transform parent)
        {
            Vector3 worldPos = parent != null ? parent.position : Vector3.zero;
            Quaternion worldRot = prefab != null ? prefab.transform.rotation : Quaternion.identity;
            VfxInstanceView instance = Instantiate(prefab, worldPos, worldRot, parent);
            instance.transform.localPosition = prefab.transform.position;
            instance.gameObject.SetActive(false);
            return instance;
        }

        private bool OutsideZoneFxEnabled => _outsideZoneFxPrefab != null;

        private PlayerParticlesManager PlayerParticlesManager => PlayerParticlesManager.Instance;

        [Header("Data")]
        private List<ZoneTransition> _zoneTransitions = new List<ZoneTransition>();

        private bool _isPaused;
        private SkinView _skinView;
        private SkinConfig _skinConfig;

        private VfxInstanceView _insideEnemyZoneFx;
        private VfxInstanceView _insideOwnZoneFx;
        private VfxInstanceView _outsideZoneFx;
        private VfxInstanceView _closingZoneTinyFx;
        private VfxInstanceView _closingZoneSmallFx;
        private VfxInstanceView _closingZoneMediumFx;
        private VfxInstanceView _closingZoneBigFx;
        private VfxInstanceView _spawnFX;

        [Header("Internal Components")]
        [SerializeField] private GameObject _newVfxParent;

        [SerializeField] private SpriteRenderer _trailHeadCover;

        [Header("External Components")]
        [SerializeField] private Transform _rotatedFxsParent;

        [Header("Zone VFX Prefabs")]
        [SerializeField] private VfxInstanceView _insideEnemyZoneFxPrefab;

        [SerializeField] private VfxInstanceView _insideFriendlyZoneFxPrefab;

        [Header("Outside Zone VFX (neutral territory)")]
        [SerializeField] private VfxInstanceView _outsideZoneFxPrefab;

        [Header("Closing Zone VFX Prefabs")]
        [SerializeField] private VfxInstanceView _closingZoneTinyFxPrefab;

        [SerializeField] private VfxInstanceView _closingZoneSmallFxPrefab;
        [SerializeField] private VfxInstanceView _closingZoneMediumFxPrefab;
        [SerializeField] private VfxInstanceView _closingZoneBigFxPrefab;

        [Header("Spawn VFX")]
        [SerializeField] private VfxInstanceView _spawnFxPrefab;

        public void InitializeVfx(SkinView skinView, SkinConfig skinConfig, bool isLocalPlayer)
        {
            _skinView = skinView;
            _skinConfig = skinConfig;
            ClosingZoneFxsEnabled = isLocalPlayer;
            SetupZoneFXs();

            _newVfxParent.SetActive(true);
            InsideEnemyZoneFx.SetEmissionEnabled(false, true);
            InsideFriendlyZoneFx.SetEmissionEnabled(false, true);

            ApplyColor(skinConfig.ZoneTextureColor, skinConfig.TrailColor, skinConfig.VfxAdditiveSkinColor);

            if (OutsideZoneFxEnabled)
            {
                OutsideZoneFx.SetEmissionEnabled(false, true);
            }
        }

        public void OnZoneEnter(CharacterArea zone, bool isFriendly, Color color)
        {
            if (_zoneTransitions.Find(x => x.Key.Equals(zone)) != null)
            {
                return;
            }

            var transitionModel = new ZoneTransition(zone, isFriendly, color);
            _zoneTransitions.Add(transitionModel);

            UpdateMovingThroughZoneFx();
        }

        public void OnZoneExit(CharacterArea zone)
        {
            if (_zoneTransitions.RemoveAll(x => x.Key.Equals(zone)) <= 0)
            {
                Debug.LogWarning($"OnZoneExit called, but no transition found for the given zone: {zone}");
            }

            UpdateMovingThroughZoneFx();
        }

        public void PlayDeath()
        {
            Vector3 localPosition = _skinView.transform.localPosition;
            Quaternion localRotation = _skinView.transform.localRotation;
            Vector3 localScale = _skinView.transform.localScale;
            Transform skinParent = _skinView.transform.parent;
            _skinView.transform.SetParent(null);
            _skinView.transform
                .DOScale(transform.localScale.x * 1.5f, 0.1f)
                .OnComplete(() =>
                {
                    _skinView.transform.SetParent(skinParent, false);
                    _skinView.transform.localPosition = localPosition;
                    _skinView.transform.localRotation = localRotation;
                    _skinView.transform.localScale = localScale;
                });

            PlayerParticlesManager.PlayDeathParticles(transform.position, _skinConfig.CharacterColor, _skinConfig.VfxAdditiveSkinColor);

            _zoneTransitions.Clear();
            UpdateMovingThroughZoneFx();
        }

        public void PlayHitCircle(Vector3 position, float radius, Color color)
        {
            PlayerParticlesManager.PlayKillPaintCircleParticles(position, radius, color);
        }

        public void PlayLoopVFX(int id)
        {
            if (!ClosingZoneFxsEnabled)
            {
                return;
            }

            switch (id)
            {
                case 0:
                    SmallClosingZoneFx.Play();
                    break;
                case 1:
                    MediumClosingZoneFx.Play();
                    break;
                case 2:
                    BigClosingZoneFx.Play();
                    break;
                default:
                    TinyClosingZoneFx.Play();
                    break;
            }
        }

        public void PlaySpawn()
        {
            if (!SpawnFx)
            {
                return;
            }

            SpawnFx.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            SpawnFx.gameObject.SetActive(true);
            SpawnFx.SetColor(_skinConfig.ZoneTextureColor, _skinConfig.TrailColor);
            SpawnFx.SetAdditiveColor(_skinConfig.VfxAdditiveSkinColor);
            SpawnFx.Play();
        }

        public void ResetSkin()
        {
            //_skinView.ResetSkinSize();
        }

        public void ResetVfx()
        {
            InsideEnemyZoneFx.SetEmissionEnabled(false);
            InsideFriendlyZoneFx.SetEmissionEnabled(false);

            if (OutsideZoneFxEnabled && OutsideZoneFx)
            {
                OutsideZoneFx.SetEmissionEnabled(false);
            }

            _zoneTransitions.Clear();
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;

            UpdateMovingThroughZoneFx();
        }

        public void SetStencilRef(int id)
        {
            InsideFriendlyZoneFx.SetStencilRef(id);
            InsideEnemyZoneFx.SetStencilRef(id);
        }

        private void ApplyColor(Color zoneTextureColor, Color trailColor, Color additiveColor)
        {
            if (OutsideZoneFxEnabled && OutsideZoneFx)
            {
                OutsideZoneFx.SetColor(zoneTextureColor, trailColor);
                OutsideZoneFx.SetAdditiveColor(additiveColor);
            }

            if (ClosingZoneFxsEnabled)
            {
                SmallClosingZoneFx.SetColor(zoneTextureColor, trailColor);
                MediumClosingZoneFx.SetColor(zoneTextureColor, trailColor);
                BigClosingZoneFx.SetColor(zoneTextureColor, trailColor);
                TinyClosingZoneFx.SetColor(zoneTextureColor, trailColor);

                SmallClosingZoneFx.SetAdditiveColor(additiveColor);
                MediumClosingZoneFx.SetAdditiveColor(additiveColor);
                BigClosingZoneFx.SetAdditiveColor(additiveColor);
                TinyClosingZoneFx.SetAdditiveColor(additiveColor);
            }
        }

        private void SetTrailHeadCoverActive(bool isActive, Color color = default)
        {
            _trailHeadCover.gameObject.SetActive(isActive);

            if (color == default)
            {
                return;
            }

            _trailHeadCover.color = color.With(a: 1f);
        }

        private void SetupZoneFXs()
        {
            _ = InsideFriendlyZoneFx;
            _ = InsideEnemyZoneFx;

            if (OutsideZoneFxEnabled)
            {
                _ = OutsideZoneFx;
            }

            if (ClosingZoneFxsEnabled)
            {
                _ = BigClosingZoneFx;
                _ = MediumClosingZoneFx;
                _ = SmallClosingZoneFx;
                _ = TinyClosingZoneFx;
            }
        }

        private void UpdateMovingThroughZoneFx()
        {
            if (_zoneTransitions.Count > 0)
            {
                ZoneTransition lastTransition = _zoneTransitions[^1];

                if (OutsideZoneFxEnabled && OutsideZoneFx)
                {
                    OutsideZoneFx.SetEmissionEnabled(false);
                }

                InsideEnemyZoneFx.SetEmissionEnabled(!lastTransition.IsFriendly && !_isPaused);
                InsideFriendlyZoneFx.SetEmissionEnabled(lastTransition.IsFriendly && !_isPaused);

                Color finalColor = lastTransition.Color.With(a: 0.5f);

                if (lastTransition.IsFriendly)
                {
                    InsideFriendlyZoneFx.SetColor(finalColor);
                }
                else
                {
                    InsideEnemyZoneFx.SetColor(finalColor);
                }
            }
            else
            {
                if (OutsideZoneFxEnabled && OutsideZoneFx)
                {
                    OutsideZoneFx.SetEmissionEnabled(!_isPaused);
                }

                InsideEnemyZoneFx.SetEmissionEnabled(false);
                InsideFriendlyZoneFx.SetEmissionEnabled(false);
            }
        }

        private VfxInstanceView GetInstanceView(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            return prefab.GetComponent<VfxInstanceView>();
        }
    }
}

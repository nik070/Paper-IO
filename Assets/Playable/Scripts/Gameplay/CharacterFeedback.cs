using Core;
using DG.Tweening;
using Effects;
using Mechanics;
using Paper2.PaintParticles;
using Pooling;
using UnityEngine;
using Utility;

namespace Gameplay
{
    public class CharacterFeedback : MonoBehaviour
    {
        private const float CaptureTinyThreshold = 1f;
        private const float CaptureSmallThreshold = 28f;
        private const float CaptureBigThreshold = 450f;
        private const float SpawnDelay = 0.5f;
        private const float SpawnVisualStartZ = -15f;
        private const float SpawnVisualEndZ = 0f;
        private const float ShadowAnimationDuration = 0.25f;
        private const float KillPaintScaleFactor = 0.28f;
        private const float PaintParticlesCountRatio = 0.4f;

        private static readonly int ZoneMediumAnimation = Animator.StringToHash("ZoneMed");
        private static readonly int ZoneBigAnimation = Animator.StringToHash("ZoneBig");
        private static readonly int ShadowBigJumpTrigger = Animator.StringToHash("BigJump");
        private static readonly int ShadowMidJumpTrigger = Animator.StringToHash("MidJump");

        private Character _character;
        private SkinConfig _skin;
        private CharacterArea _area;
        private SkinView _visual;
        private Animator _shadowAnimator;
        private Transform _shadowSurface;
        private Tween _spawnDelayedCall;
        private Sequence _spawnSequence;
        private Tween _shadowTween;
        private Tween _pauseReleaseCall;
        private Coroutine _paintParticlesRoutine;
        private float _shadowBaseScale;
        private bool _gameplayShadowVisible;
        private bool _isInitialized;

        [SerializeField] private VfxController _vfxController;
        [SerializeField] private Animator _skinAnimator;
        [SerializeField] private GameplayShadowView _gameplayShadow;
        [SerializeField] private PaintParticlesManager _paintParticlesManager;

        private void OnDestroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            GameEvents.OnTrailCut -= HandleTrailCut;
            GameEvents.OnCharacterDied -= HandleCharacterDied;
            GameEvents.OnTerritoryCaptured -= HandleTerritoryCaptured;
            GameEvents.OnCharacterEnteredZone -= HandleCharacterEnteredZone;
            GameEvents.OnCharacterExitedZone -= HandleCharacterExitedZone;

            if (_spawnDelayedCall != null)
            {
                _spawnDelayedCall.Kill();
                _spawnDelayedCall = null;
            }

            if (_spawnSequence != null)
            {
                _spawnSequence.Kill();
                _spawnSequence = null;
            }

            if (_shadowTween != null)
            {
                _shadowTween.Kill();
                _shadowTween = null;
            }

            if (_pauseReleaseCall != null)
            {
                _pauseReleaseCall.Kill();
                _pauseReleaseCall = null;
            }

            if (_paintParticlesRoutine != null)
            {
                StopCoroutine(_paintParticlesRoutine);
                _paintParticlesRoutine = null;
            }

            _visual.SkinRoot.DOKill();
        }

        public void Init(Character character, SkinConfig skin, CharacterArea area, SkinView visual, PaintParticlesPool paintParticlesPool)
        {
            if (_isInitialized)
            {
                return;
            }

            _character = character;
            _skin = skin;
            _area = area;
            _visual = visual;
            _shadowBaseScale = Mathf.Max(0.8f, skin.ShadowSize);

            SetupGameplayShadow();
            InitializeVfxController();
            if (_character.IsPlayer)
            {
                InitializePaintParticlesManager(paintParticlesPool);
            }

            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnTrailCut += HandleTrailCut;
            GameEvents.OnCharacterDied += HandleCharacterDied;
            GameEvents.OnTerritoryCaptured += HandleTerritoryCaptured;
            GameEvents.OnCharacterEnteredZone += HandleCharacterEnteredZone;
            GameEvents.OnCharacterExitedZone += HandleCharacterExitedZone;

            _isInitialized = true;
            StartSpawnAnimation();
        }

        private void InitializePaintParticlesManager(PaintParticlesPool paintParticlesPool)
        {
            _paintParticlesManager.Init(_skin.ZoneTextureColor, paintParticlesPool);
        }

        private void InitializeVfxController()
        {
            _vfxController.InitializeVfx(_visual, _skin, _character.IsPlayer);
            _vfxController.SetStencilRef(_area.StencilID);
        }

        private void SetupGameplayShadow()
        {
            _gameplayShadow.Setup(_skin.ShadowSize);
            _gameplayShadow.Show(true, false);
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Tutorial:
                case GameState.Playing:
                    // CollisionManager seeds occupancy via its own OnGameStateChanged subscription
                    // and fires OnCharacterEnteredZone for everyone inside a zone — no local work.
                    break;

                default:
                    ResetVfxState();
                    break;
            }
        }

        private void HandleCharacterExitedZone(Character mover, Character zoneOwner)
        {
            if (!_isInitialized || mover != _character || zoneOwner == null || zoneOwner._area == null)
            {
                return;
            }

            if (zoneOwner != _character && _character.IsPlayer)
            {
                AudioManager.Instance.Stop(AudioClips.PlayerEat);
            }

            _vfxController.OnZoneExit(zoneOwner._area);
        }

        private void HandleCharacterEnteredZone(Character mover, Character zoneOwner)
        {
            if (!_isInitialized || mover != _character || zoneOwner == null || zoneOwner._area == null)
            {
                return;
            }
            bool isFriendly = zoneOwner == _character;

            if (!isFriendly && _character.IsPlayer)
            {
                AudioManager.Instance.Play(AudioClips.PlayerEat);
            }

            Color color = isFriendly
                ? _skin.ZoneTextureColor
                : (zoneOwner.Skin != null ? zoneOwner.Skin.ZoneTextureColor : _skin.ZoneTextureColor);

            _vfxController.OnZoneEnter(zoneOwner._area, isFriendly, color);
        }

        private void HandleTrailCut(Character killer, Character victim, Vector3 hitPosition)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (victim == _character)
            {
                PlayerParticlesManager.Instance.PlayCutTrailParticles(hitPosition, _skin.TrailColor, _skin.VfxAdditiveSkinColor);
            }
        }

        private void HandleCharacterDied(DeathInfo deathInfo)
        {
            if (_character.IsPlayer)
            {
                if (deathInfo.Victim == _character)
                {
                    AudioManager.Instance.StopAllSounds();
                    AudioManager.Instance.Play(AudioClips.PlayerDie);
                }
                else if (deathInfo.Killer == _character)
                {
                    AudioManager.Instance.Play(AudioClips.Kill);
                }
            }

            if (!_isInitialized || deathInfo.Victim != _character)
            {
                return;
            }

            if (_spawnDelayedCall != null)
            {
                _spawnDelayedCall.Kill();
                _spawnDelayedCall = null;
            }

            if (_spawnSequence != null)
            {
                _spawnSequence.Kill();
                _spawnSequence = null;
            }

            ResetVfxState();

            _vfxController.PlayDeath();
            ShowGameplayShadow(false, false);
        }

        private void HandleTerritoryCaptured(Character capturer, float playerFillPct, float capturedArea)
        {
            if (!_isInitialized || capturer != _character)
            {
                return;
            }

            PlayLoopClosingAnimations(capturedArea);
            PlayZonePaintParticles();

            if (capturer.IsPlayer)
            {
                AudioManager.Instance.Play(AudioClips.ZoneCaptured);
            }

            /*if (_paintParticlesRoutine != null)
            {
                StopCoroutine(_paintParticlesRoutine);
            }

            _paintParticlesRoutine = StartCoroutine(PlayZonePaintParticlesRoutine());*/
        }

        private void StartSpawnAnimation(bool isImmediate = true)
        {
            if (isImmediate)
            {
                _visual.SkinRoot.gameObject.SetActive(true);
                ShowGameplayShadow(true, false);
            }
            else
            {
                _visual.SkinRoot.gameObject.SetActive(false);
                ShowGameplayShadow(false, false);
                _spawnDelayedCall = DOVirtual.DelayedCall(SpawnDelay, PlaySpawnAnimation);
            }
        }

        private void PlaySpawnAnimation()
        {
            _shadowSurface.localScale = new Vector3(1f, 0.1f, 1f);
            _shadowTween = _shadowSurface.DOScale(new Vector3(1.5f, 0.1f, 1.5f), 0.15f).SetEase(Ease.Linear);

            _visual.SkinRoot.gameObject.SetActive(true);
            _visual.SkinRoot.localPosition = new Vector3(0f, 0f, SpawnVisualStartZ);
            _visual.SkinRoot.localScale = Vector3.one;

            _vfxController.PlaySpawn();
            _spawnSequence = DOTween.Sequence();
            _spawnSequence.Append(_visual.SkinRoot.DOLocalMoveZ(SpawnVisualEndZ, 0.25f));
            _spawnSequence.AppendCallback(() => { ShowGameplayShadow(false, false); });
            _spawnSequence.Append(_visual.SkinRoot.DOScale(Vector3.one * 1.2f, 0.1f));
            _spawnSequence.Append(_visual.SkinRoot.DOScale(new Vector3(0.8f, 0.8f, 2f), 0.1f));
            _spawnSequence.Append(_visual.SkinRoot.DOScale(Vector3.one, 0.1f));
            _spawnSequence.AppendCallback(() => { ShowGameplayShadow(true, false); });
        }

        private void PlayLoopClosingAnimations(float capturedArea)
        {
            if (capturedArea <= CaptureTinyThreshold)
            {
                _vfxController.PlayLoopVFX(-1);
                return;
            }

            if (capturedArea <= CaptureSmallThreshold)
            {
                _vfxController.PlayLoopVFX(0);
                return;
            }

            if (capturedArea > CaptureBigThreshold)
            {
                _vfxController.SetPaused(true);
                if (_pauseReleaseCall != null)
                {
                    _pauseReleaseCall.Kill();
                }

                _pauseReleaseCall = DOVirtual.DelayedCall(1f, () => { _vfxController.SetPaused(false); });
                TriggerSkinAndShadowJump(true);
                _vfxController.PlayLoopVFX(2);
                return;
            }

            TriggerSkinAndShadowJump(false);
            _vfxController.PlayLoopVFX(1);
        }

        private void TriggerSkinAndShadowJump(bool isBig)
        {
            _skinAnimator.SetTrigger(isBig ? ZoneBigAnimation : ZoneMediumAnimation);
            if (_gameplayShadowVisible)
            {
                _shadowAnimator.SetTrigger(isBig ? ShadowBigJumpTrigger : ShadowMidJumpTrigger);
            }
        }

        private void PlayZonePaintParticles()
        {
            if (!_character.IsPlayer || _area == null)
            {
                return;
            }

            // Spawn around the JUST-captured polygon, not the cumulative territory.
            Bounds bounds = _area.CreatedTerritoryBounds.ToBounds(transform.position.z);
            if (bounds.size.sqrMagnitude < 12f)
            {
                return;
            }

            Vector3 meshCenter = bounds.center;
            _createdMeshCenter = meshCenter;
            float radius = (meshCenter - _character.transform.position).magnitude;
            radius = Mathf.Max(radius, 2f) * 1f;

            // Small -Z offset so particles render in front of the territory mesh (which
            // sits at z=0..0.59 during the spawn animation). Camera is orthographic so the
            // 1-unit offset doesn't change visual scale, only depth-sort order.
            _paintParticlesManager.PlayZoneParticlesCircle(meshCenter + Vector3.back * 1f, radius);
        }

        private Vector3 _createdMeshCenter;

        private void OnDrawGizmos()
        {
            if (Application.isPlaying && _character.IsPlayer)
            {
                Gizmos.DrawSphere(_createdMeshCenter, 1f);
            }
        }

        private void ShowGameplayShadow(bool state, bool animate)
        {
            if (_shadowSurface == null)
            {
                return;
            }

            _gameplayShadowVisible = state;

            if (_shadowTween != null)
            {
                _shadowTween.Kill();
                _shadowTween = null;
            }

            Vector3 targetScale = Vector3.one * (state ? _shadowBaseScale : 0f);
            if (!animate)
            {
                _shadowSurface.localScale = targetScale;
                return;
            }

            _shadowTween = _shadowSurface.DOScale(targetScale, ShadowAnimationDuration);
        }

        private void ResetVfxState()
        {
            _vfxController.ResetVfx();
            _vfxController.SetPaused(false);
        }
    }
}

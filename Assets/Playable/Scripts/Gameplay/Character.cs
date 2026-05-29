using System.Collections;
using System.Collections.Generic;
using Clipper2Lib;
using Core;
using Mechanics;
using Pooling;
using UI;
using UnityEngine;

namespace Gameplay
{
    public class Character : MonoBehaviour
    {
        private const int CaptureTrailBufferCapacity = 256;

        private static int _nextCharacterIndex;

        // Static fields on MonoBehaviours persist across Editor play-sessions within the same
        // Editor session. Without this reset, _nextCharacterIndex climbs each time you press
        // Play, producing ever-increasing stencil IDs. Stencil-based zone rendering stays
        // correct as long as every character in a single session has a unique, low index.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetCharacterIndex() => _nextCharacterIndex = 0;

        private IController _controller;
        private SkinConfig _skin;
        private readonly List<Vector3> _captureTrailBuffer = new List<Vector3>(CaptureTrailBufferCapacity);

        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private SkinView _visual;
        [SerializeField] private CharacterFeedback _feedback;
        [SerializeField] private PlayerHudView _hud;

        private bool _isInsideArea = true;
        private bool _isCaptureInFlight;
        private int _characterIndex;
        public CharacterArea _area;
        public CharacterTrail _trail;

        [Tooltip("Must roughly match the visual width of your character cube/trail")] [HideInInspector]
        public float _characterRadius = 0.5f;

        public CharacterMotor Motor => _motor;
        public bool IsPlayer { get; private set; }
        public CharacterSpawnConfig Config { get; private set; }
        public Vector3 PreviousPosition { get; private set; }
        public SkinConfig Skin => _skin;

        private void Update()
        {
            HandleAreaState();
        }

        public void Init(CharacterSpawnConfig config, SkinConfig skin, PaintParticlesPool paintParticlesPool)
        {

            Config = config;
            IsPlayer = config.IsPlayer;
            _skin = skin;
            _characterIndex = _nextCharacterIndex++;
            transform.position = new Vector3(config.SpawnPosX, config.SpawnPosZ, 0);
            PreviousPosition = transform.position;
            _characterRadius = config.CharacterRadius;

            AddController(config.IsPlayer);

            CollisionManager.Instance.RegisterCharacter(this);

            _controller.Init(config);
            _motor.Init(config);
            int stencilId = _characterIndex + 1;
            _area.Init(this, stencilId);
            _visual.Init(skin, _area, _characterIndex);
            _feedback.Init(this, skin, _area, _visual, paintParticlesPool);

            _hud.Setup(transform, skin.DisplayName, skin.HudNameColor);
        }

        public void UpdatePreviousPosition()
        {
            PreviousPosition = transform.position;
        }

        public void SwapController<T>() where T : MonoBehaviour, IController
        {
            if (_controller is MonoBehaviour oldComponent)
            {
                Destroy(oldComponent);
            }

            _controller = gameObject.AddComponent<T>();
            _controller.Init(Config);
        }

        public void Die()
        {
            // Load-bearing ordering: DeregisterCharacter MUST run before the Destroy() calls below
            // so CollisionManager can fire OnCharacterExitedZone for every occupant of this zone
            // while this character + _area are still live Unity objects (subscribers read
            // zoneOwner._area, zoneOwner.Skin, etc. for VFX/audio lookup).
            CollisionManager.Instance.DeregisterCharacter(this);
            // Coroutines started on this MonoBehaviour are auto-cancelled when the GameObject is
            // destroyed, so a pending SolveCaptureAsync naturally stops here. The flag/buffer are
            // reset only to keep state coherent if Die runs while we're paused mid-coroutine.
            _isCaptureInFlight = false;
            _captureTrailBuffer.Clear();
            _trail._logicPoints.Clear();
            // HUD is detached from this character at Init time, so Destroy(gameObject) below
            // won't reach it; clean it up explicitly here.
            Destroy(_hud.gameObject);
            Destroy(_area.gameObject);
            Destroy(gameObject);
        }

        private void HandleAreaState()
        {
            // Suppress all area-state transitions while a SolveCapture coroutine is running.
            // _isInsideArea stays true throughout, so the entry-branch can't re-trigger and the
            // leave-branch can't start a new trail until the in-flight capture has applied.
            if (_isCaptureInFlight)
            {
                return;
            }

            bool currentlyInside = _area.IsPointInside(transform.position);

            if (_isInsideArea && !currentlyInside)
            {
                _isInsideArea = false;
                _trail.OnLeaveOwnArea(transform.position);
            }
            else if (!_isInsideArea && currentlyInside)
            {
                _isInsideArea = true;
                _trail.OnEnterOwnArea(transform.position);
                StartCaptureCoroutine();
            }
        }

        private void StartCaptureCoroutine()
        {
            // Snapshot trail points into our reusable buffer and clear the live list immediately
            // so the trail can start recording fresh as soon as the player leaves again. The
            // coroutine reads from _captureTrailBuffer, never the live trail list. Reuse is safe
            // because _isCaptureInFlight gates HandleAreaState and prevents a second capture from
            // starting before this one completes.
            _captureTrailBuffer.Clear();
            List<Vector3> liveTrail = _trail._logicPoints;
            for (int i = 0; i < liveTrail.Count; i++)
            {
                _captureTrailBuffer.Add(liveTrail[i]);
            }
            liveTrail.Clear();

            // currentTerritory is only ever reassigned (never mutated in-place), so holding the
            // reference is safe even if SetTerritory replaces the field while we yield.
            Paths64 territorySnapshot = _area.CurrentTerritory;

            _isCaptureInFlight = true;
            StartCoroutine(RunCaptureCoroutine(territorySnapshot));
        }

        private IEnumerator RunCaptureCoroutine(Paths64 territorySnapshot)
        {
            Paths64 captureShape = null;
            yield return CaptureSolver.SolveCaptureAsync(
                _captureTrailBuffer,
                territorySnapshot,
                _characterRadius,
                result => captureShape = result);

            _captureTrailBuffer.Clear();

            if (captureShape != null && captureShape.Count > 0)
            {
                // ProcessTerritoryCaptureAsync spreads the heavy Clipper.Union across two frames
                // so large pattern territories don't cause a frame spike on capture.
                // _isCaptureInFlight stays true throughout both frames, which keeps
                // HandleAreaState() suppressed until the full capture is committed.
                yield return CollisionManager.Instance.ProcessTerritoryCaptureAsync(this, captureShape);
            }

            _isCaptureInFlight = false;

            // The player may have moved out of their area while the coroutines were running.
            // Re-evaluate immediately so we don't miss a leave transition.
            HandleAreaState();
        }

        private void AddController(bool isPlayerCharacter)
        {
            if (isPlayerCharacter)
            {
                bool isTutorial = GameManager.Instance != null
                                  && GameManager.Instance.CurrentState == GameState.Tutorial;

                _controller = isTutorial
                    ? gameObject.AddComponent<TutorialController>() as IController
                    : gameObject.AddComponent<PlayerController>() as IController;
            }
            else
            {
                _controller = gameObject.AddComponent<AIController>();
            }
        }
    }
}

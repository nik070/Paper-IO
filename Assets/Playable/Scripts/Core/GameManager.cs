using System.Collections;
using Clipper2Lib;
using DG.Tweening;
using DG.Tweening.Core;
using Effects;
using Gameplay;
using Mechanics;
using UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Core
{
    public class GameManager : SingletonBehaviour<GameManager>
    {
        [FormerlySerializedAs("levelManager")] [SerializeField]
        private LevelManager _levelManager;
        [SerializeField] private UIManager _uiManager;

        [FormerlySerializedAs("gameCamera")] [SerializeField]
        private CameraController _cameraController;

        [SerializeField] private ArenaController _arenaController;
        [SerializeField] private float _gridCellSize = 10f;

        [LunaPlaygroundField("Game Mode", 0, "Game")]
        [SerializeField] private GameMode _gameMode = GameMode.Normal;

        [LunaPlaygroundField("Win Condition", 1, "Game")]
        [SerializeField] private WinCondition _winCondition = WinCondition.FillMap;

        [LunaPlaygroundField("Win Fill Threshold", 2, "Game")]
        [SerializeField] private float _winFillThreshold = 0.95f;

        [LunaPlaygroundField("Show Tutorial", 3, "Game")]
        [SerializeField] private bool _isTutorial = true;

        [Header("Enemy Spawn Cinematic")]
        [SerializeField] private float _enemyFocusDuration = 1.5f;
        [SerializeField] private float _enemyFocusReturnDuration = 1f;
        [SerializeField] private float _enemyFocusLerpSpeed = 2f;

        private Character _player;
        private int _killCount;
        private int _totalEnemyCount;
        private bool _isReady;
        private bool _isPaused;
        private bool _enemySpawnCinematicInProgress;

        public GameState CurrentState { get; private set; }
        public GameMode CurrentGameMode => _gameMode;
        public Character Player => _player;
        public int KillCount => _killCount;
        public bool IsPaused => _isPaused;
        public bool IsTutorial => _isTutorial;
        public GameObject confitii;

        protected override void Awake()
        {
            base.Awake();
            GameEvents.OnCharacterDied += HandleCharacterDied;
            GameEvents.OnTerritoryCaptured += HandleTerritoryCaptured;
            Luna.Unity.LifeCycle.OnPause += PauseGameplay;
            Luna.Unity.LifeCycle.OnResume += ResumeGameplay;
        }

        private void Start()
        {
            // Explicit DOTween init so we can suppress the verbose safe-mode "nested tween" warnings
            // that fire from auto-cleanup on Verbose log behaviour. Default capacity is plenty for this playable.
            // Recycling is disabled because Luna's IL→JS conversion has issues with reused DOScale/Vector3
            // tweens (they silently fail on every other reuse of the same target — see GameplayFlyingTextView
            // diagnostics from Apr 29 2026). The GC cost of re-allocating tweens for a playable this small
            // is negligible compared to the bug class it eliminates.
            DOTween.Init();
            DOTween.defaultRecyclable = false;
            DOTween.logBehaviour = LogBehaviour.ErrorsOnly;

            PlayerParticlesManager.Instance.Init();
            _arenaController.Init();
            CollisionManager.Instance.Init(_arenaController.Radius, _gridCellSize);
            _levelManager.Init();
            CachePlayer();
            SetupCam();

            _isReady = true;
            // When tutorial is off, jump straight to Playing — UIManager and TutorialView gate on
            // GameState.Tutorial, so skipping that state hides the hand prompt and any other
            // tutorial-only UI automatically. SetState(Playing) also runs SwapController<PlayerController>
            // on the player, replacing the TutorialController that Character.AddController attaches
            // by default during spawn (CurrentState is still 0/Tutorial at LevelManager.Init time).
            SetState(_isTutorial ? GameState.Tutorial : GameState.Playing);
        }

        private void Update()
        {
            if (!_isReady)
            {
                return;
            }

            if (CurrentState == GameState.Tutorial && (Input.GetMouseButtonDown(0) || Input.touchCount > 0))
            {
                SetState(GameState.Playing);
            }
        }

        private void LateUpdate()
        {
            if (!_isReady || (CurrentState != GameState.Playing && CurrentState != GameState.Tutorial))
            {
                return;
            }

            CollisionManager.Instance.Tick();
        }

        protected override void OnDestroy()
        {
            GameEvents.OnCharacterDied -= HandleCharacterDied;
            GameEvents.OnTerritoryCaptured -= HandleTerritoryCaptured;
            Luna.Unity.LifeCycle.OnPause -= PauseGameplay;
            Luna.Unity.LifeCycle.OnResume -= ResumeGameplay;

            // Make sure we never leave Luna with a frozen timescale or muted audio if the manager is torn down mid-pause.
            if (_isPaused)
            {
                Time.timeScale = 1f;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.Resume();
                }
            }

            CollisionManager.Instance?.Dispose();
            base.OnDestroy();
        }

        public void SetState(GameState newState)
        {
          
            CurrentState = newState;

            switch (newState)
            {
                case GameState.Tutorial:
                    if (_player != null)
                    {
                        _player.SwapController<TutorialController>();
                    }
                    CollisionManager.Instance.FreezeAllEnemies();
                    break;

                case GameState.Playing:
                    if (_player != null)
                    {
                        _player.SwapController<PlayerController>();
                    }
                    CollisionManager.Instance.UnfreezeAllEnemies();
                    break;

                case GameState.Death:
                    CollisionManager.Instance.FreezeAllPlayers();
                    break;

                case GameState.Win:
                    confitii.SetActive(true);
                    CollisionManager.Instance.FreezeAllPlayers();
                    break;

                case GameState.EndCard:
                    break;
            }

            GameEvents.FireGameStateChanged(newState);
        }

        public bool TryGetZoneOwnerAtPoint(Vector3 point, Character ignoreCharacter, out Character owner)
        {
            if (CollisionManager.Instance == null)
            {
                owner = null;
                return false;
            }

            return CollisionManager.Instance.TryGetZoneOwnerAtPoint(point, ignoreCharacter, out owner);
        }

        private void PauseGameplay()
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            Time.timeScale = 0f;

            // AudioListener.pause is stripped from Luna's UnityEngine stub (CS0117), so we pause every
            // pooled and looping AudioSource via the project's AudioManager instead.
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Pause();
            }
        }

        private void ResumeGameplay()
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            Time.timeScale = 1f;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Resume();
            }
        }

        private void HandleCharacterDied(DeathInfo deathInfo)
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            if (deathInfo.Victim.IsPlayer)
            {
                SetState(GameState.Death);
            }
            else if(deathInfo.Killer.IsPlayer)
            {
                _killCount++;
            }

            CheckWinConditions();
        }

        private void HandleTerritoryCaptured(Character capturer, float playerFillPct, float capturedArea)
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            CheckWinConditions();
        }

        private void CheckWinConditions()
        {
            switch (_winCondition)
            {
                case WinCondition.FillMap:
                    if (_player != null)
                    {
                        float fillPct = CollisionManager.Instance.GetPlayerFillPercent(_player);
                        if (fillPct >= _winFillThreshold)
                        {
                            SnapPlayerTerritoryToArena();
                            SetState(GameState.Win);
                        }
                    }
                    break;

                case WinCondition.KillEnemies:
                    if (!CollisionManager.Instance.IsAnyBotAlive())
                    {
                        SetState(GameState.Win);
                    }
                    break;
            }
        }

        // Replace the player's union-of-captures territory with the exact arena polygon so the
        // win-card renders a clean circle instead of the ragged shape Clipper produced as zones
        // were stitched together. Threshold-based win only — kill-all-enemies wins skip this.
        private void SnapPlayerTerritoryToArena()
        {
            if (_player == null || _player._area == null || _arenaController == null)
            {
                return;
            }
            Paths64 arenaShape = _arenaController.CreateArenaPath();
            _player._area.SetTerritory(arenaShape);
        }

        // Pans the camera over to a freshly spawned enemy, freezes the player while the
        // bot's first moves play out, then pans back and hands control to the player again.
        // Skipped if another cinematic is already running or the game isn't in a playable state.
        // Can be skipped entirely by setting skipCinematic to true (e.g., for first bot spawn).
        public void PlayEnemySpawnCinematic(Character enemy, bool skipCinematic = false)
        {
            if (_enemySpawnCinematicInProgress || enemy == null || _player == null || _cameraController == null)
            {
                return;
            }

            if (CurrentState != GameState.Playing && CurrentState != GameState.Tutorial)
            {
                return;
            }
            
            // Skip the cinematic (e.g., for first bot - let player continue moving)
            if (skipCinematic)
            {
                return;
            }

            StartCoroutine(EnemySpawnCinematicRoutine(enemy));
        }

        private IEnumerator EnemySpawnCinematicRoutine(Character enemy)
        {
            _enemySpawnCinematicInProgress = true;

            FollowTarget follow = _cameraController.FollowTarget;
            bool prevLerping = follow.lerping;
            float prevLerpSpeed = follow.lerpSpeed;

            follow.lerping = true;
            follow.lerpSpeed = _enemyFocusLerpSpeed;

            _player.Motor.SetEnabled(false);

            follow.Target = enemy.transform;
            yield return new WaitForSeconds(_enemyFocusDuration);

            // Enemy may have died during the focus window (e.g. ran into its own trail).
            // The Character reference and its transform are destroyed in Die(), so guard before reading.
            if (_player != null)
            {
                follow.Target = _player.transform;
            }
            yield return new WaitForSeconds(_enemyFocusReturnDuration);

            follow.lerping = prevLerping;
            follow.lerpSpeed = prevLerpSpeed;

            if (_player != null)
            {
                _player.Motor.SetEnabled(true);
            }

            _enemySpawnCinematicInProgress = false;
        }

        private void CachePlayer()
        {
            CollisionManager.Instance.TryGetPlayer(out _player);
            _totalEnemyCount = CollisionManager.Instance.EnemyCount;
        }

        private void SetupCam()
        {
            if (_player != null)
            {
                _cameraController.FollowTarget.Target = _player.transform;
                _cameraController.FollowTarget.SetInstantly();
            }
        }
    }
}

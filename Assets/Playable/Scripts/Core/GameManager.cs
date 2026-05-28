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
        [FormerlySerializedAs("levelManager")]
        [SerializeField]
        private LevelManager _levelManager;
        [SerializeField] private UIManager _uiManager;

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

        private Character _player;
        private int _killCount;
        private int _totalEnemyCount;
        private bool _isReady;
        private bool _isPaused;

        public GameState CurrentState { get; private set; }
        public GameMode CurrentGameMode => _gameMode;
        public Character Player => _player;
        public int KillCount => _killCount;
        public bool IsPaused => _isPaused;
        public bool IsTutorial => _isTutorial;

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
            else if (deathInfo.Killer.IsPlayer)
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

        private void CachePlayer()
        {
            CollisionManager.Instance.TryGetPlayer(out _player);
            _totalEnemyCount = CollisionManager.Instance.EnemyCount;
        }

        public void PlayEnemySpawnCinematic(Character bot)
        {
            StartCoroutine(SpawnCinematicRoutine(bot));
        }

        private System.Collections.IEnumerator SpawnCinematicRoutine(Character bot)
        {
            if (_player != null && _player.Motor != null)
            {
                _player.Motor.SetEnabled(false);
            }

            if (CinemachineController.Instance != null && bot != null)
            {
                CinemachineController.Instance.FocusTemporarily(CmCameraType.Game, bot.transform, 0.8f);
            }

            yield return new WaitForSeconds(0.8f);

            if (_player != null && _player.Motor != null)
            {
                _player.Motor.SetEnabled(true);
            }
        }

        private void SetupCam()
        {
            if (_player != null && CinemachineController.Instance != null)
            {
                CinemachineController.Instance.SetCameraTarget(CmCameraType.Game, _player.transform);
            }
        }
    }
}


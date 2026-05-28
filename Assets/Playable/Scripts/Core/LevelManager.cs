using Gameplay;
using Pooling;
using UnityEngine;

namespace Core
{
    [LunaPlaygroundSection("Level Setup")]
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private PaintParticlesPool _paintParticlesPool;
        [SerializeField] private Character _baseCharacterPrefab;

        [Header("Skins")]
        [SerializeField] private SkinConfig[] _skinConfigs;

        [Header("Player")]
        [LunaPlaygroundField("Player Skin", 0, "Characters")]
        [SerializeField] private int _playerSkinIndex;

        [LunaPlaygroundField("Player Speed", 1, "Characters")]
        [SerializeField] private float _playerSpeed = 10f;

        [SerializeField] private float _playerTurnSpeed = 8f;
        [SerializeField] private float _playerCharacterRadius = 0.5f;
        [SerializeField] private Vector2 _playerSpawnPos;

        [Tooltip("Optional: overrides the player's default circular starting zone with a shape generated from a B&W texture.")]
        [SerializeField] private TexturePatternTerritory _playerStartingPattern;

        [Header("Bot Gameplay")]
        [SerializeField] private float _botSpeed = 10f;
        [SerializeField] private float _botTurnSpeed = 8f;
        [SerializeField] private float _botCharacterRadius = 0.5f;
        [SerializeField] private float _botRiskTaker = 0.1f;

        [Header("Bot Spawns")]
        [SerializeField] private int _botCount = 4;
        [SerializeField] private Vector2[] _botSpawnPositions;

        [Tooltip("Visualised radius around every bot spawn point.\n" +
                 "Overlapping circles mean two bots will start with touching or intersecting territories.\n" +
                 "Increase spacing until all circles are separate.")]
        [SerializeField] private float _botSpawnRadius = 3f;

        [LunaPlaygroundFieldArrayLength(1, 8)]
        [LunaPlaygroundField("Bot Skins", 2, "Characters")]
        [SerializeField] private int[] _botSkinIndices = { 1, 2, 3, 3 };

        // Tracks which bot slots have already been spawned so that re-pressing
        // the same number key does not produce a duplicate. Cleared each Init().
        private bool[] _botSpawned;

        public void Init()
        {
            InitializeLevel();
            _paintParticlesPool.Init();
        }

        private void InitializeLevel()
        {
            if (_skinConfigs == null || _skinConfigs.Length == 0)
            {
                Debug.LogError("LevelManager: No SkinConfigs assigned.");
                return;
            }

            _botSpawned = new bool[_botCount];

            SpawnPlayer();
            // Bots are no longer spawned here. Press 1, 2, 3 or 4 at runtime
            // to spawn the corresponding bot — see Update().
        }

        private void Update()
        {
            // Number row 1..4 → spawn bot index 0..3
            if (Input.GetKeyDown(KeyCode.Alpha1)) TrySpawnBot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TrySpawnBot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TrySpawnBot(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) TrySpawnBot(3);
        }

        private void TrySpawnBot(int index)
        {
            if (_botSpawned == null)
            {
                // Init() hasn't run yet — ignore key presses on the title screen, etc.
                return;
            }

            if (index < 0 || index >= _botCount)
            {
                Debug.LogWarning($"LevelManager: bot index {index} is outside _botCount ({_botCount}).");
                return;
            }

            if (_botSpawnPositions == null || index >= _botSpawnPositions.Length ||
                _botSkinIndices == null || index >= _botSkinIndices.Length)
            {
                Debug.LogWarning($"LevelManager: bot index {index} has no spawn position or skin assigned.");
                return;
            }

            if (_botSpawned[index])
            {
                // Already spawned this slot — ignore.
                return;
            }

            Character bot = SpawnBot(index);
            _botSpawned[index] = true;

            // Cinematic: pan camera to the new bot's spawn, freeze the player while the
            // bot starts moving, then pan back and resume player control.
            if (bot != null && GameManager.Instance != null)
            {
                GameManager.Instance.PlayEnemySpawnCinematic(bot);
            }
        }

        private void SpawnPlayer()
        {
            var config = new CharacterSpawnConfig
            {
                IsPlayer = true,
                Id = "player",
                SpawnPosX = _playerSpawnPos.x,
                SpawnPosZ = _playerSpawnPos.y,
                Speed = _playerSpeed,
                TurnSpeed = _playerTurnSpeed,
                CharacterRadius = _playerCharacterRadius,
                CanKillSelfWithTrail = true,
                CanBeKilledIfTrailCut = true,
                CanBeKilledByAreaCapture = true
            };

            ApplyGameModeOverrides(config);

            SkinConfig skin = ResolveSkin(_playerSkinIndex);
            Character player = SpawnCharacter(config, skin);

            // Replace the default circular starting zone with the preset texture pattern.
            // Runs after CharacterArea.Init so we overwrite — not race — the initial circle.
            // Passing the spawn position unions a starting-radius circle in so the player is
            // guaranteed a round safe zone at spawn even when the pattern is thin/empty there.
            if (_playerStartingPattern != null && player != null && player._area != null)
            {
                _playerStartingPattern.ApplyTo(player._area, player.transform.position);
            }
        }

        private Character SpawnBot(int i)
        {
            Vector2 spawnPos = ResolveBotSpawnPosition(_botSpawnPositions[i]);

            var config = new CharacterSpawnConfig
            {
                IsPlayer = false,
                Id = $"bot{i + 1}",
                SpawnPosX = spawnPos.x,
                SpawnPosZ = spawnPos.y,
                Speed = _botSpeed,
                TurnSpeed = _botTurnSpeed,
                CharacterRadius = _botCharacterRadius,
                RiskTaker = _botRiskTaker,
                CanKillSelfWithTrail = false,
                CanBeKilledIfTrailCut = true,
                CanBeKilledByAreaCapture = true
            };

            SkinConfig skin = ResolveSkin(_botSkinIndices[i]);
            return SpawnCharacter(config, skin);
        }

        /// <summary>
        /// Picks an open spot near the configured spawn so a late-spawn bot can't materialise
        /// inside the player's expanded territory or sitting on their trail (the new bot's
        /// starting zone would otherwise render on top of an existing one).
        /// </summary>
        private Vector2 ResolveBotSpawnPosition(Vector2 desired)
        {
            CharacterArea areaPrefab = _baseCharacterPrefab != null
                ? _baseCharacterPrefab.GetComponentInChildren<CharacterArea>(true)
                : null;
            float clearance = areaPrefab != null ? areaPrefab.StartingAreaRadius : 3f;

            Vector3 desired3 = new Vector3(desired.x, desired.y, 0f);
            if (CollisionManager.Instance == null)
            {
                return desired;
            }

            CollisionManager.Instance.TryFindSafeSpawn(desired3, clearance, out Vector3 safe);
            return new Vector2(safe.x, safe.y);
        }

        private Character SpawnCharacter(CharacterSpawnConfig config, SkinConfig skin)
        {
            Character character = Instantiate(_baseCharacterPrefab);
            character.gameObject.name = config.IsPlayer ? "Player" : $"Bot_{config.Id}";
            character.Init(config, skin, _paintParticlesPool);
            return character;
        }

        private SkinConfig ResolveSkin(int index)
        {
            int clamped = Mathf.Clamp(index, 0, _skinConfigs.Length - 1);
            return _skinConfigs[clamped];
        }

        // ── Gizmos ───────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            // Player spawn — blue
            Vector3 playerWorld = new Vector3(_playerSpawnPos.x, _playerSpawnPos.y, 0f);
            Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.95f);
            Gizmos.DrawSphere(playerWorld, 0.45f);
            Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.4f);
            Gizmos.DrawWireSphere(playerWorld, 0.9f);

            if (_botSpawnPositions == null) return;

            float diameter = _botSpawnRadius * 2f;

            for (int i = 0; i < _botSpawnPositions.Length; i++)
            {
                Vector3 pos = new Vector3(_botSpawnPositions[i].x, _botSpawnPositions[i].y, 0f);

                // Check if this position overlaps any other bot or the player
                bool overlaps = IsOverlappingOthers(i);

                // Centre dot
                Gizmos.color = overlaps
                    ? new Color(1f, 0.15f, 0.15f, 0.95f)   // red — overlapping
                    : new Color(1f, 0.35f, 0.15f, 0.95f);   // orange — clear
                Gizmos.DrawSphere(pos, 0.45f);

                // Territory radius ring
                Gizmos.color = overlaps
                    ? new Color(1f, 0.1f, 0.1f, 0.55f)      // red ring
                    : new Color(1f, 0.75f, 0f, 0.45f);       // yellow ring
                Gizmos.DrawWireSphere(pos, _botSpawnRadius);
            }
        }

        private bool IsOverlappingOthers(int index)
        {
            if (_botSpawnPositions == null) return false;
            Vector2 a = _botSpawnPositions[index];
            float minDist = _botSpawnRadius * 2f;

            // Check against every other bot
            for (int j = 0; j < _botSpawnPositions.Length; j++)
            {
                if (j == index) continue;
                if (Vector2.Distance(a, _botSpawnPositions[j]) < minDist) return true;
            }

            // Check against player spawn
            if (Vector2.Distance(a, _playerSpawnPos) < minDist) return true;

            return false;
        }

        private void ApplyGameModeOverrides(CharacterSpawnConfig config)
        {
            switch (GameManager.Instance.CurrentGameMode)
            {
                case GameMode.Normal:
                    config.CanKillSelfWithTrail = true;
                    config.CanBeKilledIfTrailCut = true;
                    config.CanBeKilledByAreaCapture = true;
                    break;

                case GameMode.Invincible:
                    config.CanKillSelfWithTrail = false;
                    config.CanBeKilledIfTrailCut = false;
                    config.CanBeKilledByAreaCapture = false;
                    break;

                case GameMode.EnemiesOnly:
                    config.CanKillSelfWithTrail = false;
                    config.CanBeKilledIfTrailCut = true;
                    config.CanBeKilledByAreaCapture = true;
                    break;

                case GameMode.ExpandArea:
                    config.CanKillSelfWithTrail = false;
                    config.CanBeKilledIfTrailCut = true;
                    config.CanBeKilledByAreaCapture = true;
                    break;
            }
        }
    }
}

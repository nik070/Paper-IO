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

        [Header("Bot Gameplay")]
        [SerializeField] private float _botSpeed = 10f;
        [SerializeField] private float _botTurnSpeed = 8f;
        [SerializeField] private float _botCharacterRadius = 0.5f;
        [SerializeField] private float _botRiskTaker = 0.1f;

        [Header("Bot Spawns")]
        [SerializeField] private int _botCount = 4;
        [SerializeField] private Vector2[] _botSpawnPositions;

        [LunaPlaygroundFieldArrayLength(1, 8)]
        [LunaPlaygroundField("Bot Skins", 2, "Characters")]
        [SerializeField] private int[] _botSkinIndices = { 1, 2, 3, 3 };

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

            SpawnPlayer();
            // SpawnBots(); // Disabled automatic spawning to allow manual key-press spawning
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
            SpawnCharacter(config, skin);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnBot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnBot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnBot(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnBot(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SpawnBot(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SpawnBot(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SpawnBot(6);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SpawnBot(7);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SpawnBot(8);
        }

        private void SpawnBot(int index)
        {
            if (_botSpawnPositions == null || _botSkinIndices == null)
            {
                return;
            }

            if (index >= _botSpawnPositions.Length || index >= _botSkinIndices.Length)
            {
                Debug.LogWarning($"Cannot spawn bot {index + 1}: Not enough spawn positions or skin indices configured in LevelManager.");
                return;
            }

            var config = new CharacterSpawnConfig
            {
                IsPlayer = false,
                Id = $"bot{index + 1}",
                SpawnPosX = _botSpawnPositions[index].x,
                SpawnPosZ = _botSpawnPositions[index].y,
                Speed = _botSpeed,
                TurnSpeed = _botTurnSpeed,
                CharacterRadius = _botCharacterRadius,
                RiskTaker = _botRiskTaker,
                CanKillSelfWithTrail = false,
                CanBeKilledIfTrailCut = true,
                CanBeKilledByAreaCapture = true
            };

            SkinConfig skin = ResolveSkin(_botSkinIndices[index]);
            SpawnCharacter(config, skin);
        }

        private void SpawnCharacter(CharacterSpawnConfig config, SkinConfig skin)
        {
            Character character = Instantiate(_baseCharacterPrefab);
            character.gameObject.name = config.IsPlayer ? "Player" : $"Bot_{config.Id}";
            character.Init(config, skin, _paintParticlesPool);
        }

        private SkinConfig ResolveSkin(int index)
        {
            int clamped = Mathf.Clamp(index, 0, _skinConfigs.Length - 1);
            return _skinConfigs[clamped];
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

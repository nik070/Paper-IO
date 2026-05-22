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
            SpawnBots();
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

        private void SpawnBots()
        {
            if (_botSpawnPositions == null || _botSkinIndices == null)
            {
                return;
            }

            int count = Mathf.Min(_botCount, Mathf.Min(_botSpawnPositions.Length, _botSkinIndices.Length));
            for (int i = 0; i < count; i++)
            {
                var config = new CharacterSpawnConfig
                {
                    IsPlayer = false,
                    Id = $"bot{i + 1}",
                    SpawnPosX = _botSpawnPositions[i].x,
                    SpawnPosZ = _botSpawnPositions[i].y,
                    Speed = _botSpeed,
                    TurnSpeed = _botTurnSpeed,
                    CharacterRadius = _botCharacterRadius,
                    RiskTaker = _botRiskTaker,
                    CanKillSelfWithTrail = false,
                    CanBeKilledIfTrailCut = true,
                    CanBeKilledByAreaCapture = true
                };

                SkinConfig skin = ResolveSkin(_botSkinIndices[i]);
                SpawnCharacter(config, skin);
            }
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

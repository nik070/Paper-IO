// using Gameplay;
// using Pooling;
// using UnityEngine;

// namespace Core
// {
//     [LunaPlaygroundSection("Level Setup")]
//     public class LevelManager : MonoBehaviour
//     {
//         [SerializeField] private PaintParticlesPool _paintParticlesPool;
//         [SerializeField] private Character _baseCharacterPrefab;

//         [Header("Skins")]
//         [SerializeField] private SkinConfig[] _skinConfigs;

//         [Header("Player")]
//         [LunaPlaygroundField("Player Skin", 0, "Characters")]
//         [SerializeField] private int _playerSkinIndex;

//         [LunaPlaygroundField("Player Speed", 1, "Characters")]
//         [SerializeField] private float _playerSpeed = 10f;

//         [SerializeField] private float _playerTurnSpeed = 8f;
//         [SerializeField] private float _playerCharacterRadius = 0.5f;
//         [SerializeField] private Vector2 _playerSpawnPos;

//         [Header("Bot Gameplay")]
//         [SerializeField] private float _botSpeed = 10f;
//         [SerializeField] private float _botTurnSpeed = 8f;
//         [SerializeField] private float _botCharacterRadius = 0.5f;
//         [SerializeField] private float _botRiskTaker = 0.1f;

//         [Header("Bot Spawns")]
//         [SerializeField] private int _botCount = 4;
//         [SerializeField] private Vector2[] _botSpawnPositions;

//         [LunaPlaygroundFieldArrayLength(1, 8)]
//         [LunaPlaygroundField("Bot Skins", 2, "Characters")]
//         [SerializeField] private int[] _botSkinIndices = { 1, 2, 3, 3 };

//         public void Init()
//         {
//             InitializeLevel();
//             _paintParticlesPool.Init();
//         }

//         private void InitializeLevel()
//         {
//             if (_skinConfigs == null || _skinConfigs.Length == 0)
//             {
//                 Debug.LogError("LevelManager: No SkinConfigs assigned.");
//                 return;
//             }

//             SpawnPlayer();
//             // SpawnBots(); // Disabled automatic spawning to allow manual key-press spawning
//         }

//         private void SpawnPlayer()
//         {
//             var config = new CharacterSpawnConfig
//             {
//                 IsPlayer = true,
//                 Id = "player",
//                 SpawnPosX = _playerSpawnPos.x,
//                 SpawnPosZ = _playerSpawnPos.y,
//                 Speed = _playerSpeed,
//                 TurnSpeed = _playerTurnSpeed,
//                 CharacterRadius = _playerCharacterRadius,
//                 CanKillSelfWithTrail = true,
//                 CanBeKilledIfTrailCut = true,
//                 CanBeKilledByAreaCapture = true
//             };

//             ApplyGameModeOverrides(config);

//             SkinConfig skin = ResolveSkin(_playerSkinIndex);
//             SpawnCharacter(config, skin);
//         }

//         private void Update()
//         {
//             if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnBot(0);
//             if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnBot(1);
//             if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnBot(2);
//             if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnBot(3);
//             if (Input.GetKeyDown(KeyCode.Alpha5)) SpawnBot(4);
//             if (Input.GetKeyDown(KeyCode.Alpha6)) SpawnBot(5);
//             if (Input.GetKeyDown(KeyCode.Alpha7)) SpawnBot(6);
//             if (Input.GetKeyDown(KeyCode.Alpha8)) SpawnBot(7);
//             if (Input.GetKeyDown(KeyCode.Alpha9)) SpawnBot(8);
//         }

//         private void SpawnBot(int index)
//         {
//             if (_botSpawnPositions == null || _botSkinIndices == null)
//             {
//                 return;
//             }

//             if (index >= _botSpawnPositions.Length || index >= _botSkinIndices.Length)
//             {
//                 Debug.LogWarning($"Cannot spawn bot {index + 1}: Not enough spawn positions or skin indices configured in LevelManager.");
//                 return;
//             }

//             var config = new CharacterSpawnConfig
//             {
//                 IsPlayer = false,
//                 Id = $"bot{index + 1}",
//                 SpawnPosX = _botSpawnPositions[index].x,
//                 SpawnPosZ = _botSpawnPositions[index].y,
//                 Speed = _botSpeed,
//                 TurnSpeed = _botTurnSpeed,
//                 CharacterRadius = _botCharacterRadius,
//                 RiskTaker = _botRiskTaker,
//                 CanKillSelfWithTrail = false,
//                 CanBeKilledIfTrailCut = true,
//                 CanBeKilledByAreaCapture = true
//             };

//             SkinConfig skin = ResolveSkin(_botSkinIndices[index]);
//             Character spawnedBot = SpawnCharacter(config, skin);

//             // Focus on the newly spawned bot for a split second
//             if (CinemachineController.Instance != null && spawnedBot != null)
//             {
//                 CinemachineController.Instance.FocusTemporarily(CmCameraType.Game, spawnedBot.transform, 0.8f);
//             }
//         }

//         private Character SpawnCharacter(CharacterSpawnConfig config, SkinConfig skin)
//         {
//             Character character = Instantiate(_baseCharacterPrefab);
//             character.gameObject.name = config.IsPlayer ? "Player" : $"Bot_{config.Id}";
//             character.Init(config, skin, _paintParticlesPool);
//             return character;
//         }

//         private SkinConfig ResolveSkin(int index)
//         {
//             int clamped = Mathf.Clamp(index, 0, _skinConfigs.Length - 1);
//             return _skinConfigs[clamped];
//         }

//         private void ApplyGameModeOverrides(CharacterSpawnConfig config)
//         {
//             switch (GameManager.Instance.CurrentGameMode)
//             {
//                 case GameMode.Normal:
//                     config.CanKillSelfWithTrail = true;
//                     config.CanBeKilledIfTrailCut = true;
//                     config.CanBeKilledByAreaCapture = true;
//                     break;

//                 case GameMode.Invincible:
//                     config.CanKillSelfWithTrail = false;
//                     config.CanBeKilledIfTrailCut = false;
//                     config.CanBeKilledByAreaCapture = false;
//                     break;

//                 case GameMode.EnemiesOnly:
//                     config.CanKillSelfWithTrail = false;
//                     config.CanBeKilledIfTrailCut = true;
//                     config.CanBeKilledByAreaCapture = true;
//                     break;

//                 case GameMode.ExpandArea:
//                     config.CanKillSelfWithTrail = false;
//                     config.CanBeKilledIfTrailCut = true;
//                     config.CanBeKilledByAreaCapture = true;
//                     break;
//             }
//         }
//     }
// }
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

        [SerializeField] private float _playerTurnSpeed = 20f;
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
            // Number row 1..9 → spawn bot index 0..8
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    TrySpawnBot(i);
                }
            }

#if UNITY_EDITOR
            // Add bot spawn at runtime by holding M and clicking in Game View
            if (Input.GetKey(KeyCode.M) && Input.GetMouseButtonDown(0) && Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane groundPlane = new Plane(Vector3.forward, Vector3.zero); // Z = 0 plane
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    AddBotSpawnPosition(new Vector2(hitPoint.x, hitPoint.y));
                    Debug.Log($"Added bot spawn at {hitPoint}. Total bots: {_botCount}. Press Alpha{_botCount} to spawn.");
                }
            }
#endif
        }

        public void AddBotSpawnPosition(Vector2 position)
        {
            var newPositions = new System.Collections.Generic.List<Vector2>(_botSpawnPositions ?? new Vector2[0]);
            newPositions.Add(position);
            _botSpawnPositions = newPositions.ToArray();

            var newSkins = new System.Collections.Generic.List<int>(_botSkinIndices ?? new int[0]);
            newSkins.Add(UnityEngine.Random.Range(0, _skinConfigs != null ? _skinConfigs.Length : 4));
            _botSkinIndices = newSkins.ToArray();

            if (_botSpawned != null)
            {
                var newSpawned = new System.Collections.Generic.List<bool>(_botSpawned);
                newSpawned.Add(false);
                _botSpawned = newSpawned.ToArray();
            }

            _botCount = _botSpawnPositions.Length;
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

#if UNITY_EDITOR
namespace Core
{
    [UnityEditor.CustomEditor(typeof(LevelManager))]
    public class LevelManagerEditor : UnityEditor.Editor
    {
        private bool _isSpawnModeActive = false;
        private bool _isMPressed;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Level Design Tools", UnityEditor.EditorStyles.boldLabel);
            
            GUI.backgroundColor = _isSpawnModeActive ? Color.green : Color.white;
            if (GUILayout.Button(_isSpawnModeActive ? "Bot Spawn Mode: ON (Click to Disable)" : "Bot Spawn Mode: OFF (Click to Enable)", GUILayout.Height(30)))
            {
                _isSpawnModeActive = !_isSpawnModeActive;
                UnityEditor.SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (_isSpawnModeActive)
            {
                UnityEditor.EditorGUILayout.HelpBox("Click anywhere on the ground in the Scene View to add a bot spawn point.\n(You can also hold 'M' while clicking instead of using this toggle).", UnityEditor.MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            LevelManager manager = (LevelManager)target;
            Event e = Event.current;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.M)
            {
                _isMPressed = true;
                UnityEditor.SceneView.RepaintAll();
            }
            else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.M)
            {
                _isMPressed = false;
                UnityEditor.SceneView.RepaintAll();
            }

            // Prevent selecting other objects in the scene while Spawn Mode is active
            if (_isSpawnModeActive || _isMPressed)
            {
                int controlID = GUIUtility.GetControlID(FocusType.Passive);
                if (e.type == EventType.Layout)
                {
                    UnityEditor.HandleUtility.AddDefaultControl(controlID);
                }
            }

            if ((_isSpawnModeActive || _isMPressed) && e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = UnityEditor.HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    UnityEditor.Undo.RecordObject(manager, "Add Bot Spawn Position");
                    manager.AddBotSpawnPosition(new Vector2(hitPoint.x, hitPoint.y));
                    UnityEditor.EditorUtility.SetDirty(manager);
                    e.Use(); // Consume the click so it doesn't do anything else
                }
            }
        }
    }
}
#endif

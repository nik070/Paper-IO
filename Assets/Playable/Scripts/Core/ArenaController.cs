using UnityEngine;
using Clipper2Lib;
using Mechanics;

namespace Core
{
    [LunaPlaygroundSection("Arena", 1)]
    public class ArenaController : SingletonBehaviour<ArenaController>
    {

        [LunaPlaygroundField("Arena Radius", 0, "Arena")] [SerializeField]
        private float _arenaRadius = 30f;

        private const float ArenaShadowZOffset = 0.8f;

        [SerializeField] private float _borderWidth = 0.15f;
        [SerializeField] private Material _borderMaterial;
        [SerializeField] private MeshFilter _shadowMeshFilter;

        [Header("Starfish Border")]
        [Tooltip("Arc distance between starfish centres in world units. Smaller = denser ring with no gaps. Set 0 to fall back to _starfishCount.")]
        [SerializeField] private float _starfishSpacing = 2.2f;
        [Tooltip("Used only when _starfishSpacing <= 0.")]
        [SerializeField] private int _starfishCount = 80;
        [SerializeField] private float _starfishRadiusOffset = 0.5f;
        [SerializeField] private float _starfishZ = 0f;
        [SerializeField] private bool _faceOutward = true;
        [SerializeField] private float _starfishRandomRadiusJitter = 0f;
        [Tooltip("Random yaw spin in degrees to break up repetition.")]
        [SerializeField] private float _starfishYawJitter = 25f;

        private const string StarfishCollectionResourcePath = "StartFishCollection";

        private MeshFilter _meshFilter;
        private LineRenderer _borderLineRenderer;
        private Mesh _generatedMesh;
        private Vector2[] _arenaPoints;
        private Transform _starfishCollection;

        private float _arenaRadiusSqr;

        public float Radius => _arenaRadius;

        public void Init()
        {
            _arenaRadiusSqr = _arenaRadius * _arenaRadius;

            _meshFilter = GetComponent<MeshFilter>();

            GenerateGroundMesh();
            SetupBorder();
            SetupStarfishBorder();
        }

        public Vector3 ClampToArena(Vector3 position)
        {
            float sqrMag = position.x * position.x + position.y * position.y;
            if (sqrMag > _arenaRadiusSqr)
            {
                float mag = Mathf.Sqrt(sqrMag);
                position.x = position.x / mag * _arenaRadius;
                position.y = position.y / mag * _arenaRadius;
            }

            return position;
        }

        public Vector3[] GetSpawnPositions(int count, float spawnRadiusFraction = 0.7f)
        {
            float spawnRadius = _arenaRadius * spawnRadiusFraction;
            var positions = new Vector3[count];
            float angleStep = Mathf.PI * 2f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep;
                positions[i] = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    Mathf.Sin(angle) * spawnRadius,
                    0f
                );
            }

            return positions;
        }

        public bool IsInsideArena(Vector3 position)
        {
            return position.x * position.x + position.y * position.y <= _arenaRadiusSqr;
        }

        /// <summary>
        /// Returns a fresh Paths64 copy of the arena polygon — the exact shape used to draw the
        /// ground mesh. Useful for snapping a character's territory to a clean circle (e.g. when
        /// a player wins by filling the map and we want the final mesh to match the visible arena
        /// instead of being a high-vertex Clipper2 union).
        /// </summary>
        public Paths64 CreateArenaPath()
        {
            if (_arenaPoints == null)
            {
                _arenaPoints = GenerateArenaPoints();
            }

            Path64 ring = new Path64(_arenaPoints.Length);
            for (int i = 0; i < _arenaPoints.Length; i++)
            {
                Vector2 point = _arenaPoints[i];
                ring.Add(GeometryUtils.ToPoint64(new Vector3(point.x, point.y, 0f)));
            }
            return new Paths64 { ring };
        }

        private void GenerateGroundMesh()
        {
            _arenaPoints = GenerateArenaPoints();
            Paths64 arenaPath = new Paths64
            {
                new Path64(_arenaPoints.Length)
            };

            for (int i = 0; i < _arenaPoints.Length; i++)
            {
                Vector2 point = _arenaPoints[i];
                arenaPath[0].Add(GeometryUtils.ToPoint64(new Vector3(point.x, point.y, 0f)));
            }

            if (_generatedMesh == null)
            {
                _generatedMesh = new Mesh
                {
                    name = "ArenaMesh"
                };
            }

            GeometryUtils.UpdateMeshWithPaths(_generatedMesh, arenaPath);
            _meshFilter.sharedMesh = _generatedMesh;

            if (_shadowMeshFilter != null)
            {
                _shadowMeshFilter.sharedMesh = _generatedMesh;
                _shadowMeshFilter.transform.localPosition = new Vector3(0f, 0f, ArenaShadowZOffset);
            }
        }

        private void SetupBorder()
        {
            var borderGO = new GameObject("ArenaBorder");
            borderGO.transform.SetParent(transform, false);

            _borderLineRenderer = borderGO.AddComponent<LineRenderer>();
            _borderLineRenderer.useWorldSpace = false;
            _borderLineRenderer.loop = true;
            _borderLineRenderer.positionCount = _arenaPoints.Length;

            if (_borderMaterial != null)
            {
                _borderLineRenderer.material = _borderMaterial;
            }

            _borderLineRenderer.startWidth = _borderWidth;
            _borderLineRenderer.endWidth = _borderWidth;

            float borderRadius = _arenaRadius + _borderWidth;
            for (int i = 0; i < _arenaPoints.Length; i++)
            {
                Vector2 borderPoint = _arenaPoints[i].normalized * borderRadius;
                _borderLineRenderer.SetPosition(i, new Vector3(borderPoint.x, borderPoint.y, -0.01f));
            }
        }

        private void SetupStarfishBorder()
        {
            var prefab = Resources.Load<GameObject>(StarfishCollectionResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"ArenaController: missing Resources/{StarfishCollectionResourcePath}.prefab — skipping starfish border.");
                return;
            }

            if (_starfishCollection != null)
            {
                Destroy(_starfishCollection.gameObject);
            }

            var instance = Instantiate(prefab, transform, false);
            instance.name = "StartFishCollection";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            _starfishCollection = instance.transform;

            int childCount = _starfishCollection.childCount;
            if (childCount == 0)
            {
                return;
            }

            float ringRadius = _arenaRadius + _borderWidth + _starfishRadiusOffset;

            // Decide how many starfish fit. Spacing > 0 means "fill the ring at this arc-distance";
            // otherwise fall back to the explicit count.
            int targetCount;
            if (_starfishSpacing > 0.01f)
            {
                float circumference = 2f * Mathf.PI * ringRadius;
                targetCount = Mathf.Max(3, Mathf.RoundToInt(circumference / _starfishSpacing));
            }
            else
            {
                targetCount = Mathf.Max(3, _starfishCount);
            }

            // Grow the pool by cloning the first child as a template if we need more than the
            // prefab provides. Cheaper than mutating the prefab itself.
            Transform template = _starfishCollection.GetChild(0);
            while (_starfishCollection.childCount < targetCount)
            {
                Instantiate(template.gameObject, _starfishCollection);
            }

            // Deactivate any leftover children if targetCount shrunk below what we have.
            for (int i = targetCount; i < _starfishCollection.childCount; i++)
            {
                _starfishCollection.GetChild(i).gameObject.SetActive(false);
            }

            float angleStep = Mathf.PI * 2f / targetCount;
            Quaternion lieFlat = Quaternion.Euler(-90f, 0f, 0f);

            for (int i = 0; i < targetCount; i++)
            {
                Transform child = _starfishCollection.GetChild(i);
                child.gameObject.SetActive(true);

                float angle = i * angleStep;

                float radius = ringRadius;
                if (_starfishRandomRadiusJitter > 0f)
                {
                    radius += Random.Range(-_starfishRandomRadiusJitter, _starfishRandomRadiusJitter);
                }

                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                child.localPosition = new Vector3(x, y, _starfishZ);

                float baseYaw = _faceOutward ? angle * Mathf.Rad2Deg : Random.Range(0f, 360f);
                if (_starfishYawJitter > 0f)
                {
                    baseYaw += Random.Range(-_starfishYawJitter, _starfishYawJitter);
                }
                child.localRotation = lieFlat * Quaternion.Euler(0f, baseYaw, 0f);
            }
        }

        private Vector2[] GenerateArenaPoints()
        {
            float thetaScale = 0.15f / _arenaRadius;
            float theta = 0f;
            int size = (int)(1f / thetaScale + 1f);
            var points = new Vector2[size];

            for (int i = 0; i < size; i++)
            {
                theta += 2f * Mathf.PI * thetaScale;
                points[i] = new Vector2(
                    _arenaRadius * Mathf.Cos(theta),
                    _arenaRadius * Mathf.Sin(theta)
                );
            }

            return points;
        }
    }
}

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

        private MeshFilter _meshFilter;
        private LineRenderer _borderLineRenderer;
        private Mesh _generatedMesh;
        private Vector2[] _arenaPoints;

        private float _arenaRadiusSqr;

        public float Radius => _arenaRadius;

        public void Init()
        {
            _arenaRadiusSqr = _arenaRadius * _arenaRadius;

            _meshFilter = GetComponent<MeshFilter>();

            GenerateGroundMesh();
            SetupBorder();
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
            _borderLineRenderer.enabled = false;

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

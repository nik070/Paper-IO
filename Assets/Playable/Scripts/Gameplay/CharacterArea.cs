using System;
using Clipper2Lib;
using DG.Tweening;
using Effects.ZoneTransitionVFX;
using Mechanics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CharacterArea : MonoBehaviour
    {
        private const float MaskedMeshZ = -0.01f;
        private const float CreatedMeshStartZ = 0.59f;
        private const float CreatedMeshEndZ = 0f;
        private const float CreatedMeshDuration = 0.35f;
        private const float ShadowMeshZ = 0.6f;
        private const float ShadowTransparentMeshZ = 1.1f;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int StencilRefProperty = Shader.PropertyToID("_StencilRef");
        private static readonly int ZWriteHash = Shader.PropertyToID("_ZWrite");
        private static readonly int ZTestHash = Shader.PropertyToID("_ZTest");

        [SerializeField] private float _startingAreaRadius = 3f;
        public float StartingAreaRadius => _startingAreaRadius;

        [SerializeField] private MeshFilter _createdMeshFilter;
        [SerializeField] private MeshFilter _maskedMeshFilter;
        [SerializeField] private MeshFilter _shadowMeshFilter;
        [SerializeField] private MeshFilter _shadowTransparentMeshFilter;
        [SerializeField] private MeshRenderer _createdMeshRenderer;
        [SerializeField] public MeshRenderer _maskedMeshRenderer;

        private MeshFilter _meshFilter;
        private Mesh _createdMesh;
        private Tween _createdMeshTween;
        private MaterialPropertyBlock _propertyBlock;
        private Material _createdMeshMaterial;

        [NonSerialized] public Mesh _mesh;
        [NonSerialized] public Paths64 CurrentTerritory;
        [NonSerialized] public Rect64 TerritoryBounds;
        [NonSerialized] public Rect64 CreatedTerritoryBounds;
        private Rect64[] _pathBounds;  // Per-path AABB cache for fast IsPointInside

        public int StencilID { get; private set; }
        public MeshRenderer ZoneRenderer { get; private set; }
        public MeshRenderer CreatedMeshRenderer => _createdMeshRenderer;
        public MeshRenderer ShadowRenderer { get; private set; }
        public MeshRenderer ShadowTransparentRenderer { get; private set; }

        public void Init(Character character, int stencilId)
        {
            StencilID = stencilId;
            _propertyBlock = new MaterialPropertyBlock();

            gameObject.name = character.gameObject.name + " Area";
            transform.parent = null;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            _meshFilter = GetComponent<MeshFilter>();
            ZoneRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh();
            _mesh.MarkDynamic();
            _meshFilter.mesh = _mesh;

            _createdMesh = new Mesh();
            _createdMesh.MarkDynamic();
            _createdMeshFilter.mesh = _createdMesh;

            if (_shadowMeshFilter != null)
            {
                ShadowRenderer = _shadowMeshFilter.GetComponent<MeshRenderer>();
                _shadowMeshFilter.mesh = _mesh;
                _shadowMeshFilter.transform.localPosition = new Vector3(0f, 0f, ShadowMeshZ);
            }

            _maskedMeshFilter.mesh = _mesh;
            _maskedMeshFilter.transform.localPosition = new Vector3(0f, 0f, MaskedMeshZ);
            _maskedMeshFilter.gameObject.SetActive(false);
            _createdMeshRenderer.gameObject.SetActive(false);

            _shadowTransparentMeshFilter.mesh = _mesh;
            _shadowTransparentMeshFilter.transform.localPosition = new Vector3(0f, 0f, ShadowTransparentMeshZ);

            CurrentTerritory = GeometryUtils.CreateCirclePath64(_startingAreaRadius, character.transform.position);
            SetTerritory(CurrentTerritory);
        }

        /// <summary>
        /// Sets stencil ref on both zone materials matching Paper2 ZoneBase.SetMaterialsStencilRef.
        /// Slot[0] = ActorZone (stencilRef | BIT5), Slot[1] = WriteStencil (raw stencilRef).
        /// Accessing .materials creates per-renderer instances so each character gets unique refs.
        /// Also propagates the instanced zone material to CreatedMesh and MaskedMesh renderers.
        /// </summary>
        public void SetMaterialsStencilRef(int stencilRef)
        {
            Material[] zoneMaterials = ZoneRenderer.materials;
            if (zoneMaterials.Length != 2)
            {
                return;
            }

            byte byteRef = (byte)stencilRef;
            // the material in position 1 is the write stencil mat
            zoneMaterials[1].SetFloat(StencilRefProperty, byteRef);

            byteRef |= 0b00100000; // BIT 5 means that it's a zone.
            // the material in position 0 is the actor zone mat
            zoneMaterials[0].SetFloat(StencilRefProperty, byteRef);
        }

        public void SetShadowColor(Color color)
        {
            SetRendererColor(ShadowRenderer, color);
        }

        public void SetShadowTransparentColor(Color color)
        {
            SetRendererColor(ShadowTransparentRenderer, color);
        }

        public void SetTerritory(Paths64 newTerritory)
        {
            CurrentTerritory = GeometryUtils.RemoveHoles(newTerritory);
            TerritoryBounds = Clipper.GetBounds(CurrentTerritory);
            RebuildPathBounds();
            UpdateMeshSmooth(CurrentTerritory);
        }

        /// <summary>
        /// Fast path: sets territory without running RemoveHoles.
        /// Use when the caller has already cleaned holes or when holes are intentional (pattern).
        /// </summary>
        public void SetTerritoryClean(Paths64 cleanedTerritory)
        {
            CurrentTerritory = cleanedTerritory;
            TerritoryBounds = Clipper.GetBounds(CurrentTerritory);
            RebuildPathBounds();
            UpdateMeshSmooth(CurrentTerritory);
        }

        /// <summary>
        /// Rebuilds the per-path AABB cache used by IsPointInside for fast early-out.
        /// </summary>
        private void RebuildPathBounds()
        {
            if (CurrentTerritory == null || CurrentTerritory.Count == 0)
            {
                _pathBounds = null;
                return;
            }
            _pathBounds = new Rect64[CurrentTerritory.Count];
            for (int i = 0; i < CurrentTerritory.Count; i++)
            {
                _pathBounds[i] = Clipper.GetBounds(new Paths64 { CurrentTerritory[i] });
            }
        }

        /// <summary>
        /// Rounds sharp corners into smooth circular arcs before generating the mesh.
        /// Uses inflate(+R, Round) → deflate(-R, Round) which smooths outer corners
        /// without ever eroding thin features. The net displacement on straight edges
        /// is zero (inflate pushes out, deflate pulls back). Only sharp corners gain
        /// a circular arc of radius ~SmoothRadius.
        /// Smoothed paths are used ONLY for rendering — CurrentTerritory retains
        /// original vertices for accurate game logic.
        /// </summary>
        private const double SmoothRadius = 0.35 * GeometryUtils.Scale;

        /// <summary>
        /// Applies visual-only smoothing that rounds corners without destroying thin features.
        /// For complex territories (pattern with many holes), skips the expensive inflate/deflate
        /// and just does light RDP — the pattern is already smooth from Chaikin subdivision.
        /// </summary>
        private Paths64 GetMorphologicallyRoundedPaths(Paths64 territory)
        {
            if (territory == null || territory.Count == 0)
            {
                return territory;
            }

            // Fast path: skip expensive inflate/deflate for complex territories.
            // Inflate/deflate is O(n²) and cripples framerate on pattern territories
            // with many paths and hundreds of vertices. The pattern is already smooth
            // from Chaikin subdivision so rounding adds no visual benefit.
            int totalVerts = 0;
            for (int i = 0; i < territory.Count; i++)
                totalVerts += territory[i].Count;

            if (territory.Count > 8 || totalVerts > 600)
            {
                return Clipper.RamerDouglasPeucker(territory, 0.01 * GeometryUtils.Scale);
            }

            // Full rounding for simple territories (regular captures, circles, etc.)
            Paths64 inflated = Clipper.InflatePaths(territory, SmoothRadius, JoinType.Round, EndType.Polygon);
            if (inflated == null || inflated.Count == 0)
            {
                return territory;
            }

            Paths64 smoothed = Clipper.InflatePaths(inflated, -SmoothRadius, JoinType.Round, EndType.Polygon);
            if (smoothed == null || smoothed.Count == 0)
            {
                smoothed = territory;
            }

            return Clipper.RamerDouglasPeucker(smoothed, 0.002 * GeometryUtils.Scale);
        }

        private void UpdateMeshSmooth(Paths64 territory)
        {
            if (territory == null || territory.Count == 0)
            {
                _mesh.Clear();
                return;
            }

            Paths64 smoothed = GetMorphologicallyRoundedPaths(territory);

            GeometryUtils.UpdateMeshWithPaths(_mesh, smoothed, transform.position.z);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000f);
        }

        public void ShowCreatedTerritory(Paths64 createdTerritory)
        {
            if (_createdMeshFilter == null || _createdMeshRenderer == null || createdTerritory == null || createdTerritory.Count == 0)
            {
                Debug.LogError("Created mesh filter or renderer is null or created territory is empty");
                return;
            }

            CreatedTerritoryBounds = Clipper.GetBounds(createdTerritory);

            // Perform the exact same morphological rounding for the visual transition mesh
            Paths64 smoothedCreated = GetMorphologicallyRoundedPaths(createdTerritory);

            GeometryUtils.UpdateMeshWithPaths(_createdMesh, smoothedCreated, 0f);
            _createdMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000f);

            _createdMeshFilter.sharedMesh = _createdMesh;
            _maskedMeshFilter.sharedMesh = _createdMesh;
            _createdMeshRenderer.gameObject.SetActive(true);
            _maskedMeshFilter.gameObject.SetActive(true);
            _createdMeshRenderer.transform.localPosition = new Vector3(0f, 0f, CreatedMeshStartZ);

            _createdMeshTween?.Kill();

            _createdMeshTween = _createdMeshRenderer.transform
                .DOLocalMoveZ(CreatedMeshEndZ, CreatedMeshDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(HideCreatedTerritory)
                .OnKill(HideCreatedTerritory);
        }

        public void PlayZoneTransitionVfx(Paths64 stolenTerritory, SkinConfig sourceSkin, Vector3 origin)
        {
            ZoneTransitionVfxController controller = ZoneTransitionVfxController.Active;

            if (controller != null)
            {
                controller.PlayZoneTransitionVfx(stolenTerritory, origin, sourceSkin, StencilID);
            }
        }

        public bool IsPointInside(Vector3 worldPoint)
        {
            if (CurrentTerritory == null || CurrentTerritory.Count == 0)
            {
                return false;
            }

            var targetPt = GeometryUtils.ToPoint64(worldPoint);

            if (targetPt.X < TerritoryBounds.left || targetPt.X > TerritoryBounds.right ||
                targetPt.Y < TerritoryBounds.top || targetPt.Y > TerritoryBounds.bottom)
            {
                return false;
            }

            // EvenOdd containment with per-path bounding box early-out.
            // Skip expensive O(V) PointInPolygon for paths whose AABB doesn't contain the point.
            int containCount = 0;
            for (int i = 0; i < CurrentTerritory.Count; i++)
            {
                // Per-path AABB check — O(1) to skip non-containing paths
                if (_pathBounds != null && i < _pathBounds.Length)
                {
                    Rect64 b = _pathBounds[i];
                    if (targetPt.X < b.left || targetPt.X > b.right ||
                        targetPt.Y < b.top || targetPt.Y > b.bottom)
                    {
                        continue;
                    }
                }

                if (Clipper.PointInPolygon(targetPt, CurrentTerritory[i]) != PointInPolygonResult.IsOutside)
                {
                    containCount++;
                }
            }

            return (containCount % 2) != 0;
        }

        private void OnDestroy()
        {
            if (_createdMeshTween != null)
            {
                _createdMeshTween.Kill();
                _createdMeshTween = null;
            }

            if (_createdMesh != null)
            {
                Destroy(_createdMesh);
                _createdMesh = null;
            }
        }

        private void SetRendererColor(Renderer targetRenderer, Color color)
        {
            if (targetRenderer == null)
            {
                return;
            }

            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorProperty, color);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void HideCreatedTerritory()
        {
            _createdMeshRenderer.gameObject.SetActive(false);
            _maskedMeshRenderer.gameObject.SetActive(false);
        }

        public void UpdateCreatedMeshMaterial()
        {
            if (_createdMeshMaterial)
            {
                Destroy(_createdMeshMaterial);
            }

            _createdMeshMaterial = Instantiate(ZoneRenderer.sharedMaterial);
            _createdMeshMaterial.renderQueue = 1950;

            _createdMeshMaterial.SetFloat(ZWriteHash, 0); // ZWrite Off
            _createdMeshMaterial.SetFloat(ZTestHash, (int)CompareFunction.Always);

            CreatedMeshRenderer.sharedMaterial = _createdMeshMaterial;
        }
    }
}
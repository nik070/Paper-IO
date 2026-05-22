using System;
using DG.Tweening;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Effects.ZoneTransitionVFX
{
    public class ZoneTransitionVfxElement : MonoBehaviour
    {
        private const float HeightUnit = -0.02f;
        private const int ZoneRenderQueue = 2010;
        private const int ShadowRenderQueue = 1310;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int OriginProperty = Shader.PropertyToID("_Origin");
        private static readonly int RadiusProperty = Shader.PropertyToID("_Radius");
        private static readonly int StencilCompProperty = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilRefProperty = Shader.PropertyToID("_StencilRef");
        private static readonly int ZoneTransitionOnProperty = Shader.PropertyToID("_ZoneTransitionOn");

        private Material[] _runtimeMeshMaterials;
        private Material _runtimeShadowMaterial;
        private Material[] _sourceMeshMaterials;
        private Material _sourceShadowMaterial;
        private Mesh _ownedMesh;
        private Tween _radiusTween;

        [Header("Zone mesh")]
        [SerializeField] private MeshFilter _mesh;
        [SerializeField] private MeshRenderer _meshRenderer;

        [Header("Shadow mesh")]
        [SerializeField] private MeshFilter _shadowMesh;
        [SerializeField] private MeshRenderer _shadowMeshRenderer;

        [Header("Animation")]
        [SerializeField] private float _speed = 40f;

        public Action<ZoneTransitionVfxElement> OnVfxComplete;

        public Mesh OwnedMesh => _ownedMesh;
        public MeshRenderer MeshRenderer => _meshRenderer;

        private void Awake()
        {
            _sourceMeshMaterials = _meshRenderer.sharedMaterials;
            _sourceShadowMaterial = _shadowMeshRenderer.sharedMaterial;

            // One persistent dynamic mesh per element. Refilled in-place by the controller
            // via GeometryUtils.UpdateMeshWithPaths so we never allocate a new GPU buffer
            // (gl.createBuffer was the top symbol of the worst hitch in the trace).
            _ownedMesh = new Mesh { name = "ZoneTransitionVfxMesh" };
            _ownedMesh.MarkDynamic();
            _mesh.sharedMesh = _ownedMesh;
            _shadowMesh.sharedMesh = _ownedMesh;
        }

        private void OnDestroy()
        {
            ForcePlayCancellation();
            ReleaseMaterials();

            if (_ownedMesh != null)
            {
                Destroy(_ownedMesh);
                _ownedMesh = null;
            }
        }

        public void LowerHeight()
        {
            transform.localPosition -= new Vector3(0f, 0f, HeightUnit);
        }

        public void ForcePlayCancellation()
        {
            if (_radiusTween == null)
            {
                return;
            }

            _radiusTween.Kill();
            _radiusTween = null;
        }

        public void Play(Vector3 origin, float radius)
        {
            ForcePlayCancellation();
            SetOrigin(origin);
            UpdateRadius(0f);

            float duration = Mathf.Max(0.01f, radius / _speed);
            _radiusTween = DOVirtual
                .Float(0f, radius, duration, UpdateRadius)
                .SetEase(Ease.Linear)
                .OnComplete(CompleteVfx);
        }

        public void Setup(float height, SkinConfig skinConfig, int stencilRef)
        {
            // Mesh contents are filled in-place on _ownedMesh by the controller before this
            // Setup call (see ZoneTransitionVfxController.PlayZoneTransitionVfx).
            transform.localPosition = new Vector3(0f, 0f, height * HeightUnit);
            SetupMaterial(skinConfig, stencilRef);
        }

        private void CompleteVfx()
        {
            _radiusTween = null;
            OnVfxComplete?.Invoke(this);
        }

        private void SetupMaterial(SkinConfig skinConfig, int stencilRef)
        {
            ReleaseMaterials();

            int materialCount = Mathf.Max(1, _sourceMeshMaterials.Length);
            _runtimeMeshMaterials = new Material[materialCount];

            Material zoneMaterial = skinConfig.ZoneMaterial != null
                ? skinConfig.ZoneMaterial
                : _sourceMeshMaterials[0];

            _runtimeMeshMaterials[0] = Instantiate(zoneMaterial);
            for (int i = 1; i < materialCount; i++)
            {
                _runtimeMeshMaterials[i] = Instantiate(_sourceMeshMaterials[i]);
            }

            for (int i = 0; i < _runtimeMeshMaterials.Length; i++)
            {
                SetupTransitionMaterial(_runtimeMeshMaterials[i], stencilRef, ZoneRenderQueue);
            }

            _meshRenderer.sharedMaterials = _runtimeMeshMaterials;

            _runtimeShadowMaterial = Instantiate(_sourceShadowMaterial);
            SetupTransitionMaterial(_runtimeShadowMaterial, stencilRef, ShadowRenderQueue);
            _runtimeShadowMaterial.SetInt(StencilCompProperty, (int)CompareFunction.Always);
            _runtimeShadowMaterial.SetColor(ColorProperty, skinConfig.ZoneShadowColor);
            _shadowMeshRenderer.sharedMaterial = _runtimeShadowMaterial;
        }

        private void SetupTransitionMaterial(Material material, int stencilRef, int renderQueue)
        {
            // Only the zone shader (ActorZone) declares the ZONE_TRANSITION_ON toggle.
            // Other layers (e.g. AdditiveGlow using Unlit/ZoneTransitionOverlay) do not, and
            // Luna's runtime keyword validator errors on EnableKeyword for unsupported variants.
            if (material.HasProperty(ZoneTransitionOnProperty))
            {
                material.EnableKeyword("ZONE_TRANSITION_ON");
                material.SetFloat(ZoneTransitionOnProperty, 1f);
            }

            material.SetFloat(StencilRefProperty, stencilRef);
            material.renderQueue = renderQueue;
        }

        private void SetOrigin(Vector3 origin)
        {
            Vector4 originValue = new Vector4(origin.x, origin.y, 0f, 0f);
            for (int i = 0; i < _runtimeMeshMaterials.Length; i++)
            {
                _runtimeMeshMaterials[i].SetVector(OriginProperty, originValue);
            }

            _runtimeShadowMaterial.SetVector(OriginProperty, originValue);
        }

        private void UpdateRadius(float radius)
        {
            for (int i = 0; i < _runtimeMeshMaterials.Length; i++)
            {
                _runtimeMeshMaterials[i].SetFloat(RadiusProperty, radius);
            }

            _runtimeShadowMaterial.SetFloat(RadiusProperty, radius);
        }

        private void ReleaseMaterials()
        {
            if (_runtimeMeshMaterials != null)
            {
                for (int i = 0; i < _runtimeMeshMaterials.Length; i++)
                {
                    if (_runtimeMeshMaterials[i] != null)
                    {
                        Destroy(_runtimeMeshMaterials[i]);
                    }
                }

                _runtimeMeshMaterials = null;
            }

            if (_runtimeShadowMaterial != null)
            {
                Destroy(_runtimeShadowMaterial);
                _runtimeShadowMaterial = null;
            }
        }
    }
}

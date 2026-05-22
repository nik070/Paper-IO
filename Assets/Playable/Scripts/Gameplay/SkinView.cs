using UnityEngine;

namespace Gameplay
{
    public class SkinView : MonoBehaviour
    {
        private static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
        private static readonly Color ShadowTransparentColor = new Color(0f, 0f, 0f, 0.196f);

        [SerializeField] private Transform _skinRoot;
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private LineRenderer _zoneCutter;

        private GameObject _skinInstance;

        public Transform CrownContainer { get; private set; }
        public Transform SkinRoot => _skinRoot;

        public void Init(SkinConfig skin, CharacterArea area, int characterIndex)
        {
            if (skin == null)
            {
                return;
            }

            if (skin.CharacterPrefab != null)
            {
                _skinInstance = Instantiate(skin.CharacterPrefab, _skinRoot);
            }
            else
            {
                _skinInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _skinInstance.transform.SetParent(_skinRoot);

                Collider col = _skinInstance.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }
            }

            _skinInstance.transform.localPosition = Vector3.zero;
            _skinInstance.transform.localRotation = Quaternion.identity;

            FindCrownContainer(skin);
            SetupTrail(skin, characterIndex);
            SetupZoneAppearance(skin, area);
        }

        /// <summary>
        /// Matches Paper2 Player.ResetZoneAppearance:
        /// shared zone material on renderers, instanced stencil, shadow colors.
        /// </summary>
        private void SetupZoneAppearance(SkinConfig skin, CharacterArea area)
        {
            Material zoneMaterial = skin.ZoneMaterial;
            if (zoneMaterial == null)
            {
                return;
            }

            Material[] zoneMaterials = area.ZoneRenderer.sharedMaterials;
            if (zoneMaterials.Length == 2)
            {
                zoneMaterials[0] = zoneMaterial;
                area.ZoneRenderer.sharedMaterials = zoneMaterials;
            }
            else
            {
                area.ZoneRenderer.sharedMaterial = zoneMaterial;
            }

            area.SetMaterialsStencilRef(area.StencilID);
            area.SetShadowColor(skin.ZoneShadowColor);
            area.SetShadowTransparentColor(ShadowTransparentColor);
            area.UpdateCreatedMeshMaterial();
        }

        private void SetupTrail(SkinConfig skin, int characterIndex)
        {
            if (_lineRenderer != null)
            {
                if (skin.TrailMaterial != null)
                {
                    _lineRenderer.sharedMaterial = skin.TrailMaterial;
                }

                Color trailColor = skin.TrailColor;
                _lineRenderer.startColor = trailColor;
                _lineRenderer.endColor = trailColor;
            }

            if (_zoneCutter != null)
            {
                _zoneCutter.material.SetFloat(StencilRef, characterIndex + 1);
            }
        }

        private void FindCrownContainer(SkinConfig skin)
        {
            Transform scaleFactor = _skinInstance.transform.Find("ScaleFactor");
            if (scaleFactor != null)
            {
                Transform crown = scaleFactor.Find("CrownContainer");
                if (crown != null)
                {
                    CrownContainer = crown;
                    return;
                }
            }

            Debug.LogWarning($"CrownContainer not found in skin prefab '{skin.DisplayName}'");
            CrownContainer = _skinInstance.transform;
        }
    }
}

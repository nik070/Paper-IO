using UnityEngine;

namespace Gameplay
{
    // TODO: check all configs are set correctly
    [CreateAssetMenu(fileName = "Skin_New", menuName = "Playable/Skin Config")]
    public class SkinConfig : ScriptableObject
    {
        [SerializeField] private string _displayName;

        [Header("Character")]
        [SerializeField] private GameObject _characterPrefab;
        [Tooltip("Representative color for UI/effects. Not applied to 3D mesh.")]
        [SerializeField] private Color _characterColor = Color.white;
        [SerializeField] private Color _vfxAdditiveColor;
        [SerializeField] private Color _vfxAdditiveTrailColor;
        [SerializeField] private Color _vfxAdditiveSkinColor;

        [Header("Sizing")]
        [SerializeField] private float _skinHeight = 0.5f;
        [SerializeField] private float _shadowSize = 1.0f;

        [Header("Zone")]
        [SerializeField] private Color _zoneColor = Color.white;
        [SerializeField] private Color _zoneShadowColor = Color.gray;
        [SerializeField] private Material _zoneMaterial;

        [Header("Trail")]
        [SerializeField] private Color _trailColor = Color.white;
        [SerializeField] private Material _trailMaterial;

        [Header("UI")]
        [SerializeField] private bool _overrideHudSpecularColor;
        [SerializeField] private Color _hudSpecularColor;

        [Header("HUD")]
        [SerializeField] private Color _hudNameColor = Color.white;

        public string DisplayName => _displayName;
        public GameObject CharacterPrefab => _characterPrefab;
        public Color CharacterColor => _characterColor;
        public float SkinHeight => _skinHeight;
        public float ShadowSize => _shadowSize;
        public Color ZoneTextureColor => _zoneColor;
        public Color ZoneShadowColor => _zoneShadowColor;
        public Material ZoneMaterial => _zoneMaterial;
        public Color TrailColor => _trailColor;
        public Material TrailMaterial => _trailMaterial;
        public Color VfxAdditiveColor => _vfxAdditiveColor;
        public Color VfxAdditiveTrailColor => _vfxAdditiveTrailColor;
        public Color VfxAdditiveSkinColor => _vfxAdditiveSkinColor;
        public bool OverrideHudSpecularColor => _overrideHudSpecularColor;
        public Color HudSpecularColor => _hudSpecularColor;
        public Color HudNameColor => _hudNameColor;
    }
}

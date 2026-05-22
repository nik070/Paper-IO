using UnityEditor;
using UnityEngine;

public class ActorZoneInspector : ShaderGUI
{
    private MaterialEditor _materialEditor;
    private MaterialProperty[] _properties;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        _materialEditor = materialEditor;
        _properties = properties;

        DrawStencilProperties();
        DrawCullProperties();

        bool complexEnabled = DrawToggleKeyword("_ComplexOn", "COMPLEX_ON", "Complex On");


        if (complexEnabled)
        {
            DrawComplex();
        }
        else
        {
            DrawMainTexture();
        }

        bool zoneTransitionOn = DrawToggleKeyword("_ZoneTransitionOn", "ZONE_TRANSITION_ON", "Zone Transition On");

        if (zoneTransitionOn)
        {
            DrawZoneTransition();
        }

        bool zoneCreationOn = DrawToggleKeyword("_ZoneCreationOn", "ZONE_CREATION_ON", "Zone Creation On");

        if (zoneCreationOn)
        {
            DrawZoneCreation();
        }

        bool hueOn = DrawToggleKeyword("_HueShiftOn", "HUE_SHIFT_ON", "Hue Shift On");

        if (hueOn)
        {
            DrawHue();
        }

        bool distortionOn = DrawToggleKeyword("_DistortionOn", "DISTORTION_ON", "Distortion On");

        if (distortionOn)
        {
            DrawUniversalMaterial();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
        materialEditor.RenderQueueField();
    }

    private void DrawComplex()
    {
        EditorGUI.indentLevel++;

        // Background
        EditorGUILayout.LabelField("Background", EditorStyles.boldLabel);
        _materialEditor.TexturePropertySingleLine(new GUIContent("Background"), FindProp("_BackgroundTex"));
        _materialEditor.FloatProperty(FindProp("_BackgroundTiling"), "Background Tiling");
        _materialEditor.FloatProperty(FindProp("_BackgroundParallax"), "Background Parallax");

        EditorGUILayout.Space();

        // Pattern 01
        EditorGUILayout.LabelField("Pattern 01", EditorStyles.boldLabel);
        _materialEditor.TexturePropertySingleLine(new GUIContent("Pattern 01"), FindProp("_Pattern01"));
        _materialEditor.FloatProperty(FindProp("_Pattern01Tiling"), "Pattern 01 Tiling");
        _materialEditor.FloatProperty(FindProp("_Pattern01Parallax"), "Pattern 01 Parallax");
        _materialEditor.ColorProperty(FindProp("_Pattern01Color"), "Pattern 01 Color");
        _materialEditor.FloatProperty(FindProp("_Pattern01Intensity"), "Pattern 01 Intensity");
        EditorGUILayout.Space();

        // Pattern 02
        EditorGUILayout.LabelField("Pattern 02", EditorStyles.boldLabel);
        _materialEditor.TexturePropertySingleLine(new GUIContent("Pattern 02"), FindProp("_Pattern02"));
        _materialEditor.FloatProperty(FindProp("_Pattern02Tiling"), "Pattern 02 Tiling");
        _materialEditor.FloatProperty(FindProp("_Pattern02Parallax"), "Pattern 02 Parallax");
        _materialEditor.ColorProperty(FindProp("_Pattern02Color"), "Pattern 02 Color");
        _materialEditor.FloatProperty(FindProp("_Pattern02Intensity"), "Pattern 02 Intensity");
        EditorGUILayout.Space();

        // Noise
        EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel);
        _materialEditor.TexturePropertySingleLine(new GUIContent("Noise"), FindProp("_Noise"));
        _materialEditor.FloatProperty(FindProp("_NoiseTiling"), "Noise Tiling");
        _materialEditor.FloatProperty(FindProp("_NoiseSpeed"), "Noise Speed");

        EditorGUILayout.Space();
        EditorGUI.indentLevel--;
    }

    private void DrawCullProperties()
    {
        _materialEditor.ShaderProperty(FindProp("_Cull"), "Culling Mode");
        _materialEditor.ShaderProperty(FindProp("_ZWrite"), "ZWrite");
        _materialEditor.ShaderProperty(FindProp("_ZTest"), "ZTest");
        EditorGUILayout.Space();
    }

    private void DrawHue()
    {
        EditorGUI.indentLevel++;

        _materialEditor.RangeProperty(FindProp("_HueOffset"), "Hue Offset");
        _materialEditor.RangeProperty(FindProp("_SaturationOffset"), "Saturation Offset");
        _materialEditor.RangeProperty(FindProp("_ValueOffset"), "Value Offset");
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();
    }

    private void DrawMainTexture()
    {
        EditorGUI.indentLevel++;
        _materialEditor.ColorProperty(FindProp("_Color"), "Main Color");
        _materialEditor.TextureProperty(FindProp("_MainTex"), "Main Tex");
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
    }

    private void DrawStencilProperties()
    {
        _materialEditor.ShaderProperty(FindProp("_StencilRef"), "Zone ID (stencil ref)");
        _materialEditor.ShaderProperty(FindProp("_StencilComp"), "Stencil Comparison");

        EditorGUILayout.Space();
    }

    private bool DrawToggleKeyword(string prop, string keyword, string label)
    {
        var boldLabel = new GUIStyle(GUI.skin.toggle);
        boldLabel.fontStyle = FontStyle.Bold;

        MaterialProperty materialProp = FindProp(prop);

        EditorGUI.BeginChangeCheck();

        bool propEnabled = materialProp.floatValue > 0.5f;
        propEnabled = EditorGUILayout.Toggle(label, propEnabled, boldLabel);

        if (EditorGUI.EndChangeCheck())
        {
            materialProp.floatValue = propEnabled ? 1f : 0f;

            if (propEnabled)
            {
                ((Material)_materialEditor.target).EnableKeyword(keyword);
            }
            else
            {
                ((Material)_materialEditor.target).DisableKeyword(keyword);
            }
        }

        EditorGUILayout.Space();

        return propEnabled;
    }

    private void DrawUniversalMaterial()
    {
        EditorGUI.indentLevel++;

        _materialEditor.TextureProperty(FindProp("_Wave01"), "Wave 01", true);
        //_materialEditor.TextureProperty(FindProp("_Wave02"), "Wave 02", true);
        _materialEditor.VectorProperty(FindProp("_WaveSpeed01"), "Wave Speed 01");
        //_materialEditor.VectorProperty(FindProp("_WaveSpeed02"), "Wave Speed 02");
        _materialEditor.FloatProperty(FindProp("_WaveStrength01"), "Wave Strength 01");
        //_materialEditor.FloatProperty(FindProp("_WaveStrength02"), "Wave Strength 02");
        //_materialEditor.FloatProperty(FindProp("_WaveStrength0102"), "Wave Strength 01 * 02");
        _materialEditor.FloatProperty(FindProp("_WaveScaleMaster"), "Wave Scale Master");
        _materialEditor.FloatProperty(FindProp("_WaveSpeedMaster"), "Wave Speed Master");
        _materialEditor.FloatProperty(FindProp("_WaveInfluence01toBase"), "Wave Influence 01 to base");
        //_materialEditor.FloatProperty(FindProp("_WaveInfluence01to02"), "Wave Influence 01 to 02");
        _materialEditor.ColorProperty(FindProp("_WaveColorShadow"), "Wave Color Shadow");
        _materialEditor.ColorProperty(FindProp("_WaveColorLight"), "Wave Color Light");
        _materialEditor.TextureProperty(FindProp("_BorderGradient"), "Border Gradient", true);
        _materialEditor.FloatProperty(FindProp("_BorderGradientStrength"), "Border Gradient Strength");
        _materialEditor.TextureProperty(FindProp("_Matcap"), "Matcap", true);
        _materialEditor.FloatProperty(FindProp("_MatcapStrength"), "Matcap Strength");
        _materialEditor.FloatProperty(FindProp("_MatcapRotation"), "Matcap Rotation");

        EditorGUI.indentLevel--;
        EditorGUILayout.Space();
    }

    private void DrawZoneCreation()
    {
        EditorGUI.indentLevel++;
        _materialEditor.TexturePropertySingleLine(new GUIContent("Zone Creation Noise Tex"), FindProp("_ZoneCreationNoiseTex"));
        _materialEditor.TexturePropertySingleLine(new GUIContent("Zone Creation Overlay Tex"), FindProp("_ZoneCreationOverlayTex"));
        _materialEditor.VectorProperty(FindProp("_Origin"), "Origin");
        _materialEditor.FloatProperty(FindProp("_Radius"), "Radius");
        _materialEditor.FloatProperty(FindProp("_ZoneCreationNoiseTiling"), "Zone Creation Noise Tiling");
        _materialEditor.FloatProperty(FindProp("_ZoneCreationOverlayTiling"), "Zone Creation Overlay Tiling");
        _materialEditor.FloatProperty(FindProp("_ZoneCreationOverlayIntensity"), "Zone Creation Overlay Intensity");
        _materialEditor.FloatProperty(FindProp("_ZoneCreationBorderIntensity"), "Zone Creation Border Intensity");
        _materialEditor.FloatProperty(FindProp("_ZoneCreationInflation"), "Zone Creation Inflation");


        EditorGUI.indentLevel--;
        EditorGUILayout.Space();
    }

    private void DrawZoneTransition()
    {
        EditorGUI.indentLevel++;
        _materialEditor.VectorProperty(FindProp("_Origin"), "Origin");
        _materialEditor.FloatProperty(FindProp("_Radius"), "Radius");
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();
    }

    private MaterialProperty FindProp(string name)
    {
        return FindProperty(name, _properties, false);
    }
}

// Built-in render pipeline underwater overlay.
// Apply to a fullscreen quad (or Canvas Image stretched to the screen) parented in front of
// the camera. Two scrolling samples of the normal map perturb each other so the water surface
// keeps moving without ever obviously tiling. No GrabPass / no scene depth — safe for Luna WebGL.

Shader "Playable/UnderwaterFog"
{
    Properties
    {
        _Color       ("Tint Color",                Color)        = (0.0, 0.55, 0.78, 1.0)
        _DeepColor   ("Deep Color (vignette)",     Color)        = (0.0, 0.12, 0.25, 1.0)
        [NoScaleOffset] _NormalMap ("Distortion Normal Map", 2D) = "bump" {}
        _Alpha       ("Overlay Alpha",             Range(0, 1))  = 0.6
        _Distance    ("Fog Density (vignette)",    Range(0, 1))  = 0.5
        _Refraction  ("Distortion Strength",       Range(0, 0.2))= 0.05
        _UV          ("UV xy=Tile, zw=Scroll",     Vector)       = (2.0, 2.0, 0.04, 0.02)
        _Caustics    ("Caustic Brightness",        Range(0, 2))  = 0.8
        _ZWrite      ("ZWrite",                    Float)        = 0
        _ZTest       ("ZTest",                     Float)        = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        ZWrite [_ZWrite]
        ZTest  [_ZTest]
        Cull   Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _NormalMap;
            fixed4    _Color;
            fixed4    _DeepColor;
            float     _Alpha;
            float     _Distance;
            float     _Refraction;
            float4    _UV;
            float     _Caustics;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Two opposing scrolls so the surface never visibly tiles.
                float2 uvA = i.uv * _UV.xy + _Time.y * _UV.zw;
                float2 uvB = i.uv * _UV.xy * 1.3 - _Time.y * _UV.zw * 0.7;

                float3 nA = UnpackNormal(tex2D(_NormalMap, uvA));
                float3 nB = UnpackNormal(tex2D(_NormalMap, uvB));
                float2 distortion = (nA.xy + nB.xy) * _Refraction;

                // Re-sample with distorted UVs so the normal lookup itself wobbles —
                // gives the surface the rippling-water feel without sampling the scene.
                float3 nFinal = UnpackNormal(tex2D(_NormalMap, uvA + distortion));

                // Caustic highlights from where the two normals constructively peak.
                float caustic = saturate(pow(saturate(nA.z * nB.z), 8.0)) * _Caustics;

                // Radial vignette to darken the screen edges with _DeepColor.
                float2 centered = i.uv * 2.0 - 1.0;
                float  edge     = saturate(dot(centered, centered) * _Distance);
                fixed3 baseCol  = lerp(_Color.rgb, _DeepColor.rgb, edge);

                fixed4 col;
                col.rgb = baseCol + caustic.xxx + distortion.x * 0.15;
                col.a   = _Alpha;
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}

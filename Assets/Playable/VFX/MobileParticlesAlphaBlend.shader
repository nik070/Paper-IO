Shader "Mobile/Particles/Alpha Blend"
{
    // Lightweight alpha-blended particle/trail/line shader.
    // - Multiplies texture by vertex color, so TrailRenderer / LineRenderer color
    //   gradients (per-vertex alpha) drive the soft fade at the trail tail.
    // - No fog, no keywords, no instancing — Luna/WebGL strips reliably and the
    //   vertex color path survives the build pipeline (legacy Particles/* shaders
    //   get swapped at decompile time and frequently lose vertex-alpha modulation).
    // - Optional flat _TintColor for inspector tweaking.

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        ZTest LEqual
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uvMainTex   : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _TintColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = UnityObjectToClipPos(IN.positionOS);
                OUT.uvMainTex   = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color * _TintColor;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return tex2D(_MainTex, IN.uvMainTex) * IN.color;
            }
            ENDCG
        }
    }
}

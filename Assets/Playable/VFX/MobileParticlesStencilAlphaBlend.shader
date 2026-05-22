Shader "Mobile/Particles/Stencil/Alpha Blend"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 1
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _StencilComp ("Stencil Comparison", Float) = 6
        
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
            ReadMask [_StencilReadMask]
        }
        
        ZTest LEqual
        ZWrite Off
        Cull Off

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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : POSITION;
                float4 color : COLOR;
                float2 uvMainTex : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = UnityObjectToClipPos(IN.positionOS);
                OUT.uvMainTex = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 color = tex2D(_MainTex, IN.uvMainTex) * IN.color;
                return color;
            }
            ENDCG
        }
    }
}
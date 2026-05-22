Shader "Unlit/Actor Trail"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Name "Trail Pass"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "PaperUtil.cginc"

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
            sampler2D _NoiseTex;
            float4 _MainTex_ST;
            float4 _NoiseTex_ST;
            float3 _HsvOffset;
            float _ColorDarknessMultiplier;
            float _NoiseClip;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                
                OUT.positionHCS = UnityObjectToClipPos(IN.positionOS);
                
                IN.uv.x *= 0.23; // Some magic number found in shader graph
                OUT.uvMainTex = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float4 color = 1;
                
                float4 mainTexColor = tex2D(_MainTex, IN.uvMainTex);
                color.rgb = Unity_Blend_Overlay_float(IN.color.rgb, mainTexColor.rgb, 0.25);
                color.a = IN.color.a;
                return color;
            }
            ENDCG
        }
    }
}

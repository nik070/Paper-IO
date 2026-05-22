Shader "Custom/Actor Zone Shadow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [Toggle]
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SourceBlend("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DestinationBlend("Destination Blend", Float) = 0
        _RadiusOffset("Radius Offset", Float) = 0
        [Toggle(ZONE_DESTRUCTION_ON)] _ZoneDestructionOn("Zone Destruction On", Float) = 0
        //[Toggle()] _ZoneDestructionOn("Zone Destruction On", Float) = 0
        [NoScaleOffset] _DissolveTex("Dissolve Tex", 2D) = "white" {}
        _Radius("Radius", Float) = 0
        _Origin("Origin", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Pass
        {
            Blend [_SourceBlend] [_DestinationBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ZONE_DESTRUCTION_ON
            #include "UnityCG.cginc"
            #include "PaperZoneDissolveUtil.cginc"

            sampler2D _DissolveTex;
            float _Radius;
            float _RadiusOffset;
            float4 _Origin;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : POSITION;
                #if defined(ZONE_DESTRUCTION_ON)
                float2 uvDissolveNoise : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                #endif
            };

            float4 _Color;
            
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                
                OUT.positionHCS = UnityObjectToClipPos(IN.positionOS);
                
                #if defined(ZONE_DESTRUCTION_ON)
                OUT.positionWS = mul(unity_ObjectToWorld, float4(IN.positionOS.xyz, 1));
                OUT.uvDissolveNoise = OUT.positionWS * DISSOLVE_TEX_TILING;
                #endif

                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float4 color = _Color;
                
                #if defined(ZONE_DESTRUCTION_ON)
                
                DissolveAndClip(_DissolveTex, IN.uvDissolveNoise, _Radius + _RadiusOffset, _Origin, IN.positionWS);
                
                #endif
                
                return color;
            }
            ENDCG
        }
    }
}

Shader "Custom/InvisibleMask_V2"
{
    Properties
    {
        _Inflate ("Inflate", Float) = 0
    }
    
    SubShader
    {
        Pass
        {
            Blend Zero One // Only display what's in color buffer
            ZWrite On
            ZTest Always
            Cull Back
            ColorMask 0
            
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            float _Inflate;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : POSITION;
            };
            
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                IN.positionOS.xyz += IN.normalOS * _Inflate;
                OUT.positionHCS = UnityObjectToClipPos(IN.positionOS.xyz);
                return OUT;
            }
            
            float4 frag (Varyings IN) : COLOR
            {
                return 0;
            }
            
            ENDCG
        }
    }
}
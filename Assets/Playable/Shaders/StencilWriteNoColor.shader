Shader "Custom/StencilWriteNoColor"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 1
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("ZTest", Float) = 4 // LEqual
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull", Float) = 2

        [Enum(Off, 0, On, 1)]
        _ZWrite ("ZWrite", Float) = 1
        _Inflate ("Inflate", Float) = 0
        
    }
    
    SubShader
    {
        Pass
        {
            Name "StencilWritePass"
            
            Stencil
            {
                Ref [_StencilRef]
                WriteMask [_StencilWriteMask]
                Pass Replace
                Comp Always
            }
        
            Cull [_Cull]
            ZTest[_ZTest]
            ZWrite[_ZWrite]
            ColorMask 0
            
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float _Inflate;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            float4 _Color;

            float4 vert (appdata IN) : SV_POSITION
            {
                IN.vertex.xyz += IN.normal.xyz * _Inflate;
                return UnityObjectToClipPos(IN.vertex);
            }

            float4 frag () : SV_Target
            {
                return 0;
            }
            ENDCG
            
        }
    }

    FallBack Off
}

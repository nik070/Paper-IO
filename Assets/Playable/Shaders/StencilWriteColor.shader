Shader "Custom/StencilWriteColor"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader
    {
        Pass
        {
            Name "StencilWriteColor"
            
            Stencil
            {
                Ref [_StencilRef]
                WriteMask [_StencilWriteMask]
                Pass Replace
                Comp Always
            }
        
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 _Color;

            float4 vert (float4 v : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(v);
            }

            float4 frag () : SV_Target
            {
                return _Color;
            }
            ENDCG
            
        }
    }

    FallBack Off
}

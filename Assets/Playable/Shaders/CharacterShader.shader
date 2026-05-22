Shader "Custom/CharacterShader"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.1,0.1,0.1,1)
        _OutlineWidth("Outline Width", Range(0,0.01)) = 0.00024

        _MainTex ("Texture", 2D) = "white" {}
        _BlinkColor ("Blink Color", Color) = (1, 0, 0, 1)
        _FadeStart ("Fade Start (Y)", Float) = 0.0
        _FadeEnd ("Fade End (Y)", Float) = 1.0
        _BlinkDuration ("Blink Duration (sec)", Float) = 1.0
        _BlinkIntensity ("Blink Intensity", Float) = 1.0
        _Blinking ("Enable Blinking (0 or 1)", Float) = 1.0
        _BlinkTimeOffset ("Time Offset", Float) = 0.0
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" }

        // OUTLINE PASS
        Pass
        {
            Name "OUTLINEPASS"
            ZWrite On
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal);
                float camDist = distance(UnityObjectToWorldDir(v.vertex), _WorldSpaceCameraPos);
                v.vertex.xyz += norm * camDist * _OutlineWidth;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // MAIN BLINKING PASS
        Pass
        {
            Name "BLINKINGPASS"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float blinkFactor : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _BlinkColor;
            float _FadeStart;
            float _FadeEnd;
            float _BlinkDuration;
            float _BlinkIntensity;
            float _Blinking;
            float _BlinkTimeOffset;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Vertical fade effect
                float fadeFactor = saturate((o.worldPos.y - _FadeStart) / (_FadeEnd - _FadeStart));

                // Blinking logic using duration and enable flag
                float blinkCycle = abs(sin(6.2831 * (_Time.y - _BlinkTimeOffset) / max(_BlinkDuration, 0.0001)));
                float blinkFactor = blinkCycle * _Blinking * _BlinkIntensity;
                
                o.blinkFactor = (1.0 - fadeFactor) * blinkFactor;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                fixed4 finalColor = lerp(texColor, _BlinkColor, i.blinkFactor);
                return finalColor;
            }
            ENDCG
        }
    }
}

Shader "Unlit/ZoneTransitionOverlay"
{
    Properties
    {

        _Radius ("Radius", Float) = 0
        _Origin ("Origin", Vector) = (0,0,0,0)

        [Header(Line)]
        [Space(10)]
        _LineWidth ("Line Width", Float) = 0.75
        _LineFrequency ("Line Frequency", Float) = 1.2
        _LineAmplitude ("Line Amplitude", Float) = 0.1
        _LineStrength ("Line Boost", Float) = 2

        [Header(Glow)]
        [Space(10)]

        _GlowWidth ("Glow Width", Float) = 2.5
        _GlowStrength ("Glow Strength", Float) = 0.3
        _GlowOffset ("Glow Offset", Float) = 0.5

        [Header(Wave)]
        [Space(10)]

        _WaveWidth ("Wave Width", Float) = 1.5
        _WaveStrength ("Wave Strength", Float) = 0.035
        _WaveFrequency ("Wave Frequency", Float) = 0.7
        _WaveAmplitude ("Wave Amplitude", Float) = 0.5
        _WaveOffsetOuter ("Wave Offset Outer", Float) = 1
        _WaveOffsetInner ("Wave Offset Inner", Float) = 0.1

        [Header(Sparkles)]
        [Space(10)]
        _MainTex ("Sparkles", 2D) = "white" {}
        _SparklesWidth ("Sparkles Width", Float) = 2
        _SparklesOffset ("Sparkles Offset", Float) = 0
        _SparklesStrength ("Sparkles Strength", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent"
        }
        LOD 100

        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 positionWS : TEXCOORD1;
            };

            float4 _Origin;
            float _Radius;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _LineWidth;
            float _LineFrequency;
            float _LineAmplitude;
            float _LineStrength;

            float _GlowWidth;
            float _GlowStrength;
            float _GlowOffset;

            float _WaveWidth;
            float _WaveStrength;
            float _WaveFrequency;
            float _WaveAmplitude;
            float _WaveOffsetOuter;
            float _WaveOffsetInner;

            float _SparklesWidth;
            float _SparklesOffset;
            float _SparklesStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.positionWS = mul(unity_ObjectToWorld, float4(v.vertex.rgb, 1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float smoothstep010(float e0, float e1, float x)
            {
                float s = smoothstep(e0, e1, x);
                return 0.5 - abs(s - 0.5);
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 color = 0;
                float dist = length(i.positionWS.xy - _Origin.xy);
                float worldPos = i.positionWS.x + i.positionWS.y;
                
                float centerLine = _LineStrength;
                float centerLineWobble = sin(worldPos * _LineFrequency) * _LineAmplitude;
                float centerLineFalloff = smoothstep010(_Radius - _LineWidth, _Radius + _LineWidth, dist - centerLineWobble);
                centerLine *= centerLineFalloff;
                
                float glow = _GlowStrength;
                float glowFalloff = smoothstep010(_Radius - _GlowWidth, _Radius + _GlowWidth, dist - _GlowOffset);
                glow *= glowFalloff;
                
                float wave = _WaveStrength;
                float waveWidth = _WaveWidth;
                waveWidth += sin(worldPos * _WaveFrequency) * _WaveAmplitude;
                float waveFalloff = step(_Radius, dist + waveWidth + _WaveOffsetInner);
                waveFalloff *= 1-step(_Radius, dist - waveWidth + _WaveOffsetOuter);
                wave *= waveFalloff;
                
                float sparkles = tex2D(_MainTex, i.uv).a;
                float sparklesFalloff = smoothstep010(_Radius - _SparklesWidth, _Radius + _SparklesWidth, dist + _SparklesOffset);
                sparkles *= sparklesFalloff * _SparklesStrength;
                color += centerLine + glow + wave + sparkles;
                
                return color;
            }
            ENDCG
        }
    }
}
    Shader "Custom/Actor Zone"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _StencilComp ("Stencil Comparison", Float) = 8
        
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("ZTest", Float) = 4
        [Toggle]
        _ZWrite ("ZWrite", Float) = 1
        
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
        
        [Toggle(HUE_SHIFT_ON)] _HueShiftOn("Hue Shift On", Float) = 0
        _HueOffset("Hue Offset", Range(-1,1)) = 0
        _SaturationOffset("Saturation Offset", Range(-1,1)) = 0
        _ValueOffset("Value Offset", Range(-1,1)) = 0
        
        [Toggle(ZONE_TRANSITION_ON)] _ZoneTransitionOn("Zone Transition On", Float) = 0
        _Origin("Origin", Vector) = (0,0,0,0)
        _Radius("Radius", Float) = 1
        
        [Toggle(ZONE_CREATION_ON)] _ZoneCreationOn("Zone Creation On", Float) = 0
        _ZoneCreationNoiseTex("Zone Creation Noise Tex", 2D) = "black" {}
        _ZoneCreationNoiseTiling("Zone Creation Noise Tiling", Float) = 0.1
        _ZoneCreationOverlayTex("Zone Creation Overlay Tex", 2D) = "black" {}
        _ZoneCreationOverlayTiling("Zone Creation Overlay Tiling", Float) = 1
        _ZoneCreationOverlayIntensity("Zone Creation Overlay Intensity", Float) = 0.25
        _ZoneCreationBorderIntensity("Zone Creation Border Intensity", Float) = 0.25
        _ZoneCreationInflation("Zone Creation Inflation", Float) = 0
        
        
        [Toggle(COMPLEX_ON)] _ComplexOn("Complex On", Float) = 0
        [NoScaleOffset] _BackgroundTex ("Background", 2D) = "black" {}
        _BackgroundTiling("Background Tiling", Float) = 1
        _BackgroundParallax("Background Parallax", Float) = 0
        
        [NoScaleOffset] _Pattern01 ("Pattern 01", 2D) = "black" {}
        _Pattern01Tiling("Pattern 01 Tiling", Float) = 1
        _Pattern01Parallax("Pattern 01 Parallax", Float) = 0
        _Pattern01Color("Pattern 01 Color", Color) = (1,1,1,1)
        _Pattern01Intensity("Pattern 01 Intensity", Float) = 1
        
        [NoScaleOffset] _Pattern02 ("Pattern 02", 2D) = "black" {}
        _Pattern02Tiling("Pattern 02 Tiling", Float) = 1
        _Pattern02Parallax("Pattern 02 Parallax", Float) = 0
        _Pattern02Color("Pattern 02 Color", Color) = (1,1,1,1)
        _Pattern02Intensity("Pattern 02 Intensity", Float) = 1
        
        [NoScaleOffset] _Noise ("Noise", 2D) = "white" {}
        _NoiseTiling("Noise Tiling", Float) = 1
        _NoiseSpeed("Noise Speed", Float) = 1
        
        [Toggle(DISTORTION_ON)] _DistortionOn("Distortion On", Float) = 0
        _Wave01("Wave 01", 2D) = "black" {}
        _Wave02("Wave 02", 2D) = "black" {}
        _WaveStrength01("Wave Strength 01", Float) = 1
        _WaveStrength02("Wave Strength 02", Float) = 1
        _WaveStrength0102("Wave Strength 01 02", Float) = 1
        _WaveScaleMaster("Wave Scale Master", Float) = 1
        _WaveSpeedMaster("Wave Speed Master", Float) = 1
        _WaveSpeed01("Wave Speed 01", Vector) = (1,1,0,0)
        _WaveSpeed02("Wave Speed 02", Vector) = (-1,-1,0,0)
        _WaveInfluence01toBase("Wave Influence 01 -> Base", Float) = 1
        _WaveInfluence01to02("Wave Influence 01 -> 02", Float) = 1
        _WaveColorShadow("Wave Color Shadow", Color) = (0,0,0,0)
        _WaveColorLight("Wave Color Light", Color) = (1,1,1,1)
        _BorderGradient("Border Gradient", 2D) = "white" {}
        _BorderGradientStrength("Border Gradient Strength", Float) = 1
        _Matcap("Matcap", 2D) = "black" {}
        _MatcapStrength("Matcap Strength", Float) = 1
        _MatcapRotation("Matcap Rotation", Float) = 0
    }
    
    SubShader
    {
        Cull [_Cull]
        ZTest [_ZTest]
        ZWrite [_ZWrite]
        
        Stencil
        {
            // If we pass this stencil test, we will write 1 to BIT 5 in the stencil buffer.
            // This bit can later be used to render certain things only inside the zones. We can do this
            // because the Ref is containing the zone ID as well as BIT 5. See ZoneBase.SetMaterialsStencilRef(...)
            
            ReadMask 31 // 0b00011111
            Ref [_StencilRef]
            Comp [_StencilComp] // EQUAL
            WriteMask 32 // 0b00100000
            Pass Replace
        }
        
        Pass
        {
            Tags
            {
                "Queue" = "Geometry"
            }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ HUE_SHIFT_ON
            #pragma shader_feature _ COMPLEX_ON
            #pragma multi_compile  _ ZONE_TRANSITION_ON
            #pragma multi_compile  _ ZONE_CREATION_ON
            #pragma shader_feature _ ROUND_ZONE_ON
            #pragma shader_feature _ DISTORTION_ON
            
            #include "UnityCG.cginc"
            #include "PaperUtil.cginc"

            float4 _Color;
            float _HueOffset;
            float _SaturationOffset;
            float _ValueOffset;
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _RoundZoneGradient;
            
            float2 _Origin;
            float _Radius;
            
            sampler2D _BackgroundTex;
            float _BackgroundTiling;
            float _BackgroundParallax;
            
            sampler2D _ZoneCreationNoiseTex;
            sampler2D _ZoneCreationOverlayTex;
            float _ZoneCreationNoiseTiling;
            float _ZoneCreationOverlayTiling;
            float _ZoneCreationOverlayIntensity;
            float _ZoneCreationBorderIntensity;
            float _ZoneCreationInflation;
           
                                    
            sampler2D _Pattern01;         
            float _Pattern01Tiling;
            float _Pattern01Parallax;
            float4 _Pattern01Color;
            float _Pattern01Intensity;
            
            sampler2D _Pattern02;            
            float _Pattern02Tiling;
            float _Pattern02Parallax;
            float4 _Pattern02Color;
            float _Pattern02Intensity;
            
            sampler2D _Wave01;
            sampler2D _Wave02;
            float4 _Wave01_ST;
            float4 _Wave02_ST;
            float2 _WaveSpeed01;
            float2 _WaveSpeed02;
            float _WaveStrength01;
            float _WaveStrength02;
            float _WaveStrength0102;
            float _WaveScaleMaster;
            float _WaveSpeedMaster;
            float _WaveInfluence01toBase;
            float _WaveInfluence01to02;
            float4 _WaveColorLight;
            float4 _WaveColorShadow;
            sampler2D _BorderGradient;
            float4 _BorderGradient_ST;
            float _BorderGradientStrength;
            float _BorderGradientNoiseStrength;
            sampler2D _Matcap;
            float _MatcapStrength;
            float _MatcapRotation;
            
            sampler2D _Noise;
            float _NoiseTiling;
            float _NoiseSpeed;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : POSITION;
                float2 uvMainTex : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                
                #if defined(COMPLEX_ON)
                
                float2 uvBackground : TEXCOORD2;
                float4 uvPattern0102 : TEXCOORD3;
                float2 uvNoise : TEXCOORD4;
                
                #endif
                
                #if defined(DISTORTION_ON)
                float4 uvWave0102 : TEXCOORD5;
                float uvBorderGradient : TEXCOORD6;
                float2 uvMatcap : TEXCOORD7;
                #endif
                
                #if defined(ZONE_CREATION_ON)
                float2 uvZoneCreationNoise : TEXCOORD8;
                float2 uvZoneCreationOverlay : TEXCOORD9;
                #endif
                
            };
            
            float2 WorldUV(float2 positionWS, float tiling, float offset)
            {
                return positionWS * tiling;
            }
            
            float2 ParallaxUV(float2 uv, float tiling, float parallax)
            {
                float2 offset = parallax * _WorldSpaceCameraPos.xy;
                return uv * tiling + offset;
            }
            
            float2 ScrollingUV(float2 uv, float tiling, float speed)
            {
                float offset = _Time.y * speed * 0.1;
                return uv * tiling + offset;
            }
            
            float2 MatcapUV(float3 normalOS)
            {
                float3 normalWS = UnityObjectToWorldNormal(normalOS);
                float2 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                
                float angle = _MatcapRotation;
                
                float s = sin(angle);
                float c = cos(angle);

                float2 rotatedXY = float2(
                    normalVS.x * c - normalVS.y * s,
                    normalVS.x * s + normalVS.y * c
                );

                return rotatedXY;
            }
            
            float BorderGradientUV(float2 uv)
            {
                float uvBorderGradient = TRANSFORM_TEX(uv, _BorderGradient).x;
                return uvBorderGradient;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                
                OUT.uvMainTex = TRANSFORM_TEX(IN.uv0, _MainTex);
                #if defined(COMPLEX_ON)
                
                OUT.uvBackground = ParallaxUV(IN.uv0, _BackgroundTiling, _BackgroundParallax);
                OUT.uvPattern0102.xy = ParallaxUV(IN.uv0, _Pattern01Tiling, _Pattern01Parallax);
                OUT.uvPattern0102.zw = ParallaxUV(IN.uv0, _Pattern02Tiling, _Pattern02Parallax);
                OUT.uvNoise = ScrollingUV(IN.uv0, _NoiseTiling, _NoiseSpeed);
                
                #endif
                
                OUT.positionWS = mul(unity_ObjectToWorld, float4(IN.positionOS.xyz, 1));
                
                #if defined(ZONE_CREATION_ON)
                OUT.uvZoneCreationNoise = WorldUV(OUT.positionWS, _ZoneCreationNoiseTiling, 0);
                OUT.uvZoneCreationOverlay = WorldUV(OUT.positionWS, _ZoneCreationOverlayTiling, _Radius);
                IN.positionOS.xyz += IN.normal.xyz * _ZoneCreationInflation;
                #endif
                
                #if defined(DISTORTION_ON)
                
                _Wave01_ST.xy *= _WaveScaleMaster;
                _Wave01_ST.zw += _Time.y * _WaveSpeed01.xy * _WaveSpeedMaster;
                OUT.uvWave0102.xy = TRANSFORM_TEX(OUT.positionWS.xy, _Wave01);
                
                //_Wave02_ST.xy *= _WaveScaleMaster;
                //_Wave02_ST.zw += _Time.y * _WaveSpeed02.xy * _WaveSpeedMaster;
                //OUT.uvWave0102.zw = TRANSFORM_TEX(OUT.positionWS.xy, _Wave02);
                OUT.uvMatcap = MatcapUV(IN.normal);
                OUT.uvBorderGradient = BorderGradientUV(IN.uv1);
                
                #endif
                
                OUT.positionHCS = UnityObjectToClipPos(IN.positionOS);
                
                return OUT;
            }
            
            float Matcap(float2 uvMatcap)
            {
                float matcap = tex2D(_Matcap, uvMatcap);
                return matcap * _MatcapStrength;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 color = 0;
                float baseWarp = 0;
                float wave02Warp = 0;
                
                #if defined(DISTORTION_ON)
                
                float2 uvWave01 = IN.uvWave0102.xy;
                //float2 uvWave02 = IN.uvWave0102.zw;
                
                float waveNoise01 = tex2D(_Wave01, uvWave01);
                waveNoise01 = waveNoise01 * 2 - 1;
                baseWarp = waveNoise01 * _WaveInfluence01toBase;
                //wave02Warp = waveNoise01 * _WaveInfluence01to02;
                
                #endif
                
                #if !defined(COMPLEX_ON)
                color = tex2D(_MainTex, IN.uvMainTex + baseWarp) * _Color;
                #endif
                
                #if defined(COMPLEX_ON)
                
                // Texture sampling
                float4 backgroundColor = tex2D(_BackgroundTex, IN.uvBackground + baseWarp);
                float pattern01 = tex2D(_Pattern01, IN.uvPattern0102.xy).a;
                float4 pattern02Color = tex2D(_Pattern02, IN.uvPattern0102.zw);
                
                // Noise
                float noise = tex2D(_Noise, IN.uvNoise).r;
                noise = smoothstep(0.2, 0.8, noise);
                
                // Pattern 01
                float combinedPattern01 = pattern01 * pattern01 * noise * _Pattern01Intensity;
                float4 pattern01Color = combinedPattern01 * _Pattern01Color;
                
                // Pattern 02
                float pattern02Noise = pattern02Color.a * noise * _Pattern02Intensity;
                pattern02Color *= pattern02Noise * _Pattern02Color;
                
                color = backgroundColor + pattern01Color + pattern02Color;
                
                #endif
                
                
                #if defined(HUE_SHIFT_ON)
                
                float3 hsv = Unity_ColorspaceConversion_RGB_HSV_float(color.rgb);
                hsv.x += _HueOffset;
                hsv.y += _SaturationOffset;
                hsv.z += _ValueOffset;
                color.rgb = Unity_ColorspaceConversion_HSV_RGB_float(hsv);
                
                #endif
                
                #if defined(ZONE_TRANSITION_ON) 
                
                float dist = distance(_Origin.xy, IN.positionWS.xy);
                float alpha = step(_Radius, dist);
                clip(alpha - 0.5);
                
                #endif
                
                
               
                #if defined(DISTORTION_ON)
                
                //float waveNoise02 = tex2D(_Wave02, uvWave02 + wave02Warp);
                //waveNoise02 = waveNoise02 * 2 - 1;
                
				float finalNoise = 0;
			    finalNoise += waveNoise01 * _WaveStrength01;
                //finalNoise += waveNoise02 * _WaveStrength02;
                //finalNoise += waveNoise01 * waveNoise02 * _WaveStrength0102;
    
                finalNoise = finalNoise * 0.5 + 0.5;
                float4 finalNoiseColor = lerp(_WaveColorShadow, _WaveColorLight, finalNoise);
                finalNoiseColor = finalNoiseColor * 2 - 1;
                color += finalNoiseColor;
                
                if (IN.uvBorderGradient < 0.99999)
                {
                    float4 borderGradient = tex2D(_BorderGradient, IN.uvBorderGradient); 
                    borderGradient = lerp(1, borderGradient, _BorderGradientStrength);
                    color *= borderGradient;
                }
                
                color += Matcap(IN.uvMatcap);
                
                // // Debug border UV's
                // color.rgb = 0;
                // color.g = IN.uvBorderGradient.x;
                
                #endif

                #if defined(ZONE_CREATION_ON)
                
                float offset = tex2D(_ZoneCreationNoiseTex, IN.uvZoneCreationNoise).r;
                float overlay = tex2D(_ZoneCreationOverlayTex, IN.uvZoneCreationOverlay).r;
                float distance = length(IN.positionWS.xy - _Origin.xy);
                
                float wobbleDistance = distance + offset * 1.5;
                float alphaClip = step(_Radius, wobbleDistance);
                overlay *= wobbleDistance / _Radius;
                overlay *= smoothstep(wobbleDistance + 10, wobbleDistance, _Radius) * _ZoneCreationOverlayIntensity;
                overlay += smoothstep(wobbleDistance + .5, wobbleDistance + .3, _Radius) * _ZoneCreationBorderIntensity;
                color.rgb += overlay;
                float mask = 0.5 - alphaClip;
                clip(mask);
                
                #endif
                
                return color;
            }
            ENDCG
        }
    }
    FallBack Off
    
    CustomEditor "ActorZoneInspector"
}

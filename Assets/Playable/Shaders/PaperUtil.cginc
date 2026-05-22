#pragma once

float3 Unity_ColorspaceConversion_RGB_HSV_float(float3 In)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
    float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float  E = 1e-10;
    return float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), Q.x);
}

float3 Unity_ColorspaceConversion_HSV_RGB_float(float3 In)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 P = abs(frac(In.xxx + K.xyz) * 6.0 - K.www);
    return In.z * lerp(K.xxx, saturate(P - K.xxx), In.y);
}

float3 Unity_Blend_Overlay_float(float3 Base, float3 Blend, float Opacity)
{
    float3 result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
    float3 result2 = 2.0 * Base * Blend;
    float3 zeroOrOne = step(Base, 0.5);
    float3 outResult = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    return lerp(Base, outResult, Opacity);
}

float smoothstep010(float e0, float e1, float x)
{
    float s = smoothstep(e0, e1, x);
    return 0.5 - abs(s - 0.5);
}

float CheapStep(float edge0, float edge1, float val)
{
    float range = edge1 - edge0;
    return saturate((val - edge0) / (abs(range) < 1e-6 ? 1e-6 : range));
}
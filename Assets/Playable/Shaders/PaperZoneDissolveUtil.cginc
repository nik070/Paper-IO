#pragma once

#define DISSOLVE_TEX_TILING 0.06
#define DISSOLVE_SIZE 7

struct DissolveResult
{
    float noiseTex;
    float normalizedDissolve;
};

DissolveResult DissolveAndClip(sampler2D dissolveTexSampler, float2 uv, float radius, float2 origin, float2 positionWS)
{
    float dissolveTex = tex2D(dissolveTexSampler, uv);
    float distanceToOrigin = distance(origin, positionWS.xy);

    float lowEdge = radius - DISSOLVE_SIZE;
    float highEdge = radius + DISSOLVE_SIZE;

    float val = distanceToOrigin - radius;

    float dissolve = smoothstep(lowEdge, highEdge, val);
    float noise = 1 - step(dissolve, dissolveTex);

    clip(noise - 0.5);

    DissolveResult result;
    result.noiseTex = dissolveTex;
    result.normalizedDissolve = dissolve;

    return result;
}

float4 ColorizeDissolvedArea(float4 color, DissolveResult result)
{
    float dissolveTex = result.noiseTex;
    float dissolve = result.normalizedDissolve;

    float glow = 0;
    glow += 1 - smoothstep(dissolveTex, saturate(dissolveTex + 0.6), dissolve);
    glow *= .3;

    float foamMask = 1 - step(saturate(dissolveTex + 0.05), dissolve);

    color.rgb += foamMask * 1;
    color.rgb += glow * (1 - foamMask);

    return color;
}

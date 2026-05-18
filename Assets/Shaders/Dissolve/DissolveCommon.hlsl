#ifndef DICEWARS_DISSOLVE_COMMON_INCLUDED
#define DICEWARS_DISSOLVE_COMMON_INCLUDED

float DissolveHash21(float2 p)
{
    p = frac(p * float2(234.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

float DissolveValueNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);

    float a = DissolveHash21(i);
    float b = DissolveHash21(i + float2(1.0, 0.0));
    float c = DissolveHash21(i + float2(0.0, 1.0));
    float d = DissolveHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float DissolveFbm3(float2 p)
{
    float sum = 0.0;
    float amp = 0.52;
    float2 q = p;
    [unroll] for (int i = 0; i < 3; i++)
    {
        sum += DissolveValueNoise(q) * amp;
        q *= 2.07;
        amp *= 0.5;
    }
    return sum;
}

float DissolveProceduralField(float3 positionOS, float2 uv, float proceduralScale, float proceduralRandomness)
{
    float scale = max(proceduralScale, 1e-5);
    float2 p = positionOS.xy * scale + positionOS.z * scale * 0.37;
    p += uv * scale * 0.15;

    float n = DissolveFbm3(p + float2(2.71, -5.3));
    n = saturate(pow(max(n, 1e-4), lerp(0.95, 0.62, saturate(proceduralRandomness))));
    return saturate(n * 0.97 + 0.015);
}

// Dissolve boundary glow at the noise cutoff.
void ApplyDissolveEdgeGlow(
    float dissolveNoise,
    float dissolveAmount,
    float edgeSoftness,
    float dissolveEdgeWidth,
    half4 dissolveEdgeColor,
    float dissolveEdgeIntensity,
    out half3 edgeRgb)
{
    float edge = max(edgeSoftness, 1e-5);
    float w = edge * max(dissolveEdgeWidth, 0.01);

    clip(dissolveNoise - dissolveAmount + 1e-5);

    float dist = abs(dissolveNoise - dissolveAmount);
    half nearEdge = (half)saturate(1.0 - smoothstep(0.0, w, dist));
    half edgeWeight = (half)saturate(dissolveEdgeColor.a * dissolveEdgeIntensity);
    edgeRgb = dissolveEdgeColor.rgb * (nearEdge * edgeWeight);
}

#endif

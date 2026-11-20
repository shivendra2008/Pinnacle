// Rain on a window seen from inside, at night (the Curator's Gallery). Written for this project.
// The pane shows the night sky outside (a gradient with drifting cloud structure that lights up when _Flash is
// raised by GalleryStorm), refracted through two layers of water: beads that sit on the glass, and drops that run
// down it in columns, stopping and starting, leaving a trail of small beads. The candles inside catch on the drops
// as small warm glints. Between the drops the glass is dark and mostly see-through, so the rain particles outside
// show through it. Unlit, transparent, no screen grab: the cost is one full-screen-area fragment per window pixel.
Shader "Pinnacle/RainGlass"
{
    Properties
    {
        _SkyTop ("Sky top", Color) = (0.030, 0.038, 0.060, 1)
        _SkyBottom ("Sky bottom", Color) = (0.055, 0.060, 0.075, 1)
        _FlashColor ("Lightning colour", Color) = (0.72, 0.80, 1.0, 1)
        _Flash ("Lightning", Range(0, 6)) = 0
        _Glint ("Glint colour", Color) = (1.0, 0.72, 0.42, 1)
        _Size ("Pane size (m)", Vector) = (2.4, 5.8, 0, 0)
        _Drops ("Bead density", Range(0, 1)) = 0.55
        _Speed ("Running speed", Range(0, 3)) = 1
        _GlassAlpha ("Clear glass opacity", Range(0, 1)) = 0.32
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SkyTop, _SkyBottom, _FlashColor, _Glint;
            float _Flash, _Drops, _Speed, _GlassAlpha;
            float4 _Size;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float hash12(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float2 hash22(float2 p) { float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973)); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.xx + p3.yz) * p3.zy); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash12(i), hash12(i + float2(1, 0)), u.x), lerp(hash12(i + float2(0, 1)), hash12(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p) { float s = 0, a = 0.5; for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; } return s; }

            // the night outside: a gradient, clouds moving slowly, lit from within when lightning flashes
            float3 sky(float2 uv)
            {
                float n = fbm(uv * float2(1.6, 1.1) + float2(_Time.y * 0.012, _Time.y * 0.004));
                float3 c = lerp(_SkyBottom.rgb, _SkyTop.rgb, saturate(uv.y));
                c *= 0.7 + 0.6 * n;
                c += _FlashColor.rgb * _Flash * (0.25 + 1.1 * n * n);
                return c;
            }

            // beads sitting on the glass: xy = how they bend the view, z = coverage
            float3 beads(float2 p, float cell, float density)
            {
                float2 q = p / cell, id = floor(q), f = frac(q) - 0.5;
                float2 c = (hash22(id) - 0.5) * 0.55;
                float r = 0.12 + 0.24 * hash12(id + 7.13);
                float on = step(1.0 - density, hash12(id + 3.31));
                float2 d = f - c; float l = length(d);
                float m = smoothstep(r, r * 0.55, l) * on;
                return float3(d / r * m, m);
            }

            // drops running down in columns, with a trail of small beads above each
            float3 runners(float2 p, float colW)
            {
                float col = floor(p.x / colW);
                float h = hash12(float2(col, 4.7));
                float fx = frac(p.x / colW) - 0.5;
                // stick-slip: mostly still, then a quick slide
                float t = _Time.y * _Speed * (0.25 + 0.35 * h) + h * 31.0;
                float k = frac(t), slide = floor(t) + smoothstep(0.55, 1.0, k);
                float span = _Size.y + 0.6;
                float y = _Size.y + 0.3 - frac(slide * (0.18 + 0.2 * h) / span + h) * span;
                float wob = sin(p.y * 7.0 + h * 40.0) * 0.12;
                float2 d = float2((fx - wob) * colW, p.y - y);
                float r = colW * (0.18 + 0.1 * h);
                float l = length(d * float2(1.0, 0.75));
                float m = smoothstep(r, r * 0.5, l);
                // the trail: thin beads above the drop, fading upwards
                float above = p.y - y;
                float trailOn = step(0.0, above) * smoothstep(0.9, 0.0, above);
                float tq = frac(p.y / (colW * 0.32)) - 0.5;
                float2 td = float2((fx - wob) * colW, tq * colW * 0.32);
                float tm = smoothstep(r * 0.35, r * 0.15, length(td)) * trailOn;
                float cov = max(m, tm * 0.8);
                float2 n = l < r ? d / r : td / max(r * 0.35, 1e-4);
                return float3(n * cov, cov);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * _Size.xy;                             // metres on the pane
                float3 b1 = beads(p, 0.055, _Drops);
                float3 b2 = beads(p + 0.37, 0.09, _Drops * 0.6);
                float3 rn = runners(p, 0.11);
                float cov = saturate(max(max(b1.z, b2.z * 0.9), rn.z));
                float2 bend = (b1.xy * b1.z + b2.xy * b2.z + rn.xy * rn.z) * 0.06;
                float3 outside = sky(i.uv + bend);
                // water: brighter, inverted image of the sky; a warm glint from the candles in the room
                float3 nrm = normalize(float3(bend * 12.0, 1.0));
                float glint = pow(saturate(dot(nrm, normalize(float3(-0.35, -0.5, 0.8)))), 28.0) * cov;
                float3 water = outside * 1.6 + _Glint.rgb * glint * 0.45 + _FlashColor.rgb * _Flash * cov * 0.25;
                float3 col = lerp(outside, water, cov);
                float a = lerp(_GlassAlpha, 0.92, cov);
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}

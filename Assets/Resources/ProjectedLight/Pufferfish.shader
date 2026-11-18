Shader "Brick2Life/Pufferfish"
{
    Properties
    {
        _Color ("Effect tint and opacity", Color) = (1,1,1,1)
        _Hit ("Hit reaction", Range(0,1)) = 0
        _Burst ("Defeated bubbles", Float) = 0
        _SwimPhase ("Swim phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Hit, _Burst, _SwimPhase;

            float Fill(float d) { return 1.0 - smoothstep(-0.023, 0.023, d); }
            float Oval(float2 p, float2 center, float2 radii)
            {
                return Fill((length((p - center) / radii) - 1.0) * min(radii.x, radii.y));
            }

            float Line(float2 p, float2 a, float2 b, float radius)
            {
                float2 delta = b - a;
                float u = saturate(dot(p - a, delta) / dot(delta, delta));
                return Fill(length(p - a - u * delta) - radius);
            }

            float4 Paint(float4 under, float3 color, float mask)
            {
                return float4(lerp(under.rgb, color, mask), under.a + (1.0 - under.a) * mask);
            }

            float4 Bubble(float4 art, float2 p, float2 center, float radius)
            {
                float d = length(p - center);
                float edge = Fill(abs(d - radius * 0.81) - radius * 0.19);
                art = Paint(art, float3(0.54, 0.37, 0.90), 0.34 * Fill(d - radius));
                art = Paint(art, float3(0.65, 0.89, 1.0), edge);
                return Paint(art, float3(0.98, 0.92, 1.0),
                    Fill(length(p - center + float2(radius * 0.29, -radius * 0.30)) - radius * 0.24));
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float4 art = 0;
                if (_Burst > 0.5)
                {
                    art = Bubble(art, p, float2(-0.28, 0.20), 0.25);
                    art = Bubble(art, p, float2(0.27, 0.29), 0.18);
                    return Bubble(art, p, float2(0.13,-0.34), 0.15) * _Color.a;
                }
                float hit = saturate(_Hit);
                p *= lerp(float2(1,1), float2(0.94, 1.25), hit);
                float tailOffset = 0.13 * _SwimPhase;
                float tail = max(Oval(p, float2(tailOffset - 0.10,-0.76), float2(0.13,0.15)),
                    Oval(p, float2(tailOffset + 0.10,-0.77), float2(0.13,0.14)));
                tail = max(tail, Line(p,float2(0,-0.51),float2(tailOffset,-0.77),0.095));
                art = Paint(art, float3(0.63, 0.39, 0.91), tail);
                float2 mirror = float2(abs(p.x),p.y);
                float fins = Line(mirror,float2(0.50,-0.23),float2(0.83,-0.35 + 0.08 * _SwimPhase),0.085);
                art = Paint(art, float3(0.91,0.58,0.96), fins);
                float spikes = Line(mirror,float2(0.58,0.29),float2(0.77,0.43),0.082);
                spikes = max(spikes, Line(mirror,float2(0.34,0.55),float2(0.47,0.78),0.070));
                spikes = max(spikes, Line(mirror,float2(0.51,-0.31),float2(0.70,-0.44),0.066));
                spikes = max(spikes, Line(p,float2(-0.018,0.62),float2(0.021,0.86),0.064));
                art = Paint(art, float3(0.63,0.30,0.83), spikes);
                float body = Oval(p, float2(0,0.06), float2(0.69,0.66));
                art = Paint(art, float3(0.32,0.14,0.52), body);
                float inside = Oval(p, float2(-0.012,0.09), float2(0.61,0.58));
                float3 purple = lerp(float3(0.65,0.32,0.90),float3(0.93,0.54,0.97),smoothstep(-0.4,0.70,p.y));
                art = Paint(art, lerp(purple,float3(0.98,0.55,0.72),hit), inside);
                float belly = Oval(p, float2(0,-0.10), float2(0.49,0.37));
                art = Paint(art, float3(1.0,0.77,0.74), belly);
                float cheeks = Oval(mirror, float2(0.34,-0.078), float2(0.092,0.061));
                art = Paint(art, float3(1.0,0.51,0.61), cheeks);
                float3 ink = float3(0.16,0.078,0.25);
                float eye = Oval(mirror,float2(0.22,0.20),float2(0.11,0.145));
                float shutEye = max(Line(mirror,float2(0.31,0.25),float2(0.18,0.17),0.029),
                    Line(mirror,float2(0.18,0.17),float2(0.31,0.105),0.029));
                float expression = smoothstep(0.24, 0.62, hit);
                art = Paint(art, ink, lerp(eye, shutEye, expression));
                float eyeGlint = Oval(mirror,float2(0.185,0.252),float2(0.036,0.047)) * (1.0 - expression);
                art = Paint(art, float3(1.0,0.97,0.96), eyeGlint);
                float mouth = Oval(p,float2(0,-0.053),lerp(float2(0.090,0.052),float2(0.088,0.085),hit));
                art = Paint(art, ink, mouth);
                return art * _Color.a;
            }
            ENDCG
        }
    }
}

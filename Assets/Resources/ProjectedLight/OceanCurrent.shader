Shader "Hidden/Brick2Life/OceanCurrent"
{
    Properties
    {
        _MainTex ("Route field", 2D) = "black" {}
        _FlowTime ("Flow time", Float) = 0
        _RouteOpacity ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _FlowTime;
            float _RouteOpacity;

            float4 frag(v2f_img input) : SV_Target
            {
                float4 field = tex2D(_MainTex, input.uv);
                float edge = (1.0 - smoothstep(0.73, 1.0, field.b)) * field.a;
                float distanceToCore = field.r;
                float side = field.g;

                // Two broken strands move at different speeds while the base water stays steady.
                float fast = distanceToCore + _FlowTime * 0.94;
                float slow = distanceToCore + _FlowTime * 0.57;
                float strandA = 1.0 - smoothstep(0.035, 0.16,
                    abs(side - (0.34 + 0.12 * sin(fast * 6.4))));
                float strandB = 1.0 - smoothstep(0.025, 0.12,
                    abs(side + (0.29 + 0.17 * sin(slow * 4.8 + 1.5))));
                strandA *= smoothstep(-0.45, 0.7, sin(fast * 5.9));
                strandB *= smoothstep(-0.55, 0.8, sin(slow * 7.3 + 1.3));
                float streams = max(strandA, strandB * 0.75);

                // Decreasing distance is downstream. Repeat every 1.65 cells, even after a merge.
                float spacing = 1.65;
                float along = (0.5 - frac((distanceToCore + _FlowTime * 0.78 + 0.4) / spacing)) * spacing;
                float head = max(max(-along, along - 0.20) * 4.0, abs(side) - (0.20 - along) * 3.8);
                float arrow = 1.0 - smoothstep(-0.038, 0.06, head);
                float tailSide = 0.18 * sin(along * 11.0);
                float tail = (1.0 - smoothstep(0.05, 0.18, abs(side - tailSide))) *
                    smoothstep(-0.30, -0.21, along) * (1.0 - smoothstep(0.02, 0.07, along));
                float foam = max(arrow, tail * 0.78);

                float3 water = lerp(float3(0.10, 0.58, 0.57), float3(0.22, 0.85, 0.79), streams);
                float3 color = lerp(water, float3(0.54, 1.0, 0.88), foam);
                float opacity = edge * _RouteOpacity * max(0.31 + streams * 0.33, foam * 0.92);
                return float4(color * opacity, opacity);
            }
            ENDCG
        }
    }
}

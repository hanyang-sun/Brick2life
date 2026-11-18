Shader "Brick2Life/CoralWall"
{
    Properties
    {
        _Color ("Wall tint and intensity", Color) = (1,1,1,1)
        _Exposed ("Exposed sides (W E S N)", Vector) = (1,1,1,1)
        _InnerCorners ("Inner corners (SW SE NW NE)", Vector) = (0,0,0,0)
        _Nearby ("Enemy proximity (W E S N)", Vector) = (0,0,0,0)
        _ShowCrest ("Show shield crest", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color, _Exposed, _InnerCorners, _Nearby;
            float _ShowCrest;

            float Stroke(float distance, float width)
            {
                return 1.0 - smoothstep(width - 0.004, width + 0.006, distance);
            }

            float Branch(float2 p, float2 a, float2 b, float width)
            {
                float2 direction = b - a;
                float t = saturate(dot(p - a, direction) / dot(direction, direction));
                return Stroke(length(p - a - direction * t), width * lerp(1.43, 0.84, t));
            }

            float Battlement(float2 p, float exposed)
            {
                float tooth = abs(abs(p.x - 0.5) - 0.198) - 0.112;
                float wall = min(p.y - 0.231, max(p.y - 0.316, tooth));
                return exposed * (1.0 - smoothstep(-0.008, 0.008, wall));
            }

            float Cap(float2 p)
            {
                float2 lower = lerp(-0.13, 0.158, _Exposed.xz);
                float2 upper = lerp(1.13, 0.842, _Exposed.yw);
                float2 d = abs(p - (lower + upper) * 0.5) - (upper - lower) * 0.5 + 0.082;
                float distance = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - 0.082;
                return 1.0 - smoothstep(-0.008, 0.008, distance);
            }
            float2 Staghorn(float2 p)
            {
                float stem = Branch(p, float2(0,0), float2(0.026,0.139), 0.031);
                float arms = max(Branch(p, float2(0.01,0.047), float2(-0.079,0.106), 0.026),
                    Branch(p, float2(0.015,0.072), float2(0.083,0.106), 0.026));
                float tips = Branch(p, float2(-0.058,0.090), float2(-0.086,0.154), 0.024);
                tips = max(tips, Branch(p, float2(0.021,0.114), float2(0.007,0.191), 0.025));
                tips = max(tips, Branch(p, float2(0.064,0.100), float2(0.102,0.146), 0.023));
                return float2(max(max(stem, arms), tips), tips * smoothstep(0.105, 0.167, p.y));
            }

            float Fan(float2 p)
            {
                float radius = length(p);
                float angle = atan2(p.x, max(0.001, p.y));
                float ribs = 1.0 - smoothstep(0.13, 0.28, abs(frac(angle * 2.3 + 0.5) - 0.5));
                float scallop = 0.113 + 0.011 * cos(angle * 14.5);
                float rim = Stroke(abs(radius - scallop + 0.007), 0.012);
                float spread = smoothstep(0.0, 0.012, p.y - abs(p.x) * 0.28);
                return spread * (1.0 - smoothstep(scallop - 0.006, scallop + 0.008, radius)) *
                    max(rim, lerp(0.48, 0.95, ribs));
            }

            float Tube(float2 p, float2 base, float2 top, float width)
            {
                float body = Branch(p, base, top, width * 0.69);
                float distance = length((p - top) * float2(1, 1.55));
                float lip = Stroke(abs(distance - width * 0.72), width * 0.30);
                float mouth = Stroke(distance, width * 0.50);
                return max(0.56 * body * (1.0 - 0.90 * mouth), lip);
            }

            // Masks for branching coral, sea fans, tube coral and tips affected by nearby enemies.
            float4 SideCoral(float2 edge, float exposed)
            {
                if (exposed < 0.5 || edge.y > 0.242) return 0;
                float2 p = float2(edge.x, 0.209 - edge.y);
                float2 branching = Staghorn((p - float2(0.226, 0.014)) * float2(1.28, 1.24));
                float2 fanPosition = (p - float2(0.489, 0.023)) * float2(1.0, 0.86);
                float fan = Fan(fanPosition);
                float tubes = Tube(p, float2(0.770,0.010), float2(0.725,0.104), 0.036);
                tubes = max(tubes, Tube(p, float2(0.775,0.019), float2(0.787,0.148), 0.041));
                tubes = max(tubes, Tube(p, float2(0.793,0.010), float2(0.846,0.086), 0.036));
                float tip = max(branching.y, fan * smoothstep(0.079, 0.108, length(fanPosition)));
                return float4(branching.x, fan, tubes, tip);
            }

            float3 CornerCoral(float2 p, float outer, float inner)
            {
                if (outer + inner < 0.5 || max(p.x,p.y) > 0.29) return 0;
                float2 d = p - 0.175;
                float2 diagonal = float2(d.x - d.y, -d.x - d.y) * 0.7071;
                float2 branch = Staghorn(diagonal) * outer;
                float shell = Fan(diagonal * float2(1.02, 0.96)) * inner;
                return float3(branch.x, shell, branch.y);
            }
            float ShieldDistance(float2 p)
            {
                float shoulder = 0.202 - 0.030 * smoothstep(0.126, 0.192, p.y);
                float width = shoulder * lerp(0.04, 1.0, smoothstep(-0.211, -0.024, p.y));
                return max(abs(p.x) - width, max(p.y - 0.187, -0.210 - p.y));
            }

            float ShellEmblem(float2 p)
            {
                float2 fanPoint = p + float2(0, 0.068);
                float angle = atan2(fanPoint.x, max(fanPoint.y, 0.001));
                float radius = 0.185 + 0.007 * cos(angle * 10.5);
                float fan = (1.0 - smoothstep(radius - 0.007, radius + 0.007, length(fanPoint))) *
                    smoothstep(-0.006, 0.008, fanPoint.y - 0.31 * abs(fanPoint.x));
                float groove = Branch(float2(abs(fanPoint.x), fanPoint.y),
                    float2(0.019, 0.045), float2(0.056, 0.127), 0.013);
                float2 hinge = abs(fanPoint + float2(0, 0.009)) - float2(0.043, 0.018);
                float foot = 1.0 - smoothstep(-0.005, 0.007, max(hinge.x, hinge.y));
                return max(fan * (1.0 - 0.76 * groove), foot);
            }

            float3 AddShieldCrest(float3 color, float2 uv)
            {
                if (_ShowCrest < 0.5) return color;

                float2 crestUV = (uv - 0.5) * 0.89;
                float distance = ShieldDistance(crestUV);
                float margin = 1.0 - smoothstep(0.040, 0.052, distance);
                float shield = 1.0 - smoothstep(-0.009, 0.009, distance);
                color = lerp(color, float3(0.20, 0.012, 0.57), margin);
                float inset = 1.0 - smoothstep(-0.034, -0.022, distance);
                float3 badge = lerp(float3(0.94, 0.31, 0.08), float3(0.08, 0.012, 0.26), inset);
                badge = lerp(badge, float3(1.0, 0.67, 0.21), ShellEmblem(crestUV));
                return lerp(color, badge, shield);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 p = input.uv;
                float2 west = p.yx, east = 1.0 - p.yx, south = p, north = 1.0 - p;
                float cap = Cap(p);
                float blocks = max(max(Battlement(west, _Exposed.x), Battlement(east, _Exposed.y)),
                    max(Battlement(south, _Exposed.z), Battlement(north, _Exposed.w)));
                float4 w = SideCoral(west, _Exposed.x), e = SideCoral(east, _Exposed.y);
                float4 s = SideCoral(south, _Exposed.z), n = SideCoral(north, _Exposed.w);
                float3 sw = CornerCoral(p, _Exposed.x * _Exposed.z, _InnerCorners.x);
                float3 se = CornerCoral(float2(1.0-p.x,p.y), _Exposed.y * _Exposed.z, _InnerCorners.y);
                float3 nw = CornerCoral(float2(p.x,1.0-p.y), _Exposed.x * _Exposed.w, _InnerCorners.z);
                float3 ne = CornerCoral(1.0-p, _Exposed.y * _Exposed.w, _InnerCorners.w);
                float3 corner = max(max(sw, se), max(nw, ne));
                float4 sides = max(max(w, e), max(s, n));
                float branch = max(sides.x, corner.x);
                float glow = max(max(w.w*_Nearby.x,e.w*_Nearby.y), max(s.w*_Nearby.z,n.w*_Nearby.w));
                glow = max(glow, max(max(sw.z * max(_Nearby.x,_Nearby.z), se.z * max(_Nearby.y,_Nearby.z)),
                    max(nw.z * max(_Nearby.x,_Nearby.w), ne.z * max(_Nearby.y,_Nearby.w))));
                // Keep green out of the blue-violet cap so it does not saturate the green receiver.
                float3 color = lerp(float3(0.36, 0.04, 0.91), float3(1.0, 0.35, 0.18), blocks);
                float seam = max(max((1.0 - _Exposed.x) * (1.0 - smoothstep(0.0, 0.027, p.x)),
                    (1.0 - _Exposed.y) * smoothstep(0.973, 1.0, p.x)),
                    max((1.0 - _Exposed.z) * (1.0 - smoothstep(0.0, 0.027, p.y)),
                    (1.0 - _Exposed.w) * smoothstep(0.973, 1.0, p.y)));
                color *= 1.0 - 0.25 * seam;
                float3 rgb = AddShieldCrest(color * cap, p);
                rgb = lerp(rgb, float3(1.0, 0.14, 0.60), branch);
                rgb = lerp(rgb, float3(0.52, 0.22, 1.0), sides.y);
                rgb = lerp(rgb, float3(1.0, 0.42, 0.32), sides.z);
                rgb = lerp(rgb, float3(1.0, 0.40, 0.76), corner.y);
                rgb = lerp(rgb, float3(1.0, 0.70, 0.93), glow * 0.80);
                float alpha = max(cap, max(max(branch, corner.y), max(sides.y, sides.z)));
                float opacity = saturate(2.65 * _Color.a);
                return fixed4(rgb * opacity, alpha * opacity);
            }
            ENDCG
        }
    }
}

#version 410 core
// Expands each object into a velocity arrow (GPU twin of Universe3D.Core.Display.VelocityArrow):
// length L = clamp(k*log10(1 + |v|/100), 3, 60) px along the Sun-to-object direction, outward and red when
// receding, inward and blue when approaching. The drawn length is foreshortened by the view; when it is
// under 1 px the arrow points along the line of sight and a circled dot (toward the viewer) or circled cross (away) is drawn.

layout(points) in;
layout(line_strip, max_vertices = 16) out;

in VS
{
    vec4 clip;
    vec3 rel;
    vec3 radial;
    float speed;
    float keep;
} gs_in[];

uniform mat4 u_viewProj;
uniform vec2 u_viewport;     // pixels
uniform float u_focalPx;
uniform float u_k;

out vec3 g_color;

const float HeadAngle = 0.4363;   // 25 degrees
const float SymbolRadiusPx = 4.0;

vec2 toPx(vec4 c) { return (c.xy / c.w * 0.5 + 0.5) * u_viewport; }

vec4 atPx(vec2 px, vec4 ref) { return vec4((px / u_viewport * 2.0 - 1.0) * ref.w, ref.z, ref.w); }

void segment(vec2 a, vec2 b, vec4 ref)
{
    gl_Position = atPx(a, ref); EmitVertex();
    gl_Position = atPx(b, ref); EmitVertex();
    EndPrimitive();
}

vec2 rotate(vec2 v, float a) { float c = cos(a), s = sin(a); return vec2(c * v.x - s * v.y, s * v.x + c * v.y); }

void main()
{
    if (gs_in[0].keep < 0.5) return;
    vec4 c0 = gs_in[0].clip;
    if (c0.w <= 0.0) return;
    vec3 ndc = c0.xyz / c0.w;
    if (any(greaterThan(abs(ndc), vec3(1.2)))) return;

    float v = gs_in[0].speed;
    bool approaching = v < 0.0;
    float L = clamp(u_k * log(1.0 + abs(v) / 100.0) / log(10.0), 3.0, 60.0);
    vec3 dir = gs_in[0].radial * (approaching ? -1.0 : 1.0);
    g_color = approaching ? vec3(0.35, 0.55, 1.0) : vec3(1.0, 0.3, 0.25);

    // Screen direction from a small step along dir; fraction = projected / face-on length.
    float eps = 1e-3 * c0.w;
    vec4 c1 = u_viewProj * vec4(gs_in[0].rel + dir * eps, 1.0);
    vec2 p0 = toPx(c0);
    vec2 delta = c1.w > 0.0 ? toPx(c1) - p0 : vec2(0.0);
    float fraction = min(length(delta) / (1e-3 * u_focalPx), 1.0);
    float len = L * fraction;

    if (len < 1.0)
    {
        // Circle, then a dot (toward the viewer) or a cross (away).
        const int N = 8;
        for (int i = 0; i <= N; i++)
        {
            float a = 6.2831853 * float(i) / float(N);
            gl_Position = atPx(p0 + SymbolRadiusPx * vec2(cos(a), sin(a)), c0);
            EmitVertex();
        }
        EndPrimitive();
        bool towardViewer = dot(dir, -gs_in[0].rel) > 0.0;
        float r = towardViewer ? 1.0 : SymbolRadiusPx * 0.7071;
        segment(p0 + vec2(-r, -r), p0 + vec2(r, r), c0);
        segment(p0 + vec2(-r, r), p0 + vec2(r, -r), c0);
        return;
    }

    vec2 sd = normalize(delta);
    vec2 tip = p0 + sd * len;
    segment(p0, tip, c0);
    float head = min(6.0, 0.4 * len);
    segment(tip, tip + rotate(-sd, HeadAngle) * head, c0);
    segment(tip, tip + rotate(-sd, -HeadAngle) * head, c0);
}

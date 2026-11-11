#version 410 core
// Point cloud. Positions arrive as high/low float pairs and are made relative to the eye before any
// float arithmetic on large values, so objects 1e4 Mpc away stay steady at 1e-4 Mpc camera distances.

layout(location = 0) in vec3 a_posHigh;
layout(location = 1) in vec3 a_posLow;
layout(location = 2) in float a_distance;
layout(location = 3) in uint a_typeCode;
layout(location = 4) in float a_cz;        // observed velocity, km/s (c*z above z = 0.1); NaN for none
layout(location = 5) in float a_pec;       // peculiar velocity, km/s; NaN for none
layout(location = 6) in float a_magnitude; // NaN for none
layout(location = 7) in float a_diameter;  // Mpc; 0 for none

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;     // rotation-only view times projection
uniform float u_focalPx;     // viewport height / (2 tan(fov / 2))
uniform float u_maxDistance;
uniform uint u_typeMask;
uniform int u_colorMode;     // 0 = type, 1 = velocity
uniform vec3 u_typeColors[16];
uniform vec2 u_magRange;     // (bright, faint)
uniform float u_pointScale;
uniform float u_alpha;
uniform vec2 u_fadeRange;     // px; galaxies cross-fade to shapes: alpha * (1 - smoothstep(x, y, px)); off unless x < y
uniform int u_growWithDiameter;

out vec4 v_color;

bool isMissing(float v) { return (floatBitsToUint(v) & 0x7f800000u) == 0x7f800000u; }

vec3 velocityColor(float v)
{
    if (isMissing(v)) return vec3(0.55);
    if (v < 0.0)
    {
        float t = clamp(log(1.0 + -v / 100.0) / log(11.0), 0.0, 1.0);   // saturates at 1,000 km/s
        return mix(vec3(0.85, 0.9, 1.0), vec3(0.2, 0.45, 1.0), t);
    }
    float t = clamp(log(1.0 + v / 100.0) / log(1001.0), 0.0, 1.0);      // saturates at 100,000 km/s
    return mix(vec3(1.0, 0.95, 0.85), vec3(1.0, 0.15, 0.1), t);
}

void main()
{
    if (a_distance > u_maxDistance || ((u_typeMask >> min(a_typeCode, 31u)) & 1u) == 0u)
    {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);   // outside the clip volume: dropped
        gl_PointSize = 0.0;
        v_color = vec4(0.0);
        return;
    }

    precise vec3 rel = (a_posHigh - u_eyeHigh) + (a_posLow - u_eyeLow);
    gl_Position = u_viewProj * vec4(rel, 1.0);

    float size = 3.0;
    if (!isMissing(a_magnitude) && u_magRange.y > u_magRange.x)
    {
        float t = clamp((a_magnitude - u_magRange.x) / (u_magRange.y - u_magRange.x), 0.0, 1.0);
        size = mix(6.0, 1.75, t);
    }
    size *= u_pointScale;
    // Projected diameter, the same measure ShapeRenderer selects shapes by.
    float px = (a_diameter > 0.0 && gl_Position.w > 0.0) ? a_diameter * u_focalPx / gl_Position.w : 0.0;
    // Off unless the window is a valid range (smoothstep is undefined when edge0 >= edge1).
    float fade = (u_fadeRange.y > u_fadeRange.x && a_diameter > 0.0) ? smoothstep(u_fadeRange.x, u_fadeRange.y, px) : 0.0;
    if (fade >= 1.0)
    {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);   // fully replaced by its shape
        gl_PointSize = 0.0;
        v_color = vec4(0.0);
        return;
    }
    if (u_growWithDiameter != 0)
        size = max(size, min(px, 64.0));
    gl_PointSize = max(size, 1.0);

    vec3 rgb = u_colorMode == 1 ? velocityColor(a_cz) : u_typeColors[min(a_typeCode, 15u)];
    v_color = vec4(rgb, u_alpha * (1.0 - fade));
}

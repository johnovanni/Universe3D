#version 410 core
// Velocity arrows: one point per object, expanded to line segments by arrows.geom.
// Shares the point cloud's vertex buffer and filters.

layout(location = 0) in vec3 a_posHigh;
layout(location = 1) in vec3 a_posLow;
layout(location = 2) in float a_distance;
layout(location = 3) in uint a_typeCode;
layout(location = 4) in float a_cz;
layout(location = 5) in float a_pec;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_maxDistance;
uniform uint u_typeMask;
uniform int u_mode;          // 0 = Observed, 1 = Peculiar velocity

out VS
{
    vec4 clip;
    vec3 rel;
    vec3 radial;             // Sun-to-object unit vector
    float speed;             // signed km/s
    float keep;
} vs;

bool isMissing(float v) { return (floatBitsToUint(v) & 0x7f800000u) == 0x7f800000u; }

void main()
{
    precise vec3 rel = (a_posHigh - u_eyeHigh) + (a_posLow - u_eyeLow);
    vec3 pos = a_posHigh + a_posLow;
    float speed = u_mode == 1 ? a_pec : a_cz;
    bool visible = a_distance <= u_maxDistance && ((u_typeMask >> min(a_typeCode, 31u)) & 1u) != 0u;

    vs.clip = u_viewProj * vec4(rel, 1.0);
    vs.rel = rel;
    vs.radial = dot(pos, pos) > 0.0 ? normalize(pos) : vec3(0.0);
    vs.speed = isMissing(speed) ? 0.0 : speed;
    vs.keep = (visible && !isMissing(speed) && dot(pos, pos) > 0.0) ? 1.0 : 0.0;
    gl_Position = vs.clip;
}

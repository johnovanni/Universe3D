#version 410 core
// Ellipsoid impostors: galaxy shapes, cluster spheres and the Milky Way. Each instance is one quad facing the eye
// that covers the ellipsoid's bounding sphere; the fragment shader intersects the view ray with the ellipsoid in
// the ellipsoid's unit-sphere space, so the shape is right from any angle and at any distance (true perspective).
// Centers arrive as high/low float pairs and are made relative to the eye first, as in points.vert.

layout(location = 0) in vec2 a_corner;      // quad corner, -1..1
layout(location = 1) in vec3 i_posHigh;
layout(location = 2) in vec3 i_posLow;
layout(location = 3) in vec4 i_rotation;    // quaternion (x, y, z, w): local axes -> display frame
layout(location = 4) in vec3 i_semiAxes;    // Mpc, along local x, y, z
layout(location = 5) in uint i_kind;
layout(location = 6) in uint i_typeCode;
layout(location = 7) in float i_cz;         // km/s; NaN for none
layout(location = 8) in float i_seed;
layout(location = 9) in float i_distance;   // Mpc from the Sun

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;     // rotation-only view times projection
uniform float u_focalPx;
uniform vec3 u_forward;
uniform vec3 u_right;
uniform vec3 u_up;
uniform float u_near;
uniform float u_tanHalfFov;
uniform float u_aspect;
uniform vec2 u_fadeRange;    // px: alpha = smoothstep(x, y, projected diameter)
uniform float u_maxDistance;
uniform uint u_typeMask;
uniform int u_colorMode;     // 0 = type, 1 = velocity, 2 = natural (Milky Way)
uniform vec3 u_typeColors[16];
uniform float u_alpha;

flat out vec3 v_origin;      // the eye in unit-sphere space
out vec3 v_dir;              // eye-to-quad-point in unit-sphere space (linear in the quad point, so interpolable)
flat out vec3 v_shape;       // semi-axes / largest semi-axis
flat out vec4 v_color;       // tint; alpha = fade * u_alpha
flat out uint v_kind;
flat out float v_seed;

bool isMissing(float v) { return (floatBitsToUint(v) & 0x7f800000u) == 0x7f800000u; }

vec3 rotate(vec4 q, vec3 v) { return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v); }

vec3 unrotate(vec4 q, vec3 v) { return rotate(vec4(-q.xyz, q.w), v); }

vec3 velocityColor(float v)
{
    if (isMissing(v)) return vec3(0.55);
    if (v < 0.0)
    {
        float t = clamp(log(1.0 + -v / 100.0) / log(11.0), 0.0, 1.0);
        return mix(vec3(0.85, 0.9, 1.0), vec3(0.2, 0.45, 1.0), t);
    }
    float t = clamp(log(1.0 + v / 100.0) / log(1001.0), 0.0, 1.0);
    return mix(vec3(1.0, 0.95, 0.85), vec3(1.0, 0.15, 0.1), t);
}

void drop()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
    v_origin = vec3(0.0);
    v_dir = vec3(0.0);
    v_shape = vec3(1.0);
    v_color = vec4(0.0);
    v_kind = 0u;
    v_seed = 0.0;
}

void main()
{
    bool visible = i_distance <= u_maxDistance && ((u_typeMask >> min(i_typeCode, 31u)) & 1u) != 0u;
    precise vec3 center = (i_posHigh - u_eyeHigh) + (i_posLow - u_eyeLow);
    float R = max(i_semiAxes.x, max(i_semiAxes.y, i_semiAxes.z));
    float D = length(center);
    float depth = dot(center, u_forward);
    bool inside = D <= 1.02 * R;
    // Projected diameter, as Projector.PixelSize and points.vert measure it.
    float px = inside ? 1e9 : (depth > 0.0 ? 2.0 * R * u_focalPx / depth : 0.0);
    float fade = smoothstep(u_fadeRange.x, u_fadeRange.y, px);
    if (!visible || R <= 0.0 || fade <= 0.0) { drop(); return; }

    vec3 p;
    if (!inside)
    {
        // The plane through the center, facing the eye, cut by the cone tangent to the bounding sphere.
        vec3 w = center / D;
        vec3 helper = abs(dot(w, u_up)) < 0.9 ? u_up : u_right;
        vec3 u = normalize(cross(w, helper));
        vec3 v = cross(u, w);
        float halfSize = 1.02 * R * D / sqrt(D * D - R * R);
        p = center + (a_corner.x * u + a_corner.y * v) * halfSize;
    }
    else
    {
        // The eye is inside: a quad just beyond the near plane covering the whole view.
        float dz = 4.0 * u_near;
        float h = 1.2 * dz * u_tanHalfFov;
        p = u_forward * dz + (a_corner.x * u_aspect * u_right + a_corner.y * u_up) * h;
    }
    gl_Position = u_viewProj * vec4(p, 1.0);

    vec3 inv = 1.0 / i_semiAxes;
    v_origin = unrotate(i_rotation, -center) * inv;
    v_dir = unrotate(i_rotation, p) * inv;
    v_shape = i_semiAxes / R;
    vec3 tint = u_colorMode == 2 ? vec3(0.72, 0.82, 1.0)
              : u_colorMode == 1 ? velocityColor(i_cz)
              : u_typeColors[min(i_typeCode, 15u)];
    v_color = vec4(tint, fade * u_alpha);
    v_kind = i_kind;
    v_seed = i_seed;
}

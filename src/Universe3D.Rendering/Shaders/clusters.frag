#version 410 core
// Translucent cluster sphere (radius R500): a faint fill and a brighter limb, so it reads as a sphere.
// Uses the impostor from shapes.vert; drawn with additive blending.

flat in vec3 v_origin;
in vec3 v_dir;
flat in vec3 v_shape;
flat in vec4 v_color;
flat in uint v_kind;
flat in float v_seed;

out vec4 o_color;

void main()
{
    vec3 o = v_origin, d = v_dir;
    float a = dot(d, d);
    vec3 c = cross(o, d);
    float h2 = dot(c, c) / a;
    if (h2 >= 1.0) discard;
    float tm = -dot(o, d) / a;
    if (tm + sqrt((1.0 - h2) / a) <= 0.0) discard;   // behind the eye
    float fill = 0.10 * sqrt(1.0 - h2);
    float limb = 0.45 * pow(h2, 6.0) * (1.0 - smoothstep(0.93, 1.0, h2));
    o_color = vec4(v_color.rgb, (fill + limb) * v_color.a);
}

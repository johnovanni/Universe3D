#version 410 core
// Soft disc sprite.

in vec4 v_color;
out vec4 o_color;

void main()
{
    vec2 d = gl_PointCoord * 2.0 - 1.0;
    float r = length(d);
    if (r > 1.0) discard;
    float a = 1.0 - smoothstep(0.35, 1.0, r);
    o_color = vec4(v_color.rgb, v_color.a * a);
}

#version 410 core
// The astronomical Sun symbol: a circle with a dot in the middle.

uniform vec3 u_color;
out vec4 o_color;

void main()
{
    float r = length(gl_PointCoord * 2.0 - 1.0);
    float ring = smoothstep(0.62, 0.72, r) * (1.0 - smoothstep(0.9, 1.0, r));
    float core = 1.0 - smoothstep(0.22, 0.32, r);
    float a = max(ring, core);
    if (a <= 0.0) discard;
    o_color = vec4(u_color, a);
}

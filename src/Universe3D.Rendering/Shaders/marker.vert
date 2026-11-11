#version 410 core
// The Sun marker: one point sprite at the origin, relative to the eye.

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_sizePx;

void main()
{
    precise vec3 rel = -u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    gl_PointSize = u_sizePx;
}

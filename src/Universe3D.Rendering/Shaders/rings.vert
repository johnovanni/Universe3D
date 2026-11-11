#version 410 core
// Scale ring: a unit circle in the display frame's reference plane (z = 0) around the Sun, scaled by u_radius.
// Made relative to the eye before the projection, as in points.vert.

layout(location = 0) in vec2 a_unit;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_radius;      // Mpc

void main()
{
    precise vec3 rel = (vec3(a_unit * u_radius, 0.0) - u_eyeHigh) - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
}

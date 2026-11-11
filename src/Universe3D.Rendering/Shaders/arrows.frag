#version 410 core

in vec3 g_color;
out vec4 o_color;

void main()
{
    o_color = vec4(g_color, 0.7);   // spec section 3: 70% opacity
}

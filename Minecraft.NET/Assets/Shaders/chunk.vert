#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexIndex;
layout (location = 2) in vec2 aUV;
layout (location = 3) in float aAO;

out vec2 vTexIndex;
out vec2 vUV;
out float v_ao;

uniform mat4 mvp;

void main() 
{
    gl_Position = mvp * vec4(aPos, 1.0);
    vTexIndex = aTexIndex;
    vUV = aUV;
    v_ao = aAO;
}
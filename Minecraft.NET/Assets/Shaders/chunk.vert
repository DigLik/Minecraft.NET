#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec3 vLocalPos;
out vec2 vTexIndex;

uniform mat4 mvp;

void main() 
{
    gl_Position = mvp * vec4(aPos, 1.0);
    
    vLocalPos = aPos;
    vTexIndex = aTexCoords;
}
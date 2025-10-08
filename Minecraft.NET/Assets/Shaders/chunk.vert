#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec3 vWorldPos;
out vec2 vTexIndex;

uniform mat4 mvp;
uniform mat4 model;

void main() 
{
    vec4 worldPos = model * vec4(aPos, 1.0);
    gl_Position = mvp * vec4(aPos, 1.0);
    
    vWorldPos = worldPos.xyz;
    vTexIndex = aTexCoords;
}
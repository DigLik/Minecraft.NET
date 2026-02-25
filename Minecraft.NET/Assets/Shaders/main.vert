#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in float aTexIndexFloat;
layout(location = 2) in vec2 aUV;

out vec3 WorldPos;
out vec2 TexCoords;
flat out uint TexIndex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    vec4 worldPos = model * vec4(aPos, 1.0);
    WorldPos = worldPos.xyz;
    TexCoords = aUV;
    TexIndex = uint(aTexIndexFloat);
    gl_Position = projection * view * worldPos;
}
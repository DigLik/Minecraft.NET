#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec3 v_worldPos;
out vec3 v_viewPos;
out vec2 v_texIndex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main() 
{
    vec4 worldPos = model * vec4(aPos, 1.0);
    vec4 viewPos = view * worldPos;

    v_worldPos = worldPos.xyz;
    v_viewPos = viewPos.xyz;
    v_texIndex = aTexCoords;

    gl_Position = projection * viewPos;
}
#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;
layout (location = 2) in mat4 aModel;

out vec3 v_worldPos;
out vec3 v_viewPos;
out vec2 v_texIndex;

uniform mat4 view;
uniform mat4 projection;

void main() 
{
    vec4 worldPos = aModel * vec4(aPos, 1.0);
    vec4 viewPos = view * worldPos;

    v_worldPos = worldPos.xyz;
    v_viewPos = viewPos.xyz;
    v_texIndex = aTexCoords;

    gl_Position = projection * viewPos;
}
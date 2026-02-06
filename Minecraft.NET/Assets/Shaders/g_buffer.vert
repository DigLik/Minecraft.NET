#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexIndex;
layout (location = 2) in vec2 aUV;
layout (location = 3) in float aAO;
layout (location = 4) in vec3 aInstancePos;

out vec2 v_texIndex;
out vec2 v_uv;
out float v_ao;
out float v_fogFactor;

uniform mat4 view;
uniform mat4 projection;

uniform float u_fogStart;
uniform float u_fogEnd;

void main()
{
    vec4 relativeWorldPos = vec4(aPos + aInstancePos, 1.0);
    vec4 viewPos = view * relativeWorldPos;

    float distance = length(viewPos.xyz);
    v_fogFactor = smoothstep(u_fogStart, u_fogEnd, distance);

    v_texIndex = aTexIndex;
    v_uv = aUV;
    v_ao = aAO;

    gl_Position = projection * viewPos;
}
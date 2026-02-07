#version 460 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aUV;
layout (location = 2) in vec4 aColor;
layout (location = 3) in float aType;
layout (location = 4) in vec2 aSize;
layout (location = 5) in float aRadius;

out vec2 vUV;
out vec4 vColor;
out float vType;
out vec2 vPos;
out vec2 vSize;
out float vRadius;

uniform mat4 uProjection;

void main() {
    vUV = aUV;
    vColor = aColor;
    vType = aType;
    vPos = aPos;
    vSize = aSize;
    vRadius = aRadius;
    gl_Position = uProjection * vec4(aPos, 0.0, 1.0);
}
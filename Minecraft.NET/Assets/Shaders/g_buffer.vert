#version 460 core

struct PackedVertex {
    uint data1;
    uint data2;
};

layout(std430, binding = 0) readonly buffer VertexBuffer {
    PackedVertex vertices[];
};

layout (location = 4) in vec3 aInstancePos;

out vec3 vTexCoord;
out float v_ao;
out float v_fogFactor;

uniform mat4 view;
uniform mat4 projection;
uniform float u_fogStart;
uniform float u_fogEnd;

void main()
{
    PackedVertex v = vertices[gl_VertexID];

    float x = float(bitfieldExtract(v.data1, 0, 8));
    float y = float(bitfieldExtract(v.data1, 8, 8));
    float z = float(bitfieldExtract(v.data1, 16, 8));
    float ao = float(bitfieldExtract(v.data1, 24, 8)) / 255.0;

    uint texIndex = bitfieldExtract(v.data2, 0, 16);
    float u = float(bitfieldExtract(v.data2, 16, 8));
    float v_coord = float(bitfieldExtract(v.data2, 24, 8));

    vec3 localPos = vec3(x, y, z);
    vec4 relativeWorldPos = vec4(localPos + aInstancePos, 1.0);
    vec4 viewPos = view * relativeWorldPos;
    
    float distance = length(viewPos.xyz);
    v_fogFactor = smoothstep(u_fogStart, u_fogEnd, distance);

    vTexCoord = vec3(u, v_coord, float(texIndex));
    v_ao = ao;

    gl_Position = projection * viewPos;
}
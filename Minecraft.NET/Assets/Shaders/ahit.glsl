#version 460
#extension GL_EXT_ray_tracing : require
#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_EXT_buffer_reference2 : require
#extension GL_EXT_scalar_block_layout : enable
#extension GL_EXT_shader_explicit_arithmetic_types_int64 : require 

hitAttributeEXT vec2 attribs;

struct ChunkVertex {
    vec4 position;
    int textureIndex;
    vec2 uv;
    int overlayTextureIndex;
    vec4 color;
    vec4 overlayColor;
};

struct InstanceData {
    uint vertexOffset;
    uint indexOffset;
    uint pad1;
    uint pad2;
    uint64_t vertexAddress;
    uint64_t indexAddress;
};

layout(buffer_reference, scalar) buffer Vertices { ChunkVertex v[]; };
layout(buffer_reference, scalar) buffer Indices { uint i[]; };

layout(binding = 3) uniform sampler2DArray texSampler;
layout(binding = 4, scalar) buffer InstanceDataBuffer { InstanceData instances[]; };

void main() 
{
    InstanceData inst = instances[gl_InstanceCustomIndexEXT];
    
    Vertices verts = Vertices(inst.vertexAddress);
    Indices inds = Indices(inst.indexAddress);

    uint baseIndex = inst.indexOffset + (gl_PrimitiveID * 3);
    uint i0 = inds.i[baseIndex + 0];
    uint i1 = inds.i[baseIndex + 1];
    uint i2 = inds.i[baseIndex + 2];

    ChunkVertex v0 = verts.v[inst.vertexOffset + i0];
    ChunkVertex v1 = verts.v[inst.vertexOffset + i1];
    ChunkVertex v2 = verts.v[inst.vertexOffset + i2];

    vec3 barycentrics = vec3(1.0 - attribs.x - attribs.y, attribs.x, attribs.y);
    
    vec2 uv = v0.uv * barycentrics.x + v1.uv * barycentrics.y + v2.uv * barycentrics.z;
    int texIndex = v0.textureIndex;

    vec4 texColor = texture(texSampler, vec3(uv, texIndex));

    if (texColor.a < 0.5) 
        ignoreIntersectionEXT;
}
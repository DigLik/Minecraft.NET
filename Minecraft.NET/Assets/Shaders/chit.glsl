#version 460
#extension GL_EXT_ray_tracing : require
#extension GL_EXT_scalar_block_layout : require
#extension GL_EXT_ray_query : require

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
    float reflectivity;
};

layout(location = 0) rayPayloadInEXT Payload payload;
hitAttributeEXT vec2 attribs;

struct ChunkVertex {
    vec4 Position;
    int TextureIndex;
    vec2 UV;
    int OverlayTextureIndex;
    vec4 Color;
    vec4 OverlayColor;
};

struct InstanceData {
    uint VertexOffset;
    uint IndexOffset;
    uint Pad1;
    uint Pad2;
};

layout(binding = 0, set = 0) uniform accelerationStructureEXT Scene;

layout(binding = 2, set = 0) uniform Camera {
    mat4 ViewProj;
    mat4 InverseViewProj;
    vec4 CameraPos;
    vec4 SunDirection;
} cam;

layout(binding = 3, set = 0) uniform sampler2DArray TexArray;
layout(binding = 4, set = 0, scalar) readonly buffer Vertices { ChunkVertex v[]; } vertices;
layout(binding = 5, set = 0, scalar) readonly buffer Indices { uint i[]; } indices;
layout(binding = 6, set = 0, scalar) readonly buffer Instances { InstanceData d[]; } instances;

void main() {
    uint instId = gl_InstanceID;
    uint primId = gl_PrimitiveID;

    InstanceData inst = instances.d[instId];
    uint i0 = indices.i[inst.IndexOffset + primId * 3 + 0];
    uint i1 = indices.i[inst.IndexOffset + primId * 3 + 1];
    uint i2 = indices.i[inst.IndexOffset + primId * 3 + 2];

    ChunkVertex v0 = vertices.v[inst.VertexOffset + i0];
    ChunkVertex v1 = vertices.v[inst.VertexOffset + i1];
    ChunkVertex v2 = vertices.v[inst.VertexOffset + i2];

    vec3 barycentrics = vec3(1.0 - attribs.x - attribs.y, attribs.x, attribs.y);

    vec2 uv = v0.UV * barycentrics.x + v1.UV * barycentrics.y + v2.UV * barycentrics.z;
    vec4 color = v0.Color * barycentrics.x + v1.Color * barycentrics.y + v2.Color * barycentrics.z;
    
    int texIndex = v0.TextureIndex;
    int overlayTexIndex = v0.OverlayTextureIndex;
    vec4 overlayColor = v0.OverlayColor * barycentrics.x + v1.OverlayColor * barycentrics.y + v2.OverlayColor * barycentrics.z;

    vec4 texColor = texture(TexArray, vec3(uv, float(texIndex)));
    
    if (overlayTexIndex >= 0) {
        vec4 overlayTex = texture(TexArray, vec3(uv, float(overlayTexIndex)));
        if (overlayTex.a > 0.5) {
            texColor = overlayTex * overlayColor;
        } else {
            texColor *= color;
        }
    } else {
        texColor *= color;
    }

    vec3 worldPos = gl_WorldRayOriginEXT + gl_WorldRayDirectionEXT * gl_HitTEXT;
    
    vec3 e1 = v1.Position.xyz - v0.Position.xyz;
    vec3 e2 = v2.Position.xyz - v0.Position.xyz;
    vec3 normal = normalize(cross(e1, e2));

    if (dot(normal, gl_WorldRayDirectionEXT) > 0.0) {
        normal = -normal;
    }
    
    vec3 shadowOrigin = worldPos + normal * 0.01;

    rayQueryEXT rq;
    rayQueryInitializeEXT(rq, Scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsOpaqueEXT | gl_RayFlagsSkipClosestHitShaderEXT, 0xFF, shadowOrigin, 0.001, cam.SunDirection.xyz, 1000.0);
    rayQueryProceedEXT(rq);

    if (rayQueryGetIntersectionTypeEXT(rq, true) != gl_RayQueryCommittedIntersectionNoneEXT) {
        texColor.rgb *= 0.4;
    }

    payload.hitPos = worldPos;
    payload.normal = normal;
    payload.color = texColor;

    if (texIndex == 0) {
        payload.reflectivity = 1.0;
    } else {
        payload.reflectivity = 0.0;
    }
}
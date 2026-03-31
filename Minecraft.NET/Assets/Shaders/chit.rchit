#version 460
#extension GL_EXT_ray_tracing : require
#extension GL_EXT_scalar_block_layout : require
#extension GL_EXT_ray_query : require
#extension GL_EXT_buffer_reference2 : require

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
};

layout(location = 0) rayPayloadInEXT Payload payload;
hitAttributeEXT vec2 attribs;

struct ChunkVertex {
    float x;
    float y;
    float z;
    uint packedData;
};

layout(buffer_reference, scalar, buffer_reference_align = 4) readonly buffer VertexBuffer { ChunkVertex v[]; };
layout(buffer_reference, scalar, buffer_reference_align = 4) readonly buffer IndexBuffer { uint i[]; };

struct InstanceData {
    uint VertexOffset;
    uint IndexOffset;
    uint Pad1;
    uint Pad2;
    VertexBuffer verts;
    IndexBuffer inds;
};

layout(binding = 0, set = 0) uniform accelerationStructureEXT Scene;

layout(binding = 2, set = 0) uniform Camera {
    mat4 ViewProj;
    mat4 InverseViewProj;
    ivec3 ChunkPosition;
    float Pad1;
    vec3 LocalPosition;
    float Pad2;
    vec4 SunDirection;
} cam;

layout(binding = 3, set = 0) uniform sampler2DArray TexArray;
layout(binding = 4, set = 0, scalar) readonly buffer Instances { InstanceData d[]; } instances;

void main() {
    uint instId = gl_InstanceID;
    uint primId = gl_PrimitiveID;
    InstanceData inst = instances.d[instId];

    uint i0 = inst.inds.i[inst.IndexOffset + primId * 3 + 0];
    uint i1 = inst.inds.i[inst.IndexOffset + primId * 3 + 1];
    uint i2 = inst.inds.i[inst.IndexOffset + primId * 3 + 2];

    ChunkVertex v0 = inst.verts.v[inst.VertexOffset + i0];
    ChunkVertex v1 = inst.verts.v[inst.VertexOffset + i1];
    ChunkVertex v2 = inst.verts.v[inst.VertexOffset + i2];

    vec3 p0 = vec3(v0.x, v0.y, v0.z);
    vec3 p1 = vec3(v1.x, v1.y, v1.z);
    vec3 p2 = vec3(v2.x, v2.y, v2.z);

    vec3 e1 = p1 - p0;
    vec3 e2 = p2 - p0;
    vec3 normal = normalize(cross(e1, e2));
    if (dot(normal, gl_WorldRayDirectionEXT) > 0.0) normal = -normal;

    uint pd = v0.packedData;
    int texIndex = int(pd & 0xFFF);
    int overlayTexIndex = int((pd >> 12) & 0xFFF);
    if (overlayTexIndex == 0xFFF) overlayTexIndex = -1;
    uint tintType = (pd >> 26) & 0x7;

    vec2 uvs[4] = vec2[](vec2(0,0), vec2(0,1), vec2(1,1), vec2(1,0));
    vec2 uv0 = uvs[(v0.packedData >> 24) & 0x3];
    vec2 uv1 = uvs[(v1.packedData >> 24) & 0x3];
    vec2 uv2 = uvs[(v2.packedData >> 24) & 0x3];
    
    vec3 barycentrics = vec3(1.0 - attribs.x - attribs.y, attribs.x, attribs.y);
    vec2 uv = uv0 * barycentrics.x + uv1 * barycentrics.y + uv2 * barycentrics.z;

    vec4 baseTint = vec4(1.0);
    vec4 overTint = vec4(1.0);
    if (tintType == 1) baseTint = vec4(145.0/255.0, 189.0/255.0, 89.0/255.0, 1.0); // Grass Top
    else if (tintType == 2) overTint = vec4(145.0/255.0, 189.0/255.0, 89.0/255.0, 1.0); // Grass Side
    else if (tintType == 3) baseTint = vec4(72.0/255.0, 181.0/255.0, 72.0/255.0, 1.0); // Leaves

    vec4 texColor = texture(TexArray, vec3(uv, float(texIndex))) * baseTint;

    if (overlayTexIndex >= 0) {
        vec4 overlayTex = texture(TexArray, vec3(uv, float(overlayTexIndex)));
        if (overlayTex.a > 0.5) texColor = overlayTex * overTint;
    }

    float shade = 1.0;
    vec3 absN = abs(normal);
    if (absN.y > 0.5) shade = normal.y > 0.0 ? 1.0 : 0.5;
    else if (absN.z > 0.5) shade = 0.8;
    else if (absN.x > 0.5) shade = 0.6;
    
    texColor.rgb *= shade;

    vec3 worldPos = gl_WorldRayOriginEXT + gl_WorldRayDirectionEXT * gl_HitTEXT;
    vec3 shadowOrigin = worldPos + normal * 0.01;

    rayQueryEXT rq;
    rayQueryInitializeEXT(rq, Scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsSkipClosestHitShaderEXT, 0xFF, shadowOrigin, 0.001, cam.SunDirection.xyz, 1000.0);
    while(rayQueryProceedEXT(rq)) {
        if (rayQueryGetIntersectionTypeEXT(rq, false) == gl_RayQueryCandidateIntersectionTriangleEXT) {
            uint sInstId = rayQueryGetIntersectionInstanceCustomIndexEXT(rq, false);
            uint sPrimId = rayQueryGetIntersectionPrimitiveIndexEXT(rq, false);
            vec2 sAttribs = rayQueryGetIntersectionBarycentricsEXT(rq, false);

            InstanceData sInst = instances.d[sInstId];
            uint si0 = sInst.inds.i[sInst.IndexOffset + sPrimId * 3 + 0];
            uint si1 = sInst.inds.i[sInst.IndexOffset + sPrimId * 3 + 1];
            uint si2 = sInst.inds.i[sInst.IndexOffset + sPrimId * 3 + 2];

            ChunkVertex sv0 = sInst.verts.v[sInst.VertexOffset + si0];
            ChunkVertex sv1 = sInst.verts.v[sInst.VertexOffset + si1];
            ChunkVertex sv2 = sInst.verts.v[sInst.VertexOffset + si2];
            
            uint sPacked = sv0.packedData;
            int sTexIndex = int(sPacked & 0xFFF);

            vec2 sUv0 = uvs[(sPacked >> 24) & 0x3];
            vec2 sUv1 = uvs[(sv1.packedData >> 24) & 0x3];
            vec2 sUv2 = uvs[(sv2.packedData >> 24) & 0x3];

            vec3 sBary = vec3(1.0 - sAttribs.x - sAttribs.y, sAttribs.x, sAttribs.y);
            vec2 sUv = sUv0 * sBary.x + sUv1 * sBary.y + sUv2 * sBary.z;
            
            vec4 sTexColor = texture(TexArray, vec3(sUv, float(sTexIndex)));
            if (sTexColor.a >= 0.5)
                rayQueryConfirmIntersectionEXT(rq);
        }
    }

    if (rayQueryGetIntersectionTypeEXT(rq, true) != gl_RayQueryCommittedIntersectionNoneEXT) texColor.rgb *= 0.4;

    payload.hitPos = worldPos;
    payload.normal = normal;
    payload.color = texColor;
}
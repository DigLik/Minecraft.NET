#version 460
#extension GL_EXT_ray_tracing : require

struct Payload {
    vec3 hitPos;
    float hitDistance;
    vec3 normal;
    float roughness;
    vec3 albedo;
    float metallic;
    vec3 emission;
    float pad;
};

layout(location = 0) rayPayloadInEXT Payload payload;

void main() {
    payload.hitDistance = -1.0;
    payload.emission = pow(vec3(0.4, 0.6, 0.9), vec3(2.2)); // Sky color
}
#version 460
#extension GL_EXT_ray_tracing : require

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
    float reflectivity;
};

layout(location = 0) rayPayloadInEXT Payload payload;

void main() {
    payload.color = vec4(0.4, 0.6, 0.9, 1.0);
    payload.reflectivity = 0.0;
}
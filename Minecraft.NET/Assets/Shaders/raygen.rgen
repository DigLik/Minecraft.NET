#version 460
#extension GL_EXT_ray_tracing : require

layout(binding = 0, set = 0) uniform accelerationStructureEXT Scene;
layout(binding = 1, set = 0, rgba8) uniform image2D RenderTarget;

layout(binding = 2, set = 0) uniform Camera {
    mat4 ViewProj;
    mat4 InverseViewProj;
    vec4 CameraPos;
    vec4 SunDirection;
} cam;

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
    float reflectivity;
};

layout(location = 0) rayPayloadEXT Payload payload;

void main() {
    ivec2 launchIndex = ivec2(gl_LaunchIDEXT.xy);
    ivec2 launchDim = ivec2(gl_LaunchSizeEXT.xy);

    vec2 crd = vec2(launchIndex) / vec2(launchDim);
    crd = crd * 2.0 - 1.0;

    vec4 target = cam.InverseViewProj * vec4(crd.x, crd.y, 1.0, 1.0);
    vec3 rayDir = normalize(target.xyz / target.w - cam.CameraPos.xyz);

    uint rayFlags = gl_RayFlagsCullBackFacingTrianglesEXT;
    uint cullMask = 0xFF;
    float tmin = 0.001;
    float tmax = 10000.0;

    vec4 finalColor = vec4(0.0);
    vec3 currentOrigin = cam.CameraPos.xyz;
    vec3 currentDir = rayDir;
    float currentReflectivity = 0.0;

    for (int i = 0; i < 2; i++) {
        payload.reflectivity = 0.0;
        traceRayEXT(Scene, rayFlags, cullMask, 0, 0, 0, currentOrigin, tmin, currentDir, tmax, 0);

        if (i == 0) {
            finalColor = payload.color;
            currentReflectivity = payload.reflectivity;
        } else {
            finalColor.rgb = mix(finalColor.rgb, payload.color.rgb, currentReflectivity);
        }

        if (payload.reflectivity > 0.0) {
            currentOrigin = payload.hitPos + payload.normal * 0.01;
            currentDir = reflect(currentDir, payload.normal);
            currentReflectivity = payload.reflectivity;
        } else {
            break;
        }
    }

    imageStore(RenderTarget, launchIndex, finalColor.bgra);
}
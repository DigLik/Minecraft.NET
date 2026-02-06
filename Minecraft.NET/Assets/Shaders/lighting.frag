#version 460 core
out vec4 FragColor;

in vec2 vTexCoords;

uniform sampler2D gNormal;
uniform sampler2D gAlbedo;
uniform sampler2D gDepth;

uniform vec3 u_fogColor;
uniform float u_fogStart;
uniform float u_fogEnd;

uniform mat4 u_inverseView;
uniform mat4 u_inverseProjection;

vec3 ReconstructWorldPos(float depth, vec2 uv)
{
    float z = depth; 
    vec4 clipSpacePosition = vec4(uv * 2.0 - 1.0, z, 1.0);
    vec4 viewSpacePosition = u_inverseProjection * clipSpacePosition;

    viewSpacePosition /= viewSpacePosition.w;
    vec4 worldSpacePosition = u_inverseView * viewSpacePosition;
    return worldSpacePosition.xyz;
}

void main()
{
    vec3 albedo = texture(gAlbedo, vTexCoords).rgb;
    float depth = texture(gDepth, vTexCoords).r;

    if (depth <= 0.00001) 
    {
        FragColor = vec4(u_fogColor, 1.0);
        return;
    }

    vec3 worldPos = ReconstructWorldPos(depth, vTexCoords);
    
    vec3 ambient = albedo;
    vec3 lighting = ambient;

    float distance = length(worldPos);
    
    float fogFactor = smoothstep(u_fogStart, u_fogEnd, distance);

    vec3 finalColor = mix(lighting, u_fogColor, fogFactor);

    FragColor = vec4(finalColor, 1.0);
}
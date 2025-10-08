#version 460 core
out vec4 FragColor;

in vec2 vTexCoords;

uniform sampler2D gAlbedo;
uniform sampler2D ssao;
uniform sampler2D gPosition;

uniform vec3 u_fogColor;
uniform float u_fogStart;
uniform float u_fogEnd;

void main()
{
    vec3 albedo = texture(gAlbedo, vTexCoords).rgb;
    float ao = texture(ssao, vTexCoords).r;
    
    vec3 ambient = albedo * (ao * 0.9 + 0.1);
    vec3 lighting = ambient;

    vec3 fragPos = texture(gPosition, vTexCoords).xyz;
    float distance = length(fragPos);

    float fogFactor = smoothstep(u_fogStart, u_fogEnd, distance);

    vec3 finalColor = mix(lighting, u_fogColor, fogFactor);

    FragColor = vec4(finalColor, 1.0);
}
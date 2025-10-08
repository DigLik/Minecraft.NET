#version 460 core
out vec4 FragColor;

in vec2 vTexCoords;

uniform sampler2D gAlbedo;
uniform sampler2D ssao;

void main()
{
    vec3 albedo = texture(gAlbedo, vTexCoords).rgb;
    float ao = texture(ssao, vTexCoords).r;
    
    vec3 ambient = albedo * (ao * 0.9 + 0.1);
    vec3 lighting = ambient;

    FragColor = vec4(lighting, 1.0);
}
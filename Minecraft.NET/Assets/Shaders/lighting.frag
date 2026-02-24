#version 460 core
out vec4 FragColor;

in vec2 vTexCoords;

uniform sampler2D gNormal;
uniform sampler2D gAlbedo;
uniform sampler2D gDepth;

void main()
{
    vec3 albedo = texture(gAlbedo, vTexCoords).rgb;
    float depth = texture(gDepth, vTexCoords).r;

    if (depth <= 0.00001) 
    {
        FragColor = vec4(0.0, 0.0, 0.0, 0.0);
        return;
    }
    
    vec3 ambient = albedo;
    vec3 lighting = ambient;

    FragColor = vec4(lighting, 1.0);
}
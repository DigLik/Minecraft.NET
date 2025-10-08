#version 460 core
out float FragColor;

in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D texNoise;

uniform vec3 samples[64];
uniform mat4 projection;

int kernelSize = 64;
float radius = 1.5;
float bias = 0.05;

uniform vec2 u_ScreenSize;

void main()
{
    vec3 fragPos = texture(gPosition, vTexCoords).xyz;
    vec3 normal = normalize(texture(gNormal, vTexCoords).rgb);
    vec2 noiseScale = u_ScreenSize / 4.0;
    vec3 randomVec = normalize(texture(texNoise, vTexCoords * noiseScale).xyz);

    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

    float occlusion = 0.0;
    for(int i = 0; i < kernelSize; ++i)
    {
        vec3 samplePos = TBN * samples[i];
        samplePos = fragPos + samplePos * radius;
        
        vec4 offset = vec4(samplePos, 1.0);
        offset = projection * offset;
        offset.xyz /= offset.w;
        offset.xyz = offset.xyz * 0.5 + 0.5;
        
        vec3 sampledPos = texture(gPosition, offset.xy).xyz;

        float occlusionFactor = (sampledPos.z > samplePos.z + bias) ? 1.0 : 0.0;

        float dist = length(fragPos - sampledPos);
        float attenuation = 1.0 - smoothstep(0.0, radius, dist);
        
        occlusion += occlusionFactor * attenuation;
    }
    
    occlusion = 1.0 - (occlusion / kernelSize);
    FragColor = pow(occlusion, 2.0);
}
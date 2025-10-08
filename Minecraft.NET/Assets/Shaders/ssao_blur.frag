#version 460 core
out float FragColor;

in vec2 vTexCoords;

uniform sampler2D ssaoInput;
uniform sampler2D gPosition;

const float depthThreshold = 0.025;

void main()
{
    float centerDepth = texture(gPosition, vTexCoords).z;
    if (centerDepth >= 1.0)
    {
        FragColor = texture(ssaoInput, vTexCoords).r;
        return;
    }

    vec2 texelSize = 1.0 / vec2(textureSize(ssaoInput, 0));
    float result = 0.0;
    float totalWeight = 0.0;

    for (int x = -2; x <= 2; ++x)
    {
        for (int y = -2; y <= 2; ++y)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleCoords = vTexCoords + offset;

            float sampleDepth = texture(gPosition, sampleCoords).z;

            if (abs(centerDepth - sampleDepth) < depthThreshold)
            {
                result += texture(ssaoInput, sampleCoords).r;
                totalWeight += 1.0;
            }
        }
    }

    if (totalWeight > 0.0)
    {
        FragColor = result / totalWeight;
    }
    else
    {
        FragColor = texture(ssaoInput, vTexCoords).r;
    }
}
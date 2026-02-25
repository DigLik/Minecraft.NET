#version 460 core
out vec4 FragColor;

in vec3 WorldPos;
in vec2 TexCoords;
flat in uint TexIndex;

uniform sampler2DArray uTextureArray;
uniform bool u_UseWireframeColor;
uniform vec4 u_WireframeColor;

const vec3 lightDir = normalize(vec3(0.4, 0.8, 0.3));
const float ambient = 0.3;

void main()
{
    if (u_UseWireframeColor)
    {
        FragColor = u_WireframeColor;
        return;
    }

    vec4 texColor = texture(uTextureArray, vec3(TexCoords, float(TexIndex)));
    if(texColor.a < 0.1)
        discard;

    vec3 normal = normalize(cross(dFdx(WorldPos), dFdy(WorldPos)));
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 lighting = texColor.rgb * (diff + ambient);

    FragColor = vec4(lighting, texColor.a);
}
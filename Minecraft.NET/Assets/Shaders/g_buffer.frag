#version 460 core
layout(location = 0) out vec4 gNormal;
layout(location = 1) out vec4 gAlbedo;

in vec3 FragPos;
in vec2 TexCoords;
flat in uint TexIndex;

uniform sampler2DArray uTextureArray;
uniform bool u_UseWireframeColor;
uniform vec4 u_WireframeColor;

void main()
{
    if (u_UseWireframeColor)
    {
        gAlbedo = u_WireframeColor;
        gNormal = vec4(0.0, 1.0, 0.0, 1.0);
        return;
    }

    vec4 texColor = texture(uTextureArray, vec3(TexCoords, float(TexIndex)));
    if(texColor.a < 0.1)
        discard;

    gAlbedo = texColor;
    vec3 normal = normalize(cross(dFdx(FragPos), dFdy(FragPos)));
    gNormal = vec4(normal, 1.0);
}
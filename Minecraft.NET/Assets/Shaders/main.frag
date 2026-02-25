#version 460 core
out vec4 FragColor;

in vec2 TexCoords;
flat in uint TexIndex;

uniform sampler2DArray uTextureArray;
uniform bool u_UseWireframeColor;
uniform vec4 u_WireframeColor;

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

    FragColor = texColor;
}
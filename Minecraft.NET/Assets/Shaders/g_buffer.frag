#version 460 core
layout (location = 0) out vec4 FragColor;

in vec3 vTexCoord;
in float v_ao;
in float v_fogFactor;

uniform sampler2DArray uTextureArray;
uniform vec3 u_fogColor;
uniform bool u_UseWireframeColor;
uniform vec4 u_WireframeColor;

void main() 
{
    if (u_UseWireframeColor)
    {
        FragColor = u_WireframeColor;
    }
    else
    {
        vec4 texColor = texture(uTextureArray, vTexCoord);
        
        if (texColor.a < 0.1) discard;

        vec3 lighting = texColor.rgb * v_ao;
        vec3 finalColor = mix(lighting, u_fogColor, v_fogFactor);

        FragColor = vec4(finalColor, 1.0);
    }
}
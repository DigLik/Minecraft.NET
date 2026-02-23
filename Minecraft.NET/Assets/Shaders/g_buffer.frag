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

        int texIndex = int(round(vTexCoord.z)); 
        
        vec3 grassColor = vec3(145.0 / 255.0, 189.0 / 255.0, 89.0 / 255.0);
        vec3 leavesColor = vec3(119.0 / 255.0, 171.0 / 255.0, 47.0 / 255.0);
        
        if (texIndex == 2)
        {
            texColor.rgb *= grassColor;
        }
        else if (texIndex == 3)
        {
            vec4 overlay = texture(uTextureArray, vec3(vTexCoord.xy, 4.0));
            overlay.rgb *= grassColor;
            texColor.rgb = mix(texColor.rgb, overlay.rgb, overlay.a);
        }
        else if (texIndex == 7)
        {
            texColor.rgb *= leavesColor;
        }

        vec3 lighting = texColor.rgb * v_ao;
        vec3 finalColor = mix(lighting, u_fogColor, v_fogFactor);

        FragColor = vec4(finalColor, 1.0);
    }
}
#version 460 core
layout (location = 0) out vec4 FragColor;

in vec2 v_texIndex;
in vec2 v_uv;
in float v_ao;
in float v_fogFactor;

uniform sampler2D uTexture;
uniform vec2 uTileAtlasSize; 
uniform float uTileSize;
uniform float uPixelPadding;

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
        vec2 BaseIndex = v_texIndex;
        vec2 TileRel = fract(v_uv);
        vec2 TileUVSize = vec2(uTileSize) / uTileAtlasSize;
        vec2 NormalizedPadding = vec2(uPixelPadding) / uTileAtlasSize;
        vec2 UV_Span = TileUVSize - 2.0 * NormalizedPadding;
        vec2 UV_Start = BaseIndex * TileUVSize + NormalizedPadding;
        vec2 FinalTexCoord = UV_Start + TileRel * UV_Span;
        
        vec4 texColor = texture(uTexture, FinalTexCoord);
        if (texColor.a < 0.1) discard;

        vec3 lighting = texColor.rgb * v_ao;

        vec3 finalColor = mix(lighting, u_fogColor, v_fogFactor);

        FragColor = vec4(finalColor, 1.0);
    }
}
#version 460 core
out vec4 FragColor;

in vec2 vTexIndex;
in vec2 vUV;

uniform sampler2D uTexture;
uniform vec2 uTileAtlasSize; 
uniform float uTileSize;
uniform float uPixelPadding;

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
        vec2 BaseIndex = vTexIndex;
        vec2 TileRel = fract(vUV);
        
        vec2 TileUVSize = vec2(uTileSize) / uTileAtlasSize;
        vec2 NormalizedPadding = vec2(uPixelPadding) / uTileAtlasSize;
        vec2 UV_Span = TileUVSize - 2.0 * NormalizedPadding;
        vec2 UV_Start = BaseIndex * TileUVSize + NormalizedPadding;
        
        vec2 FinalTexCoord = UV_Start + TileRel * UV_Span;
        
        FragColor = texture(uTexture, FinalTexCoord);
        if (FragColor.a < 0.1) discard;
    }
}
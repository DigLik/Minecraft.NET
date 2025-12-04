#version 460 core
layout (location = 0) out vec4 gNormal;
layout (location = 1) out vec4 gAlbedo;

in vec3 v_viewPos;
in vec2 v_texIndex;
in vec2 v_uv;
in float v_ao;

uniform sampler2D uTexture;
uniform vec2 uTileAtlasSize; 
uniform float uTileSize;
uniform float uPixelPadding;

void main() 
{
    vec3 viewNormal = normalize(cross(dFdx(v_viewPos), dFdy(v_viewPos)));
    gNormal = vec4(viewNormal, 1.0);

    vec2 BaseIndex = v_texIndex;
    vec2 TileRel = fract(v_uv);
    
    vec2 TileUVSize = vec2(uTileSize) / uTileAtlasSize;
    vec2 NormalizedPadding = vec2(uPixelPadding) / uTileAtlasSize;

    vec2 UV_Span = TileUVSize - 2.0 * NormalizedPadding;
    vec2 UV_Start = BaseIndex * TileUVSize + NormalizedPadding;

    vec2 FinalTexCoord = UV_Start + TileRel * UV_Span;
    
    gAlbedo = texture(uTexture, FinalTexCoord);
    gAlbedo.rgb *= v_ao;
    
    if (gAlbedo.a < 0.1) discard;
}
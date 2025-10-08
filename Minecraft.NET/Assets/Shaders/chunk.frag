#version 460 core
out vec4 FragColor;

in vec3 vWorldPos;
in vec2 vTexIndex;

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
        vec3 dx = dFdx(vWorldPos);
        vec3 dy = dFdy(vWorldPos);
        vec3 normal = normalize(cross(dx, dy));

        vec2 uv;
        if (abs(normal.x) > abs(normal.y) && abs(normal.x) > abs(normal.z))
            uv = vWorldPos.zy;
        else if (abs(normal.y) > abs(normal.z))
            uv = vWorldPos.xz;
        else
            uv = vWorldPos.xy;

        if (abs(normal.x) > abs(normal.y) && abs(normal.x) > abs(normal.z))
            uv.y = -uv.y;
        else if (abs(normal.z) > abs(normal.y))
            uv.y = -uv.y;

        vec2 BaseIndex = vTexIndex;
        vec2 TileRel = fract(uv);
        
        vec2 TileUVSize = vec2(uTileSize) / uTileAtlasSize;
        vec2 NormalizedPadding = vec2(uPixelPadding) / uTileAtlasSize;
        vec2 UV_Span = TileUVSize - 2.0 * NormalizedPadding;
        vec2 UV_Start = BaseIndex * TileUVSize + NormalizedPadding;
        
        vec2 FinalTexCoord = UV_Start + TileRel * UV_Span;
        
        FragColor = texture(uTexture, FinalTexCoord);
        if (FragColor.a < 0.1) discard;
    }
}
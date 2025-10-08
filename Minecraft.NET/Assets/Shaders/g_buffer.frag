#version 460 core
layout (location = 0) out vec4 gPosition;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gAlbedo;

in vec3 v_viewPos;
in vec2 v_texIndex;
in vec3 v_localPos;

uniform sampler2D uTexture;
uniform vec2 uTileAtlasSize; 
uniform float uTileSize;
uniform float uPixelPadding;
uniform mat4 inverseView;

void main() 
{
    gPosition = vec4(v_viewPos, 1.0);

    vec3 viewNormal = normalize(cross(dFdx(v_viewPos), dFdy(v_viewPos)));
    gNormal = vec4(viewNormal, 1.0);

    vec3 worldNormal = (inverseView * vec4(viewNormal, 0.0)).xyz;
    
    vec2 uv;
    if (abs(worldNormal.x) > abs(worldNormal.y) && abs(worldNormal.x) > abs(worldNormal.z))
        uv = v_localPos.zy;
    else if (abs(worldNormal.y) > abs(worldNormal.z))
        uv = v_localPos.xz;
    else
        uv = v_localPos.xy;

    if (abs(worldNormal.x) > abs(worldNormal.y) && abs(worldNormal.x) > abs(worldNormal.z))
        uv.y = -uv.y;
    else if (abs(worldNormal.z) > abs(worldNormal.y))
        uv.y = -uv.y;

    vec2 BaseIndex = v_texIndex;
    vec2 TileRel = fract(uv);
    
    vec2 TileUVSize = vec2(uTileSize) / uTileAtlasSize;
    vec2 NormalizedPadding = vec2(uPixelPadding) / uTileAtlasSize;
    vec2 UV_Span = TileUVSize - 2.0 * NormalizedPadding;
    vec2 UV_Start = BaseIndex * TileUVSize + NormalizedPadding;
    
    vec2 FinalTexCoord = UV_Start + TileRel * UV_Span;
    
    gAlbedo = texture(uTexture, FinalTexCoord);
    if (gAlbedo.a < 0.1) discard;
}
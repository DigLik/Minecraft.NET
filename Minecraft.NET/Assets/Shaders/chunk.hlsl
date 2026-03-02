#pragma pack_matrix(row_major)
    
struct VSInput
{
    [[vk::location(0)]] float4 Position : POSITION0;
    [[vk::location(1)]] float2 UV : TEXCOORD0;
    [[vk::location(2)]] int TextureIndex : TEXCOORD1;
    [[vk::location(3)]] float4 Color : COLOR0;
    [[vk::location(4)]] int OverlayTextureIndex : TEXCOORD2;
    [[vk::location(5)]] float4 OverlayColor : COLOR1;
    [[vk::location(6)]] float3 InstancePosition : POSITION1;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    [[vk::location(0)]] float2 UV : TEXCOORD0;
    [[vk::location(1)]] nointerpolation int TextureIndex : TEXCOORD1;
    [[vk::location(2)]] float4 Color : COLOR0;
    [[vk::location(3)]] nointerpolation int OverlayTextureIndex : TEXCOORD2;
    [[vk::location(4)]] float4 OverlayColor : COLOR1;
};

cbuffer UBO : register(b0, space0)
{
    float4x4 ViewProjection;
};

Texture2DArray Tex : register(t1, space0);
SamplerState Samp : register(s1, space0);

PSInput VSMain(VSInput input)
{
    PSInput output;
    
    float4 worldPos = float4(input.Position.xyz + input.InstancePosition, 1.0f);
    
    output.Position = mul(worldPos, ViewProjection);
    output.UV = input.UV;
    output.TextureIndex = input.TextureIndex;
    output.Color = input.Color;
    output.OverlayTextureIndex = input.OverlayTextureIndex;
    output.OverlayColor = input.OverlayColor;
    
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    float4 baseColor = Tex.Sample(Samp, float3(input.UV, input.TextureIndex)) * input.Color;
    
    if (input.OverlayTextureIndex >= 0)
    {
        float4 overlay = Tex.Sample(Samp, float3(input.UV, input.OverlayTextureIndex)) * input.OverlayColor;
        baseColor = lerp(baseColor, overlay, overlay.a);
    }
    
    if (baseColor.a < 0.1f)
        discard;
    
    return baseColor;
}
#pragma pack_matrix(row_major)

struct VSInput
{
    float4 Position : POSITION;
    float2 UV : TEXCOORD0;
    int TextureIndex : TEXCOORD1;
    float4 Color : COLOR0;
    int OverlayTextureIndex : BLENDINDICES;
    float4 OverlayColor : COLOR1;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR0;
    int TextureIndex : TEXCOORD1;
    int OverlayTextureIndex : BLENDINDICES;
    float4 OverlayColor : COLOR1;
};

cbuffer CameraData : register(b0)
{
    row_major float4x4 viewProj;
};

[[vk::push_constant]]
cbuffer PushData
{
    float3 chunkOffset;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    float3 worldPos = input.Position.xyz + chunkOffset;
    
    output.Position = mul(float4(worldPos, 1.0), viewProj);
    output.UV = input.UV;
    output.Color = input.Color;
    output.TextureIndex = input.TextureIndex;
    output.OverlayTextureIndex = input.OverlayTextureIndex;
    output.OverlayColor = input.OverlayColor;
    
    return output;
}

Texture2DArray albedoTextures : register(t1);
SamplerState albedoSampler : register(s1);

float4 PSMain(VSOutput input) : SV_Target
{
    float4 baseColor = albedoTextures.Sample(albedoSampler, float3(input.UV, input.TextureIndex)) * input.Color;
    
    if (input.OverlayTextureIndex >= 0)
    {
        float4 overlay = albedoTextures.Sample(albedoSampler, float3(input.UV, input.OverlayTextureIndex));
        baseColor.rgb = lerp(baseColor.rgb, overlay.rgb * input.OverlayColor.rgb, overlay.a);
    }
    
    if (baseColor.a < 0.1)
        discard;
    
    return baseColor;
}
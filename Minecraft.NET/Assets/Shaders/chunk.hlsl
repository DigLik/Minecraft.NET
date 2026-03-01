struct VSInput
{
    float3 Position : POSITION;
    float2 UV : TEXCOORD0;
    int TextureIndex : TEXCOORD1;
    float Shade : COLOR;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float Shade : COLOR;
};

cbuffer CameraData : register(b0)
{
    float4x4 viewProj;
};

[[vk::push_constant]]
cbuffer PushData
{
    float3 chunkOffset;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    float3 worldPos = input.Position + chunkOffset;
    output.Position = mul(viewProj, float4(worldPos, 1.0));
    output.UV = input.UV;
    output.Shade = input.Shade;
    return output;
}

float4 PSMain(VSOutput input) : SV_Target
{
    return float4(input.Shade * 0.5, input.Shade * 0.5, input.Shade * 0.5, 1.0);
}
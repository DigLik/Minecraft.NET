struct VSInput
{
    float3 Position : POSITION;
    float2 UV : TEXCOORD0;
    int TextureIndex : TEXCOORD1;
    float Shade : COLOR0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
    int TextureIndex : TEXCOORD1;
    float Shade : COLOR0;
};

cbuffer CameraBuffer : register(b0)
{
    matrix ViewProjection;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 1.0f), ViewProjection);
    output.UV = input.UV;
    output.TextureIndex = input.TextureIndex;
    output.Shade = input.Shade;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 debugColor = float3(input.UV.x, input.UV.y, 0.8f) * input.Shade;
    return float4(debugColor, 1.0f);
}
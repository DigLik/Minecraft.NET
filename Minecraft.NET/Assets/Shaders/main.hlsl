cbuffer Constants : register(b0)
{
    matrix Model;
    matrix View;
    matrix Projection;
    float4 WireframeColor;
    int UseWireframeColor;
    float3 Padding;
};

Texture2DArray blockTextures : register(t0);
SamplerState samLinear : register(s0);

struct VS_INPUT
{
    float3 Pos : POSITION;
    uint TexIndex : TEXCOORD0;
    float2 UV : TEXCOORD1;
};

struct PS_INPUT
{
    float4 Pos : SV_POSITION;
    float2 UV : TEXCOORD0;
    uint TexIndex : TEXCOORD1;
};

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    float4 worldPos = mul(float4(input.Pos, 1.0f), Model);
    float4 viewPos = mul(worldPos, View);
    output.Pos = mul(viewPos, Projection);
    
    output.UV = input.UV;
    output.TexIndex = input.TexIndex;
    
    return output;
}

float4 PS(PS_INPUT input) : SV_Target
{
    if (UseWireframeColor != 0)
    {
        return WireframeColor;
    }
    
    float4 texColor = blockTextures.Sample(samLinear, float3(input.UV, input.TexIndex));
    if (texColor.a < 0.1f)
        discard;
        
    return texColor;
}
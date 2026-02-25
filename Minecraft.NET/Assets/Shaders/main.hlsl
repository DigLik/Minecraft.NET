cbuffer FrameConstants : register(b0)
{
    matrix View;
    matrix Projection;
    float4 WireframeColor;
    int UseWireframeColor;
};

cbuffer ChunkData : register(b1)
{
    float3 ChunkOffset;
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
    
    float4 worldPos = float4(input.Pos + ChunkOffset, 1.0f);
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
        
    if (input.TexIndex == 2)
    {
        texColor.rgb *= float3(0.569f, 0.741f, 0.349f);
    }
    else if (input.TexIndex == 7)
    {
        texColor.rgb *= float3(0.467f, 0.671f, 0.184f);
    }
    else if (input.TexIndex == 3)
    {
        float4 overlay = blockTextures.Sample(samLinear, float3(input.UV, 4));
        
        if (overlay.a > 0.1f)
        {
            overlay.rgb *= float3(0.569f, 0.741f, 0.349f);
            texColor.rgb = lerp(texColor.rgb, overlay.rgb, overlay.a);
        }
    }
        
    return texColor;
}
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class Shader : IDisposable
{
    private readonly D3D11Context _d3d;
    public ComPtr<ID3D11VertexShader> VertexShader;
    public ComPtr<ID3D11PixelShader> PixelShader;
    public ComPtr<ID3D11InputLayout> InputLayout;

    private bool _isDisposed;

    public Shader(D3D11Context d3d, string path)
    {
        _d3d = d3d;
        string source = File.ReadAllText(path);

        ID3D10Blob* vsBlob = null;
        ID3D10Blob* psBlob = null;
        ID3D10Blob* errorBlob = null;

        var vsEntry = SilkMarshal.StringToPtr("VS");
        var psEntry = SilkMarshal.StringToPtr("PS");
        var vsTarget = SilkMarshal.StringToPtr("vs_5_0");
        var psTarget = SilkMarshal.StringToPtr("ps_5_0");
        var sourcePtr = SilkMarshal.StringToPtr(source);

        nuint sourceLen = (nuint)source.Length;

        _d3d.Compiler.Compile((void*)sourcePtr, sourceLen, (byte*)null, (D3DShaderMacro*)null, (ID3DInclude*)null, (byte*)vsEntry, (byte*)vsTarget, 0, 0, &vsBlob, &errorBlob);

        if (errorBlob != null)
        {
            Console.WriteLine("VS Compile Error: " + SilkMarshal.PtrToString((nint)errorBlob->GetBufferPointer()));
            errorBlob->Release();
        }

        fixed (ComPtr<ID3D11VertexShader>* vsPtr = &VertexShader)
            _d3d.Device.CreateVertexShader(vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), (ID3D11ClassLinkage*)null, (ID3D11VertexShader**)vsPtr);

        _d3d.Compiler.Compile((void*)sourcePtr, sourceLen, (byte*)null, (D3DShaderMacro*)null, (ID3DInclude*)null, (byte*)psEntry, (byte*)psTarget, 0, 0, &psBlob, &errorBlob);

        if (errorBlob != null)
        {
            Console.WriteLine("PS Compile Error: " + SilkMarshal.PtrToString((nint)errorBlob->GetBufferPointer()));
            errorBlob->Release();
        }

        fixed (ComPtr<ID3D11PixelShader>* psPtr = &PixelShader)
            _d3d.Device.CreatePixelShader(psBlob->GetBufferPointer(), psBlob->GetBufferSize(), (ID3D11ClassLinkage*)null, (ID3D11PixelShader**)psPtr);

        InputElementDesc* elements = stackalloc InputElementDesc[3];
        elements[0] = new InputElementDesc((byte*)SilkMarshal.StringToPtr("POSITION"), 0, Silk.NET.DXGI.Format.FormatR32G32B32Float, 0, 0, InputClassification.PerVertexData, 0);
        elements[1] = new InputElementDesc((byte*)SilkMarshal.StringToPtr("TEXCOORD"), 0, Silk.NET.DXGI.Format.FormatR32Uint, 0, 12, InputClassification.PerVertexData, 0);
        elements[2] = new InputElementDesc((byte*)SilkMarshal.StringToPtr("TEXCOORD"), 1, Silk.NET.DXGI.Format.FormatR32G32Float, 0, 16, InputClassification.PerVertexData, 0);

        fixed (ComPtr<ID3D11InputLayout>* layoutPtr = &InputLayout)
            _d3d.Device.CreateInputLayout(elements, 3, vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), (ID3D11InputLayout**)layoutPtr);

        vsBlob->Release();
        psBlob->Release();
        SilkMarshal.Free(vsEntry);
        SilkMarshal.Free(psEntry);
        SilkMarshal.Free(vsTarget);
        SilkMarshal.Free(psTarget);
        SilkMarshal.Free(sourcePtr);
        SilkMarshal.Free((nint)elements[0].SemanticName);
        SilkMarshal.Free((nint)elements[1].SemanticName);
        SilkMarshal.Free((nint)elements[2].SemanticName);
    }

    public void Bind()
    {
        _d3d.Context.IASetInputLayout(InputLayout);
        _d3d.Context.VSSetShader(VertexShader, null, 0);
        _d3d.Context.PSSetShader(PixelShader, null, 0);
        _d3d.Context.IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        VertexShader.Dispose();
        PixelShader.Dispose();
        InputLayout.Dispose();
    }
}
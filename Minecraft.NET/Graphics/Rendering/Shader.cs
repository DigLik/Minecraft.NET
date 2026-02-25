using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class Shader : IDisposable
{
    private readonly D3D12Context _d3d;

    public ComPtr<ID3D10Blob> VertexShader;
    public ComPtr<ID3D10Blob> PixelShader;
    public InputElementDesc[] InputLayout;

    private bool _isDisposed;

    public Shader(D3D12Context d3d, string path)
    {
        _d3d = d3d;

        string source = File.ReadAllText(path);
        var compiler = D3DCompiler.GetApi();

        ID3D10Blob* vsBlob = null;
        ID3D10Blob* psBlob = null;
        ID3D10Blob* errorBlob = null;

        var vsEntry = SilkMarshal.StringToPtr("VS");
        var psEntry = SilkMarshal.StringToPtr("PS");
        var vsTarget = SilkMarshal.StringToPtr("vs_5_0");
        var psTarget = SilkMarshal.StringToPtr("ps_5_0");
        var sourcePtr = SilkMarshal.StringToPtr(source);
        nuint sourceLen = (nuint)source.Length;

        int hr = compiler.Compile((void*)sourcePtr, sourceLen, (byte*)null, (D3DShaderMacro*)null, (ID3DInclude*)null, (byte*)vsEntry, (byte*)vsTarget, 0, 0, &vsBlob, &errorBlob);

        if (errorBlob != null)
        {
            string err = SilkMarshal.PtrToString((nint)errorBlob->GetBufferPointer());
            errorBlob->Release();
            throw new Exception($"Vertex Shader Compilation Error: {err}");
        }

        hr = compiler.Compile((void*)sourcePtr, sourceLen, (byte*)null, (D3DShaderMacro*)null, (ID3DInclude*)null, (byte*)psEntry, (byte*)psTarget, 0, 0, &psBlob, &errorBlob);

        if (errorBlob != null)
        {
            string err = SilkMarshal.PtrToString((nint)errorBlob->GetBufferPointer());
            errorBlob->Release();
            throw new Exception($"Pixel Shader Compilation Error: {err}");
        }

        VertexShader = new ComPtr<ID3D10Blob>(vsBlob);
        PixelShader = new ComPtr<ID3D10Blob>(psBlob);

        InputLayout = new InputElementDesc[3];
        InputLayout[0] = new InputElementDesc((byte*)SilkMarshal.StringToPtr("POSITION"), 0, Format.FormatR32G32B32Float, 0, 0, InputClassification.PerVertexData, 0);
        InputLayout[1] = new InputElementDesc((byte*)SilkMarshal.StringToPtr("TEXCOORD"), 0, Format.FormatR32Uint, 0, 12, InputClassification.PerVertexData, 0);
        InputLayout[2] = new InputElementDesc((byte*)SilkMarshal.StringToPtr("TEXCOORD"), 1, Format.FormatR32G32Float, 0, 16, InputClassification.PerVertexData, 0);

        SilkMarshal.Free(vsEntry);
        SilkMarshal.Free(psEntry);
        SilkMarshal.Free(vsTarget);
        SilkMarshal.Free(psTarget);
        SilkMarshal.Free(sourcePtr);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (VertexShader.Handle != null) VertexShader.Dispose();
        if (PixelShader.Handle != null) PixelShader.Dispose();

        if (InputLayout != null)
        {
            SilkMarshal.Free((nint)InputLayout[0].SemanticName);
            SilkMarshal.Free((nint)InputLayout[1].SemanticName);
            SilkMarshal.Free((nint)InputLayout[2].SemanticName);
        }
    }
}
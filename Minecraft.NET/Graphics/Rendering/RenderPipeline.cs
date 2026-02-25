using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Minecraft.NET.Character;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Engine;
using Minecraft.NET.Services;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace Minecraft.NET.Graphics.Rendering;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct FrameConstants
{
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Vector4 WireframeColor;
    public int UseWireframeColor;
}

public sealed unsafe partial class RenderPipeline(
    Player player,
    FrameContext frameContext,
    IChunkRenderer chunkRenderer,
    RenderSettings renderSettings,
    ChunkManager chunkManager,
    D3D12Context d3d) : IRenderPipeline
{
    public IChunkRenderer ChunkRenderer => chunkRenderer;

    private Shader _mainShader = null!;
    private TextureArray _blockTextures = null!;

    private ComPtr<ID3D12Resource> _constantBuffer;
    private byte* _mappedConstantBuffer;

    private ComPtr<ID3D12RootSignature> _rootSignature;
    private ComPtr<ID3D12PipelineState> _pipelineStateFill;
    private ComPtr<ID3D12PipelineState> _pipelineStateWireframe;

    private readonly Vector4[] _frustumPlanes = new Vector4[6];
    private bool _isDisposed;

    public const uint PresentAllowTearing = 512;

    [LibraryImport("d3d12.dll")]
    private static partial int D3D12SerializeRootSignature(RootSignatureDesc* pRootSignature, D3DRootSignatureVersion Version, ID3D10Blob** ppBlob, ID3D10Blob** ppErrorBlob);

    public void Initialize()
    {
        chunkRenderer.Initialize();
        _blockTextures = new TextureArray(d3d, BlockRegistry.TextureFiles);
        _mainShader = new Shader(d3d, "Assets/Shaders/main.hlsl");

        var device = d3d.Device.Handle;
        var parameters = stackalloc RootParameter[3];

        parameters[0] = new RootParameter { ParameterType = RootParameterType.TypeCbv, Descriptor = new RootDescriptor { ShaderRegister = 0, RegisterSpace = 0 }, ShaderVisibility = ShaderVisibility.All };
        var range = new DescriptorRange { RangeType = DescriptorRangeType.Srv, NumDescriptors = 1, BaseShaderRegister = 0, RegisterSpace = 0, OffsetInDescriptorsFromTableStart = 0 };
        parameters[1] = new RootParameter { ParameterType = RootParameterType.TypeDescriptorTable, DescriptorTable = new RootDescriptorTable { NumDescriptorRanges = 1, PDescriptorRanges = &range }, ShaderVisibility = ShaderVisibility.Pixel };
        parameters[2] = new RootParameter { ParameterType = RootParameterType.Type32BitConstants, Constants = new RootConstants { ShaderRegister = 1, RegisterSpace = 0, Num32BitValues = 3 }, ShaderVisibility = ShaderVisibility.Vertex };

        var sampler = new StaticSamplerDesc
        {
            Filter = Filter.MinMagMipPoint, AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap, MipLODBias = 0, MaxAnisotropy = 1, ComparisonFunc = ComparisonFunc.Never,
            BorderColor = StaticBorderColor.TransparentBlack, MinLOD = 0.0f, MaxLOD = float.MaxValue, ShaderRegister = 0, RegisterSpace = 0, ShaderVisibility = ShaderVisibility.Pixel
        };

        RootSignatureDesc rootSigDesc = new RootSignatureDesc
        {
            NumParameters = 3, PParameters = parameters, NumStaticSamplers = 1, PStaticSamplers = &sampler, Flags = RootSignatureFlags.AllowInputAssemblerInputLayout
        };

        ID3D10Blob* signatureBlob; ID3D10Blob* errorBlob;
        _ = D3D12SerializeRootSignature(&rootSigDesc, D3DRootSignatureVersion.Version1, &signatureBlob, &errorBlob);
        if (errorBlob != null) errorBlob->Release();

        var riidRootSig = SilkMarshal.GuidPtrOf<ID3D12RootSignature>();
        void* rootSigPtr;
        device->CreateRootSignature(0, signatureBlob->GetBufferPointer(), signatureBlob->GetBufferSize(), riidRootSig, &rootSigPtr);
        _rootSignature = new ComPtr<ID3D12RootSignature>((ID3D12RootSignature*)rootSigPtr);
        signatureBlob->Release();

        HeapProperties uploadHeap = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);
        ResourceDesc cbDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer, Alignment = 0, Width = 256 * D3D12Context.FrameCount, Height = 1, DepthOrArraySize = 1, MipLevels = 1,
            Format = Format.FormatUnknown, SampleDesc = new SampleDesc(1, 0), Layout = TextureLayout.LayoutRowMajor, Flags = ResourceFlags.None
        };

        var riidResource = SilkMarshal.GuidPtrOf<ID3D12Resource>(); void* cbPtr;
        device->CreateCommittedResource(&uploadHeap, HeapFlags.None, &cbDesc, ResourceStates.GenericRead, null, riidResource, &cbPtr);
        _constantBuffer = new ComPtr<ID3D12Resource>((ID3D12Resource*)cbPtr);

        void* mappedData;
        _constantBuffer.Handle->Map(0, null, &mappedData); _mappedConstantBuffer = (byte*)mappedData;

        GraphicsPipelineStateDesc psoDesc = new GraphicsPipelineStateDesc();
        fixed (InputElementDesc* pInputElements = &_mainShader.InputLayout[0])
        {
            psoDesc.InputLayout = new InputLayoutDesc { PInputElementDescs = pInputElements, NumElements = (uint)_mainShader.InputLayout.Length };
        }

        psoDesc.PRootSignature = _rootSignature.Handle;
        psoDesc.VS = new ShaderBytecode(_mainShader.VertexShader.Handle->GetBufferPointer(), _mainShader.VertexShader.Handle->GetBufferSize());
        psoDesc.PS = new ShaderBytecode(_mainShader.PixelShader.Handle->GetBufferPointer(), _mainShader.PixelShader.Handle->GetBufferSize());

        psoDesc.RasterizerState = new RasterizerDesc
        {
            FillMode = FillMode.Solid, CullMode = CullMode.Back, FrontCounterClockwise = 0, DepthBias = 0, DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0, DepthClipEnable = 1, MultisampleEnable = 0, AntialiasedLineEnable = 0, ForcedSampleCount = 0, ConservativeRaster = ConservativeRasterizationMode.Off
        };
        psoDesc.BlendState = new BlendDesc { AlphaToCoverageEnable = 0, IndependentBlendEnable = 0 };
        psoDesc.BlendState.RenderTarget[0] = new RenderTargetBlendDesc
        {
            BlendEnable = 0, LogicOpEnable = 0, SrcBlend = Blend.One, DestBlend = Blend.Zero, BlendOp = BlendOp.Add,
            SrcBlendAlpha = Blend.One, DestBlendAlpha = Blend.Zero, BlendOpAlpha = BlendOp.Add, LogicOp = LogicOp.Noop, RenderTargetWriteMask = 0xF
        };
        psoDesc.DepthStencilState = new DepthStencilDesc
        {
            DepthEnable = 1, DepthWriteMask = DepthWriteMask.All, DepthFunc = ComparisonFunc.Less, StencilEnable = 0,
            StencilReadMask = 0xFF, StencilWriteMask = 0xFF,
            FrontFace = new DepthStencilopDesc(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep, ComparisonFunc.Always),
            BackFace = new DepthStencilopDesc(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep, ComparisonFunc.Always)
        };
        psoDesc.SampleMask = uint.MaxValue; psoDesc.PrimitiveTopologyType = PrimitiveTopologyType.Triangle;
        psoDesc.NumRenderTargets = 1; psoDesc.RTVFormats.Element0 = Format.FormatR8G8B8A8Unorm;
        psoDesc.DSVFormat = Format.FormatD24UnormS8Uint; psoDesc.SampleDesc = new SampleDesc(1, 0);

        var riidPso = SilkMarshal.GuidPtrOf<ID3D12PipelineState>(); void* psoPtrFill;
        device->CreateGraphicsPipelineState(&psoDesc, riidPso, &psoPtrFill);
        _pipelineStateFill = new ComPtr<ID3D12PipelineState>((ID3D12PipelineState*)psoPtrFill);

        psoDesc.RasterizerState.FillMode = FillMode.Wireframe; psoDesc.RasterizerState.CullMode = CullMode.None;
        void* psoPtrWire; device->CreateGraphicsPipelineState(&psoDesc, riidPso, &psoPtrWire);
        _pipelineStateWireframe = new ComPtr<ID3D12PipelineState>((ID3D12PipelineState*)psoPtrWire);
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        frameContext.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);
    }

    private void ExtractFrustum(Matrix4x4 vp)
    {
        _frustumPlanes[0] = Vector4.Normalize(new Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41));
        _frustumPlanes[1] = Vector4.Normalize(new Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41));
        _frustumPlanes[2] = Vector4.Normalize(new Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42));
        _frustumPlanes[3] = Vector4.Normalize(new Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42));
        _frustumPlanes[4] = Vector4.Normalize(new Vector4(vp.M13, vp.M23, vp.M33, vp.M43));
        _frustumPlanes[5] = Vector4.Normalize(new Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBoxInFrustum(Vector3 min, Vector3 max)
    {
        for (int i = 0; i < 6; i++)
        {
            var p = _frustumPlanes[i];
            Vector3 axisVert = new Vector3(p.X < 0.0f ? min.X : max.X, p.Y < 0.0f ? min.Y : max.Y, p.Z < 0.0f ? min.Z : max.Z);
            if (Vector3.Dot(new Vector3(p.X, p.Y, p.Z), axisVert) + p.W < 0.0f) return false;
        }
        return true;
    }

    public void OnRender(double deltaTime)
    {
        if (frameContext.ViewportSize.X <= 0 || frameContext.ViewportSize.Y <= 0) return;

        UpdateCamera();
        ExtractFrustum(frameContext.ViewMatrix * frameContext.ProjectionMatrix);

        var cmdAllocator = (ID3D12CommandAllocator*)d3d.CommandAllocators[d3d.FrameIndex].Handle;
        var cmdList = d3d.CommandList.Handle;
        var cmdQueue = d3d.CommandQueue.Handle;
        var swapChain = d3d.SwapChain.Handle;

        cmdAllocator->Reset();
        cmdList->Reset(cmdAllocator, renderSettings.IsWireframeEnabled ? _pipelineStateWireframe.Handle : _pipelineStateFill.Handle);

        if (chunkRenderer is ChunkRenderer cr)
        {
            cr.FlushUploads(cmdList);
        }

        var backBuffer = d3d.RenderTargets[d3d.FrameIndex];
        var barrier = new ResourceBarrier
        {
            Type = ResourceBarrierType.Transition, Flags = ResourceBarrierFlags.None,
            Transition = new ResourceTransitionBarrier { PResource = backBuffer, StateBefore = ResourceStates.Present, StateAfter = ResourceStates.RenderTarget, Subresource = uint.MaxValue }
        };
        cmdList->ResourceBarrier(1, &barrier);

        var rtvHandle = d3d.RtvHeap.Handle->GetCPUDescriptorHandleForHeapStart();
        rtvHandle.Ptr += d3d.FrameIndex * d3d.RtvDescriptorSize;
        var dsvHandle = d3d.DsvHeap.Handle->GetCPUDescriptorHandleForHeapStart();

        float* clearColor = stackalloc float[4] { 0.53f, 0.81f, 0.92f, 1.0f };
        cmdList->ClearRenderTargetView(rtvHandle, clearColor, 0, null);
        cmdList->ClearDepthStencilView(dsvHandle, ClearFlags.Depth, 1.0f, 0, 0, null);

        cmdList->OMSetRenderTargets(1, &rtvHandle, 0, &dsvHandle);

        Viewport viewport = new Viewport(0, 0, frameContext.ViewportSize.X, frameContext.ViewportSize.Y, 0.0f, 1.0f);
        Box2D<int> scissor = new Box2D<int>(0, 0, (int)frameContext.ViewportSize.X, (int)frameContext.ViewportSize.Y);
        cmdList->RSSetViewports(1, &viewport); cmdList->RSSetScissorRects(1, &scissor);

        cmdList->SetGraphicsRootSignature(_rootSignature.Handle);
        cmdList->IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);

        _blockTextures.Bind(cmdList, 1);

        FrameConstants frameConstants = new FrameConstants
        {
            View = Matrix4x4.Transpose(frameContext.ViewMatrix), Projection = Matrix4x4.Transpose(frameContext.ProjectionMatrix),
            WireframeColor = new Vector4(0, 0, 0, 1), UseWireframeColor = renderSettings.IsWireframeEnabled ? 1 : 0
        };

        uint cbOffset = d3d.FrameIndex * 256;
        Unsafe.CopyBlock(_mappedConstantBuffer + cbOffset, Unsafe.AsPointer(ref frameConstants), (uint)sizeof(FrameConstants));
        cmdList->SetGraphicsRootConstantBufferView(0, _constantBuffer.Handle->GetGPUVirtualAddress() + cbOffset);

        float* offset = stackalloc float[3];
        var chunks = chunkManager.GetRenderChunks();

        // Сняли искусственный лимит на количество отрисовываемых чанков за кадр
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            float chunkWorldX = chunk.Position.X * 16f;
            float chunkWorldZ = chunk.Position.Y * 16f;

            Vector3 chunkMin = new Vector3(chunkWorldX, -VerticalChunkOffset * 16f, chunkWorldZ);
            Vector3 chunkMax = new Vector3(chunkWorldX + 16f, (WorldHeightInChunks - VerticalChunkOffset) * 16f, chunkWorldZ + 16f);

            if (!IsBoxInFrustum(chunkMin, chunkMax)) continue;

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                if (chunk.MeshGeometries[y].IndexCount > 0)
                {
                    float chunkWorldY = (y - VerticalChunkOffset) * 16f;
                    offset[0] = chunkWorldX; offset[1] = chunkWorldY; offset[2] = chunkWorldZ;

                    cmdList->SetGraphicsRoot32BitConstants(2, 3, offset, 0);
                    chunkRenderer.DrawChunk(chunk.MeshGeometries[y]);
                }
            }
        }

        barrier.Transition.StateBefore = ResourceStates.RenderTarget;
        barrier.Transition.StateAfter = ResourceStates.Present;
        cmdList->ResourceBarrier(1, &barrier);
        cmdList->Close();

        ID3D12CommandList* pCmd = (ID3D12CommandList*)cmdList;
        cmdQueue->ExecuteCommandLists(1, &pCmd);

        uint presentFlags = d3d.IsTearingSupported ? PresentAllowTearing : 0u;
        int hr = swapChain->Present(0, presentFlags);

        if (hr < 0)
        {
            Console.WriteLine($"[CRITICAL ERROR] DXGI Present failed! HRESULT: {hr:X}");
            if (hr == unchecked((int)0x887A0005)) Console.WriteLine($"[GPU CRASH] Device Removed Reason: {d3d.Device.Get().GetDeviceRemovedReason():X}");
            Environment.Exit(-1);
        }

        d3d.MoveToNextFrame();
    }

    private void UpdateCamera()
    {
        var camera = player.Camera;
        float aspect = frameContext.ViewportSize.Y > 0 ? frameContext.ViewportSize.X / frameContext.ViewportSize.Y : 1.0f;
        frameContext.ProjectionMatrix = camera.GetProjectionMatrix(aspect);
        frameContext.ViewMatrix = camera.GetViewMatrix();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_constantBuffer.Handle != null) _constantBuffer.Handle->Unmap(0, null);
        _mainShader?.Dispose(); _blockTextures?.Dispose(); chunkRenderer?.Dispose();
        if (_constantBuffer.Handle != null) _constantBuffer.Dispose();
        if (_rootSignature.Handle != null) _rootSignature.Dispose();
        if (_pipelineStateFill.Handle != null) _pipelineStateFill.Dispose();
        if (_pipelineStateWireframe.Handle != null) _pipelineStateWireframe.Dispose();
    }
}
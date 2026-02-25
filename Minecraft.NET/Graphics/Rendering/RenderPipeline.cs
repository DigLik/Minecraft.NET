using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Minecraft.NET.Character;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Engine;
using Minecraft.NET.Services;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace Minecraft.NET.Graphics.Rendering;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct ShaderConstants
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Vector4 WireframeColor;
    public int UseWireframeColor;
    public Vector3 Padding;
}

public sealed unsafe class RenderPipeline(
    Player player,
    FrameContext frameContext,
    IChunkRenderer chunkRenderer,
    ChunkManager chunkManager,
    RenderSettings renderSettings,
    D3D11Context d3d) : IRenderPipeline
{
    public IChunkRenderer ChunkRenderer => chunkRenderer;

    private Shader _mainShader = null!;
    private TextureArray _blockTextures = null!;
    private ComPtr<ID3D11Buffer> _constantBuffer;
    private ComPtr<ID3D11RasterizerState> _rasterizerStateFill;
    private ComPtr<ID3D11RasterizerState> _rasterizerStateWireframe;
    private ComPtr<ID3D11DepthStencilState> _depthState;

    private bool _isDisposed;

    public void Initialize()
    {
        chunkRenderer.Initialize();

        _blockTextures = new TextureArray(d3d, BlockRegistry.TextureFiles);
        _mainShader = new Shader(d3d, "Assets/Shaders/main.hlsl");

        BufferDesc cbDesc = new BufferDesc
        {
            ByteWidth = (uint)sizeof(ShaderConstants),
            Usage = Usage.Dynamic,
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = (uint)CpuAccessFlag.Write
        };
        fixed (ComPtr<ID3D11Buffer>* cbPtr = &_constantBuffer)
            d3d.Device.CreateBuffer(&cbDesc, (SubresourceData*)null, (ID3D11Buffer**)cbPtr);

        RasterizerDesc rsFillDesc = new RasterizerDesc
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.Back,
            FrontCounterClockwise = false,
            DepthClipEnable = true
        };
        fixed (ComPtr<ID3D11RasterizerState>* rsPtr = &_rasterizerStateFill)
            d3d.Device.CreateRasterizerState(&rsFillDesc, (ID3D11RasterizerState**)rsPtr);

        RasterizerDesc rsWireDesc = new RasterizerDesc
        {
            FillMode = FillMode.Wireframe,
            CullMode = CullMode.None,
            FrontCounterClockwise = false,
            DepthClipEnable = true,
            DepthBias = -1000,
            SlopeScaledDepthBias = -1.0f
        };
        fixed (ComPtr<ID3D11RasterizerState>* rsPtr = &_rasterizerStateWireframe)
            d3d.Device.CreateRasterizerState(&rsWireDesc, (ID3D11RasterizerState**)rsPtr);

        DepthStencilDesc dsDesc = new DepthStencilDesc
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunc.Greater
        };
        fixed (ComPtr<ID3D11DepthStencilState>* dsPtr = &_depthState)
            d3d.Device.CreateDepthStencilState(&dsDesc, (ID3D11DepthStencilState**)dsPtr);
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        frameContext.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);
    }

    public void OnRender(double deltaTime)
    {
        UpdateCamera();

        float* clearColor = stackalloc float[4] { 0.53f, 0.81f, 0.92f, 1.0f };
        ID3D11RenderTargetView* rtv = d3d.RenderTargetView;
        d3d.Context.ClearRenderTargetView(rtv, clearColor);
        d3d.Context.ClearDepthStencilView(d3d.DepthStencilView, (uint)ClearFlag.Depth, 0.0f, 0);

        d3d.Context.OMSetRenderTargets(1, &rtv, d3d.DepthStencilView);
        d3d.Context.OMSetDepthStencilState(_depthState, 0);

        Viewport vp = new Viewport
        {
            TopLeftX = 0,
            TopLeftY = 0,
            Width = frameContext.ViewportSize.X,
            Height = frameContext.ViewportSize.Y,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        d3d.Context.RSSetViewports(1, &vp);

        _mainShader.Bind();
        _blockTextures.Bind(0);

        fixed (ComPtr<ID3D11Buffer>* cbPtr = &_constantBuffer)
        {
            d3d.Context.VSSetConstantBuffers(0, 1, (ID3D11Buffer**)cbPtr);
            d3d.Context.PSSetConstantBuffers(0, 1, (ID3D11Buffer**)cbPtr);
        }

        if (renderSettings.IsWireframeEnabled)
            d3d.Context.RSSetState(_rasterizerStateWireframe);
        else
            d3d.Context.RSSetState(_rasterizerStateFill);

        ShaderConstants constants = new ShaderConstants
        {
            View = Matrix4x4.Transpose(frameContext.ViewMatrix),
            Projection = Matrix4x4.Transpose(frameContext.ProjectionMatrix),
            UseWireframeColor = renderSettings.IsWireframeEnabled ? 1 : 0,
            WireframeColor = new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
        };

        foreach (var chunk in chunkManager.GetRenderChunks())
        {
            float colX = chunk.Position.X * ChunkSize;
            float colZ = chunk.Position.Y * ChunkSize;

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                var geometry = chunk.MeshGeometries[y];
                if (geometry.IndexCount == 0) continue;

                float colY = y * ChunkSize - VerticalChunkOffset * ChunkSize;
                constants.Model = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(new Vector3(colX, colY, colZ)));

                MappedSubresource mapped;
                d3d.Context.Map((ID3D11Resource*)_constantBuffer.Handle, 0, Map.WriteDiscard, 0, &mapped);
                Unsafe.Write(mapped.PData, constants);
                d3d.Context.Unmap((ID3D11Resource*)_constantBuffer.Handle, 0);

                chunkRenderer.DrawChunk(geometry);
            }
        }

        d3d.SwapChain.Present(1, 0);
    }

    private void UpdateCamera()
    {
        var camera = player.Camera;
        float aspect = frameContext.ViewportSize.Y > 0
            ? frameContext.ViewportSize.X / frameContext.ViewportSize.Y
            : 1.0f;

        frameContext.ProjectionMatrix = camera.GetProjectionMatrix(aspect);
        frameContext.ViewMatrix = camera.GetViewMatrix();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _mainShader?.Dispose();
        _blockTextures?.Dispose();
        chunkRenderer?.Dispose();
        _constantBuffer.Dispose();
        _rasterizerStateFill.Dispose();
        _rasterizerStateWireframe.Dispose();
        _depthState.Dispose();
    }
}
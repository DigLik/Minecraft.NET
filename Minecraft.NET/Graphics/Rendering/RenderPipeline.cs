using System.Runtime.InteropServices;

using Minecraft.NET.Character;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Engine;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

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
    D3D12Context d3d) : IRenderPipeline
{
    public IChunkRenderer ChunkRenderer => chunkRenderer;

    private Shader _mainShader = null!;
    private TextureArray _blockTextures = null!;

    private ComPtr<ID3D12Resource> _constantBuffer;
    private ComPtr<ID3D12RootSignature> _rootSignature;
    private ComPtr<ID3D12PipelineState> _pipelineStateFill;
    private ComPtr<ID3D12PipelineState> _pipelineStateWireframe;

    private bool _isDisposed;

    public void Initialize()
    {
        chunkRenderer.Initialize();
        _blockTextures = new TextureArray(d3d, BlockRegistry.TextureFiles);
        _mainShader = new Shader(d3d, "Assets/Shaders/main.hlsl");
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        frameContext.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);
    }

    public void OnRender(double deltaTime)
    {
        UpdateCamera();

        var cmdAllocator = d3d.CommandAllocator.Handle;
        var cmdList = d3d.CommandList.Handle;
        var cmdQueue = d3d.CommandQueue.Handle;
        var swapChain = d3d.SwapChain.Handle;

        cmdAllocator->Reset();
        cmdList->Reset(cmdAllocator, null);

        var backBuffer = d3d.RenderTargets[d3d.FrameIndex];

        var barrier = new ResourceBarrier
        {
            Type = ResourceBarrierType.Transition,
            Flags = ResourceBarrierFlags.None,
            Transition = new ResourceTransitionBarrier
            {
                PResource = backBuffer,
                StateBefore = ResourceStates.Present,
                StateAfter = ResourceStates.RenderTarget,
                Subresource = uint.MaxValue
            }
        };
        cmdList->ResourceBarrier(1, &barrier);

        var rtvHeap = d3d.RtvHeap.Handle;
        var rtvHandle = rtvHeap->GetCPUDescriptorHandleForHeapStart();
        rtvHandle.Ptr += d3d.FrameIndex * d3d.RtvDescriptorSize;

        var dsvHeap = d3d.DsvHeap.Handle;
        var dsvHandle = dsvHeap->GetCPUDescriptorHandleForHeapStart();

        float* clearColor = stackalloc float[4] { 0.53f, 0.81f, 0.92f, 1.0f };
        cmdList->ClearRenderTargetView(rtvHandle, clearColor, 0, null);
        cmdList->ClearDepthStencilView(dsvHandle, ClearFlags.Depth, 1.0f, 0, 0, null);

        barrier.Transition.StateBefore = ResourceStates.RenderTarget;
        barrier.Transition.StateAfter = ResourceStates.Present;
        cmdList->ResourceBarrier(1, &barrier);

        cmdList->Close();

        ID3D12CommandList** ppCommandLists = stackalloc ID3D12CommandList*[1] { (ID3D12CommandList*)cmdList };
        cmdQueue->ExecuteCommandLists(1, ppCommandLists);

        swapChain->Present(1, 0);

        d3d.WaitForGpu();
        d3d.FrameIndex = swapChain->GetCurrentBackBufferIndex();
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

        if (_constantBuffer.Handle != null) _constantBuffer.Dispose();
        if (_rootSignature.Handle != null) _rootSignature.Dispose();
        if (_pipelineStateFill.Handle != null) _pipelineStateFill.Dispose();
        if (_pipelineStateWireframe.Handle != null) _pipelineStateWireframe.Dispose();
    }
}
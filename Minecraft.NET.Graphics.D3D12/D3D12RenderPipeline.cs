using System.Runtime.CompilerServices;
using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Utils.Math;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Minecraft.NET.Graphics.D3D12.Core;

namespace Minecraft.NET.Graphics.D3D12;

public unsafe class D3D12RenderPipeline : IRenderPipeline
{
    private readonly D3D12GraphicsDevice _device;
    private readonly D3D12SwapChain _swapChain;

    private ComPtr<ID3D12CommandAllocator> _commandAllocator;
    private ComPtr<ID3D12GraphicsCommandList> _commandList;

    public D3D12RenderPipeline(IWindow window)
    {
        _device = new D3D12GraphicsDevice();
        _swapChain = new D3D12SwapChain(_device, window);

        _device.Device.Get().CreateCommandAllocator(CommandListType.Direct, out _commandAllocator);
        _device.Device.Get().CreateCommandList(0, CommandListType.Direct, ref _commandAllocator.Get(), ref Unsafe.NullRef<ID3D12PipelineState>(), out _commandList);
        _commandList.Get().Close();
    }

    public void OnRender(double deltaTime)
    {
        _commandAllocator.Get().Reset();
        _commandList.Get().Reset(ref _commandAllocator.Get(), ref Unsafe.NullRef<ID3D12PipelineState>());

        int frameIndex = _swapChain.CurrentFrameIndex;

        ResourceBarrier barrierToRenderTarget = new()
        {
            Type = ResourceBarrierType.Transition,
            Flags = ResourceBarrierFlags.None,
            Transition = new ResourceTransitionBarrier((ID3D12Resource*)Unsafe.AsPointer(ref _swapChain.RenderTargets[frameIndex].Get()), 0, ResourceStates.Present, ResourceStates.RenderTarget)
        };
        _commandList.Get().ResourceBarrier(1, ref barrierToRenderTarget);

        CpuDescriptorHandle rtvHandle = _swapChain.RtvHeap.Get().GetCPUDescriptorHandleForHeapStart();
        rtvHandle.Ptr += (nuint)frameIndex * _swapChain.RtvDescriptorSize;

        float* clearColor = stackalloc float[4] { 0.4f, 0.6f, 0.9f, 1.0f };
        _commandList.Get().ClearRenderTargetView(rtvHandle, clearColor, 0, null);

        ResourceBarrier barrierToPresent = new()
        {
            Type = ResourceBarrierType.Transition,
            Flags = ResourceBarrierFlags.None,
            Transition = new ResourceTransitionBarrier((ID3D12Resource*)Unsafe.AsPointer(ref _swapChain.RenderTargets[frameIndex].Get()), 0, ResourceStates.RenderTarget, ResourceStates.Present)
        };
        _commandList.Get().ResourceBarrier(1, ref barrierToPresent);

        _commandList.Get().Close();
        ID3D12CommandList* ppCommandLists = (ID3D12CommandList*)Unsafe.AsPointer(ref _commandList.Get());
        _device.CommandQueue.Get().ExecuteCommandLists(1, &ppCommandLists);

        _swapChain.Present(1);
        _device.Flush();
    }

    public void OnFramebufferResize(Vector2<int> newSize)
    {
        Console.WriteLine($"[DX12] Resize to {newSize}");
    }

    public void Dispose()
    {
        _device.Flush();
        _commandList.Dispose();
        _commandAllocator.Dispose();
        _swapChain.Dispose();
        _device.Dispose();
    }
}
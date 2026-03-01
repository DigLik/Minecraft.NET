using System.Runtime.CompilerServices;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using D3D12Api = Silk.NET.Direct3D12.D3D12;
using DxgiApi = Silk.NET.DXGI.DXGI;

namespace Minecraft.NET.Graphics.D3D12.Core;

public unsafe class D3D12GraphicsDevice : IDisposable
{
    public D3D12Api D3D12 { get; }
    public DxgiApi DXGI { get; }

    public ComPtr<IDXGIFactory4> Factory;
    public ComPtr<ID3D12Device> Device;
    public ComPtr<ID3D12CommandQueue> CommandQueue;
    public ComPtr<ID3D12Fence> Fence;

    private ulong _fenceValue = 1;
    private readonly void* _fenceEvent;

    public D3D12GraphicsDevice()
    {
        D3D12 = D3D12Api.GetApi();
        DXGI = DxgiApi.GetApi(null);

#if DEBUG
        ComPtr<ID3D12Debug> debugController = default;
        if (D3D12.GetDebugInterface(out debugController).IsSuccess)
        {
            debugController.Get().EnableDebugLayer();
            debugController.Dispose();
        }
#endif

        DXGI.CreateDXGIFactory2(0, out Factory);
        D3D12.CreateDevice(ref Unsafe.NullRef<IUnknown>(), D3DFeatureLevel.Level110, out Device);

        CommandQueueDesc queueDesc = new()
        {
            Type = CommandListType.Direct,
            Flags = CommandQueueFlags.None
        };
        Device.Get().CreateCommandQueue(ref queueDesc, out CommandQueue);

        Device.Get().CreateFence(0, FenceFlags.None, out Fence);
        _fenceEvent = Win32Native.CreateEventW(null, false, false, null);
    }

    public void Flush()
    {
        ulong fenceToWaitFor = _fenceValue;
        CommandQueue.Get().Signal(ref Fence.Get(), fenceToWaitFor);
        _fenceValue++;

        if (Fence.Get().GetCompletedValue() < fenceToWaitFor)
        {
            Fence.Get().SetEventOnCompletion(fenceToWaitFor, _fenceEvent);
            Win32Native.WaitForSingleObject(_fenceEvent, 0xFFFFFFFF);
        }
    }

    public void Dispose()
    {
        Flush();
        Win32Native.CloseHandle(_fenceEvent);
        Fence.Dispose();
        CommandQueue.Dispose();
        Device.Dispose();
        Factory.Dispose();
        DXGI.Dispose();
        D3D12.Dispose();
    }
}
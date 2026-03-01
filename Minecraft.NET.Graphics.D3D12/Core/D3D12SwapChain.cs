using System.Runtime.CompilerServices;

using Minecraft.NET.Engine.Abstractions;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Minecraft.NET.Graphics.D3D12.Core;

public unsafe class D3D12SwapChain : IDisposable
{
    public const int FrameCount = 2;

    public ComPtr<IDXGISwapChain3> SwapChain;
    public ComPtr<ID3D12DescriptorHeap> RtvHeap;
    public readonly ComPtr<ID3D12Resource>[] RenderTargets = new ComPtr<ID3D12Resource>[FrameCount];

    public uint RtvDescriptorSize { get; }
    public int CurrentFrameIndex => (int)SwapChain.Get().GetCurrentBackBufferIndex();

    public D3D12SwapChain(D3D12GraphicsDevice device, IWindow window)
    {
        SwapChainDesc1 swapChainDesc = new()
        {
            BufferCount = FrameCount,
            Width = (uint)Math.Max(1, window.Size.X),
            Height = (uint)Math.Max(1, window.Size.Y),
            Format = Format.FormatR8G8B8A8Unorm,
            BufferUsage = DXGI.UsageRenderTargetOutput,
            SwapEffect = SwapEffect.FlipDiscard,
            SampleDesc = new SampleDesc(1, 0)
        };

        IDXGISwapChain1* pTempSwapChain = null;

        int hr = device.Factory.Get().CreateSwapChainForHwnd(
            (IUnknown*)Unsafe.AsPointer(ref device.CommandQueue.Get()),
            window.Win32Handle,
            &swapChainDesc,
            null, null,
            &pTempSwapChain);

        if (hr < 0) throw new Exception($"[DX12] CreateSwapChainForHwnd failed! HRESULT: 0x{hr:X8}");

        Guid swapChain3Guid = IDXGISwapChain3.Guid;
        void* pSwapChain3 = null;
        hr = pTempSwapChain->QueryInterface(&swapChain3Guid, &pSwapChain3);
        if (hr < 0) throw new Exception($"[DX12] QueryInterface for SwapChain3 failed! HRESULT: 0x{hr:X8}");

        SwapChain = new ComPtr<IDXGISwapChain3>((IDXGISwapChain3*)pSwapChain3);
        pTempSwapChain->Release();

        DescriptorHeapDesc rtvHeapDesc = new()
        {
            NumDescriptors = FrameCount,
            Type = DescriptorHeapType.Rtv,
            Flags = DescriptorHeapFlags.None
        };
        device.Device.Get().CreateDescriptorHeap(ref rtvHeapDesc, out RtvHeap);
        RtvDescriptorSize = device.Device.Get().GetDescriptorHandleIncrementSize(DescriptorHeapType.Rtv);

        CpuDescriptorHandle rtvHandle = RtvHeap.Get().GetCPUDescriptorHandleForHeapStart();
        for (uint i = 0; i < FrameCount; i++)
        {
            SwapChain.Get().GetBuffer(i, out RenderTargets[i]);
            device.Device.Get().CreateRenderTargetView((ID3D12Resource*)Unsafe.AsPointer(ref RenderTargets[i].Get()), null, rtvHandle);
            rtvHandle.Ptr += RtvDescriptorSize;
        }
    }

    public void Present(uint syncInterval = 1, uint flags = 0)
    {
        SwapChain.Get().Present(syncInterval, flags);
    }

    public void Dispose()
    {
        for (int i = 0; i < FrameCount; i++) RenderTargets[i].Dispose();
        RtvHeap.Dispose();
        SwapChain.Dispose();
    }
}
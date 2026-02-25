using System.Runtime.InteropServices;

using Minecraft.NET.Windowing;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Minecraft.NET.Graphics.Rendering;

public unsafe partial class D3D12Context : IDisposable
{
    public D3D12 D3D;
    public DXGI Dxgi;

    public ComPtr<ID3D12Device> Device;
    public ComPtr<ID3D12CommandQueue> CommandQueue;
    public ComPtr<IDXGISwapChain3> SwapChain;
    public ComPtr<ID3D12GraphicsCommandList> CommandList;

    public ComPtr<ID3D12CommandAllocator>[] CommandAllocators;

    public ComPtr<ID3D12DescriptorHeap> RtvHeap;
    public ComPtr<ID3D12DescriptorHeap> DsvHeap;

    public ID3D12Resource*[] RenderTargets;
    public ComPtr<ID3D12Resource> DepthStencilBuffer;

    public ComPtr<ID3D12Fence> Fence;
    public void* FenceEvent;

    public ulong[] FenceValues;

    public uint RtvDescriptorSize;
    public uint FrameIndex;
    public const int FrameCount = 2;

    private bool _isDisposed;
    private readonly IWindow _window;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void* CreateEventEx(void* lpEventAttributes, string? lpName, uint dwFlags, uint dwDesiredAccess);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(void* hHandle, uint dwMilliseconds);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool CloseHandle(void* hObject);

    public D3D12Context(IWindow window)
    {
        _window = window;
        RenderTargets = new ID3D12Resource*[FrameCount];
        FenceValues = new ulong[FrameCount];

        D3D = D3D12.GetApi();
        Dxgi = DXGI.GetApi(null);

        var riidFactory = SilkMarshal.GuidPtrOf<IDXGIFactory4>();
        void* factoryPtr;
        Dxgi.CreateDXGIFactory1(riidFactory, &factoryPtr);
        var factory = new ComPtr<IDXGIFactory4>((IDXGIFactory4*)factoryPtr);

        var riidDevice = SilkMarshal.GuidPtrOf<ID3D12Device>();
        void* devicePtr;
        D3D.CreateDevice(null, D3DFeatureLevel.Level110, riidDevice, &devicePtr);
        Device = new ComPtr<ID3D12Device>((ID3D12Device*)devicePtr);

        CommandQueueDesc queueDesc = new CommandQueueDesc { Type = CommandListType.Direct, Flags = CommandQueueFlags.None };
        var riidQueue = SilkMarshal.GuidPtrOf<ID3D12CommandQueue>();
        void* queuePtr;
        Device.Get().CreateCommandQueue(&queueDesc, riidQueue, &queuePtr);
        CommandQueue = new ComPtr<ID3D12CommandQueue>((ID3D12CommandQueue*)queuePtr);

        SwapChainDesc1 swapChainDesc = new SwapChainDesc1
        {
            BufferCount = FrameCount, Width = (uint)window.Size.X, Height = (uint)window.Size.Y,
            Format = Format.FormatR8G8B8A8Unorm, BufferUsage = DXGI.UsageRenderTargetOutput,
            SwapEffect = SwapEffect.FlipDiscard, SampleDesc = new SampleDesc(1, 0)
        };

        void* swapChainPtr;
        factory.Get().CreateSwapChainForHwnd((IUnknown*)CommandQueue.Handle, window.Win32Handle, &swapChainDesc, null, null, (IDXGISwapChain1**)&swapChainPtr);
        factory.Get().MakeWindowAssociation(window.Win32Handle, 2);
        SwapChain = new ComPtr<IDXGISwapChain3>((IDXGISwapChain3*)swapChainPtr);
        FrameIndex = SwapChain.Get().GetCurrentBackBufferIndex();

        DescriptorHeapDesc rtvHeapDesc = new DescriptorHeapDesc { NumDescriptors = FrameCount, Type = DescriptorHeapType.Rtv, Flags = DescriptorHeapFlags.None };
        var riidHeap = SilkMarshal.GuidPtrOf<ID3D12DescriptorHeap>();
        void* rtvHeapPtr;
        Device.Get().CreateDescriptorHeap(&rtvHeapDesc, riidHeap, &rtvHeapPtr);
        RtvHeap = new ComPtr<ID3D12DescriptorHeap>((ID3D12DescriptorHeap*)rtvHeapPtr);
        RtvDescriptorSize = Device.Get().GetDescriptorHandleIncrementSize(DescriptorHeapType.Rtv);

        DescriptorHeapDesc dsvHeapDesc = new DescriptorHeapDesc { NumDescriptors = 1, Type = DescriptorHeapType.Dsv, Flags = DescriptorHeapFlags.None };
        void* dsvHeapPtr;
        Device.Get().CreateDescriptorHeap(&dsvHeapDesc, riidHeap, &dsvHeapPtr);
        DsvHeap = new ComPtr<ID3D12DescriptorHeap>((ID3D12DescriptorHeap*)dsvHeapPtr);

        CommandAllocators = new ComPtr<ID3D12CommandAllocator>[FrameCount];
        var riidAllocator = SilkMarshal.GuidPtrOf<ID3D12CommandAllocator>();
        for (int i = 0; i < FrameCount; i++)
        {
            void* allocatorPtr;
            Device.Get().CreateCommandAllocator(CommandListType.Direct, riidAllocator, &allocatorPtr);
            CommandAllocators[i] = new ComPtr<ID3D12CommandAllocator>((ID3D12CommandAllocator*)allocatorPtr);
        }

        var riidList = SilkMarshal.GuidPtrOf<ID3D12GraphicsCommandList>();
        void* listPtr;
        Device.Get().CreateCommandList(0, CommandListType.Direct, CommandAllocators[0].Handle, null, riidList, &listPtr);
        CommandList = new ComPtr<ID3D12GraphicsCommandList>((ID3D12GraphicsCommandList*)listPtr);
        CommandList.Get().Close();

        var riidFence = SilkMarshal.GuidPtrOf<ID3D12Fence>();
        void* fencePtr;
        Device.Get().CreateFence(0, FenceFlags.None, riidFence, &fencePtr);
        Fence = new ComPtr<ID3D12Fence>((ID3D12Fence*)fencePtr);
        FenceValues[0] = 1;

        FenceEvent = CreateEventEx(null, null, 0, 0x1F0003);

        CreateRenderTargets((uint)window.Size.X, (uint)window.Size.Y);
        factory.Dispose();
    }

    public void CreateRenderTargets(uint width, uint height)
    {
        var rtvHandle = RtvHeap.Get().GetCPUDescriptorHandleForHeapStart();
        var riidResource = SilkMarshal.GuidPtrOf<ID3D12Resource>();

        for (uint i = 0; i < FrameCount; i++)
        {
            void* resPtr;
            SwapChain.Get().GetBuffer(i, riidResource, &resPtr);
            RenderTargets[i] = (ID3D12Resource*)resPtr;
            Device.Get().CreateRenderTargetView(RenderTargets[i], null, rtvHandle);
            rtvHandle.Ptr += RtvDescriptorSize;
        }

        HeapProperties depthHeapProps = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);

        ResourceDesc depthDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Texture2D, Alignment = 0, Width = width, Height = height,
            DepthOrArraySize = 1, MipLevels = 1, Format = Format.FormatD24UnormS8Uint,
            SampleDesc = new SampleDesc(1, 0), Layout = TextureLayout.LayoutUnknown, Flags = ResourceFlags.AllowDepthStencil
        };

        ClearValue depthClearValue = new ClearValue { Format = Format.FormatD24UnormS8Uint, DepthStencil = new DepthStencilValue { Depth = 1.0f, Stencil = 0 } };

        void* depthPtr;
        Device.Get().CreateCommittedResource(&depthHeapProps, HeapFlags.None, &depthDesc, ResourceStates.DepthWrite, &depthClearValue, riidResource, &depthPtr);
        DepthStencilBuffer = new ComPtr<ID3D12Resource>((ID3D12Resource*)depthPtr);

        DepthStencilViewDesc dsvDesc = new DepthStencilViewDesc { Format = Format.FormatD24UnormS8Uint, ViewDimension = DsvDimension.Texture2D };
        Device.Get().CreateDepthStencilView(DepthStencilBuffer.Handle, &dsvDesc, DsvHeap.Get().GetCPUDescriptorHandleForHeapStart());
    }

    public void MoveToNextFrame()
    {
        ulong currentFenceValue = FenceValues[FrameIndex];
        CommandQueue.Get().Signal(Fence.Handle, currentFenceValue);

        FrameIndex = SwapChain.Get().GetCurrentBackBufferIndex();

        if (Fence.Get().GetCompletedValue() < FenceValues[FrameIndex])
        {
            Fence.Get().SetEventOnCompletion(FenceValues[FrameIndex], FenceEvent);
            _ = WaitForSingleObject(FenceEvent, 0xFFFFFFFF);
        }
        FenceValues[FrameIndex] = currentFenceValue + 1;
    }

    public void WaitForGpu()
    {
        ulong fenceValue = FenceValues[FrameIndex];
        CommandQueue.Get().Signal(Fence.Handle, fenceValue);
        Fence.Get().SetEventOnCompletion(fenceValue, FenceEvent);
        _ = WaitForSingleObject(FenceEvent, 0xFFFFFFFF);
        FenceValues[FrameIndex]++;
    }

    public void Resize(Vector2D<int> size)
    {
        if (size.X == 0 || size.Y == 0) return;

        WaitForGpu();

        for (int i = 0; i < FrameCount; i++)
        {
            if (RenderTargets[i] != null)
            {
                RenderTargets[i]->Release();
                RenderTargets[i] = null;
            }
        }
        DepthStencilBuffer.Dispose();

        SwapChain.Get().ResizeBuffers(FrameCount, (uint)size.X, (uint)size.Y, Format.FormatR8G8B8A8Unorm, 0);
        FrameIndex = SwapChain.Get().GetCurrentBackBufferIndex();
        CreateRenderTargets((uint)size.X, (uint)size.Y);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        WaitForGpu();
        CloseHandle(FenceEvent);
        Fence.Dispose();

        for (int i = 0; i < FrameCount; i++) if (RenderTargets[i] != null) RenderTargets[i]->Release();
        DepthStencilBuffer.Dispose();
        CommandList.Dispose();

        for (int i = 0; i < FrameCount; i++) CommandAllocators[i].Dispose();

        RtvHeap.Dispose(); DsvHeap.Dispose();
        SwapChain.Dispose(); CommandQueue.Dispose();
        Device.Dispose(); Dxgi.Dispose(); D3D.Dispose();

        _isDisposed = true;
    }
}
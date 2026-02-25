using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Direct3D.Compilers;

using Minecraft.NET.Windowing;

namespace Minecraft.NET.Graphics.Rendering;

public unsafe class D3D11Context : IDisposable
{
    public ComPtr<ID3D11Device> Device;
    public ComPtr<ID3D11DeviceContext> Context;
    public ComPtr<IDXGISwapChain> SwapChain;
    public ComPtr<ID3D11RenderTargetView> RenderTargetView;
    public ComPtr<ID3D11DepthStencilView> DepthStencilView;
    public ComPtr<ID3D11Texture2D> DepthTexture;

    public readonly D3D11 D3D;
    public readonly DXGI Dxgi;
    public readonly D3DCompiler Compiler;

    private bool _isDisposed;

    public D3D11Context(IWindow window)
    {
        D3D = D3D11.GetApi(null);
        Dxgi = DXGI.GetApi(null);
        Compiler = D3DCompiler.GetApi();

        SwapChainDesc swapChainDesc = new SwapChainDesc
        {
            BufferCount = 1,
            BufferDesc = new ModeDesc
            {
                Width = (uint)window.Size.X,
                Height = (uint)window.Size.Y,
                Format = Format.FormatR8G8B8A8Unorm,
                RefreshRate = new Rational(0, 0)
            },
            BufferUsage = DXGI.UsageRenderTargetOutput,
            OutputWindow = window.Win32Handle,
            SampleDesc = new SampleDesc(1, 0),
            Windowed = true,
            SwapEffect = SwapEffect.Discard
        };

        D3DFeatureLevel featureLevel = D3DFeatureLevel.Level110;

        fixed (ComPtr<IDXGISwapChain>* pSwapChain = &SwapChain)
        fixed (ComPtr<ID3D11Device>* pDevice = &Device)
        fixed (ComPtr<ID3D11DeviceContext>* pContext = &Context)
        {
            D3D.CreateDeviceAndSwapChain(
                null,
                D3DDriverType.Hardware,
                0,
                (uint)CreateDeviceFlag.None,
                &featureLevel,
                1,
                D3D11.SdkVersion,
                &swapChainDesc,
                (IDXGISwapChain**)pSwapChain,
                (ID3D11Device**)pDevice,
                null,
                (ID3D11DeviceContext**)pContext
            );
        }

        CreateRenderTargets((uint)window.Size.X, (uint)window.Size.Y);
    }

    public void CreateRenderTargets(uint width, uint height)
    {
        ID3D11Texture2D* backBuffer = null;
        var uuid = SilkMarshal.GuidPtrOf<ID3D11Texture2D>();
        SwapChain.GetBuffer(0, uuid, (void**)&backBuffer);

        fixed (ComPtr<ID3D11RenderTargetView>* rtvPtr = &RenderTargetView)
            Device.CreateRenderTargetView((ID3D11Resource*)backBuffer, (RenderTargetViewDesc*)null, (ID3D11RenderTargetView**)rtvPtr);

        backBuffer->Release();

        Texture2DDesc depthDesc = new Texture2DDesc
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatD24UnormS8Uint,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.DepthStencil,
            CPUAccessFlags = 0,
            MiscFlags = 0
        };

        fixed (ComPtr<ID3D11Texture2D>* depthTexPtr = &DepthTexture)
            Device.CreateTexture2D(&depthDesc, (SubresourceData*)null, (ID3D11Texture2D**)depthTexPtr);

        DepthStencilViewDesc dsvDesc = new DepthStencilViewDesc
        {
            Format = Format.FormatD24UnormS8Uint,
            ViewDimension = DsvDimension.Texture2D,
        };

        fixed (ComPtr<ID3D11DepthStencilView>* dsvPtr = &DepthStencilView)
            Device.CreateDepthStencilView((ID3D11Resource*)DepthTexture.Handle, &dsvDesc, (ID3D11DepthStencilView**)dsvPtr);
    }

    public void Resize(Vector2D<int> size)
    {
        if (size.X == 0 || size.Y == 0) return;

        Context.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);

        RenderTargetView.Dispose();
        DepthStencilView.Dispose();
        DepthTexture.Dispose();

        SwapChain.ResizeBuffers(0, (uint)size.X, (uint)size.Y, Format.FormatUnknown, 0);
        CreateRenderTargets((uint)size.X, (uint)size.Y);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        RenderTargetView.Dispose();
        DepthStencilView.Dispose();
        DepthTexture.Dispose();
        SwapChain.Dispose();
        Context.Dispose();
        Device.Dispose();
        D3D.Dispose();
        Dxgi.Dispose();
        Compiler.Dispose();
    }
}
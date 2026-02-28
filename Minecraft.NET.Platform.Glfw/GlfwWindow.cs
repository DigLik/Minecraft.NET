using System.Diagnostics;
using System.Runtime.InteropServices;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Utils.Math;

using Silk.NET.GLFW;

using GlfwApi = Silk.NET.GLFW.Glfw;

namespace Minecraft.NET.Platform.Glfw;

public unsafe partial class GlfwWindow : IWindow
{
    private readonly GlfwApi _glfw;
    private readonly WindowHandle* _windowHandle;
    private readonly GlfwCallbacks.FramebufferSizeCallback _framebufferSizeCallback;
    private readonly GlfwCallbacks.WindowCloseCallback _windowCloseCallback;

    private string _title;
    private readonly double _targetUps;
    private readonly double _targetFps;

    public Vector2<int> Size
    {
        get
        {
            _glfw.GetWindowSize(_windowHandle, out int w, out int h);
            return new Vector2<int>(w, h);
        }
    }

    public Vector2<int> FramebufferSize
    {
        get
        {
            _glfw.GetFramebufferSize(_windowHandle, out int w, out int h);
            return new Vector2<int>(w, h);
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            _glfw.SetWindowTitle(_windowHandle, value);
        }
    }

    public bool IsClosing => _glfw.WindowShouldClose(_windowHandle);
    public void* Handle => _windowHandle;
    public nint Win32Handle => GetWin32Window(_windowHandle);

    public event Action? Load;
    public event Action<double>? Update;
    public event Action<double>? Render;
    public event Action<Vector2<int>>? FramebufferResize;
    public event Action? Closing;

    [LibraryImport("glfw3", EntryPoint = "glfwGetWin32Window")]
    private static partial nint GetWin32Window(WindowHandle* window);

    public GlfwWindow(string title, int width, int height, double targetUps = 100.0, double targetFps = 0.0)
    {
        _title = title;
        _targetUps = targetUps;
        _targetFps = targetFps;

        _glfw = GlfwApi.GetApi();

        if (!_glfw.Init())
            throw new Exception("Failed to initialize GLFW.");

        _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);

        _windowHandle = _glfw.CreateWindow(width, height, _title, null, null);
        if ((nint)_windowHandle == 0)
            throw new Exception("Failed to create GLFW window.");

        _framebufferSizeCallback = OnFramebufferResize;
        _windowCloseCallback = OnWindowClose;
        _glfw.SetFramebufferSizeCallback(_windowHandle, _framebufferSizeCallback);
        _glfw.SetWindowCloseCallback(_windowHandle, _windowCloseCallback);
    }

    public void Run()
    {
        Load?.Invoke();
        double targetUpdateTime = _targetUps > 0 ? 1.0 / _targetUps : 1.0 / 100.0;
        double targetRenderTime = _targetFps > 0 ? 1.0 / _targetFps : 0.0;

        var sw = Stopwatch.StartNew();
        double previousTime = sw.Elapsed.TotalSeconds;
        double updateAccumulator = 0.0;
        double lastRenderTime = previousTime;

        while (!_glfw.WindowShouldClose(_windowHandle))
        {
            _glfw.PollEvents();

            double currentTime = sw.Elapsed.TotalSeconds;
            double frameTime = currentTime - previousTime;
            if (frameTime > 0.25) frameTime = 0.25; 
            
            previousTime = currentTime;
            updateAccumulator += frameTime;

            while (updateAccumulator >= targetUpdateTime)
            {
                Update?.Invoke(targetUpdateTime);
                updateAccumulator -= targetUpdateTime;
            }

            if (targetRenderTime == 0.0 || (currentTime - lastRenderTime) >= targetRenderTime)
            {
                Render?.Invoke(currentTime - lastRenderTime);
                lastRenderTime = sw.Elapsed.TotalSeconds;
            }

            currentTime = sw.Elapsed.TotalSeconds;
            
            double timeToNextUpdate = targetUpdateTime - updateAccumulator;
            
            double timeToNextRender = targetRenderTime > 0.0 
                ? lastRenderTime + targetRenderTime - currentTime 
                : 0.0;

            double waitTime = targetRenderTime > 0.0 
                ? Math.Min(timeToNextUpdate, timeToNextRender) 
                : timeToNextUpdate;

            if (waitTime > 0)
            {
                if (waitTime > 0.0015) 
                    Thread.Sleep(1); 
                else
                    Thread.SpinWait(10); 
            }
        }
        Closing?.Invoke();
    }

    public void Close() => _glfw.SetWindowShouldClose(_windowHandle, true);

    private void OnFramebufferResize(WindowHandle* window, int width, int height)
        => FramebufferResize?.Invoke(new Vector2<int>(width, height));

    private void OnWindowClose(WindowHandle* window) => Close();

    public void Dispose()
    {
        _glfw.DestroyWindow(_windowHandle);
        _glfw.Terminate();
        _glfw.Dispose();
        GC.SuppressFinalize(this);
    }
}
using System.Diagnostics;
using System.Runtime.InteropServices;

using Silk.NET.GLFW;
using Silk.NET.Maths;

namespace Minecraft.NET.Windowing;

public unsafe partial class GlfwWindow : IWindow
{
    private readonly Glfw _glfw;
    private readonly WindowHandle* _windowHandle;
    private readonly WindowOptions _options;
    private readonly GlfwCallbacks.FramebufferSizeCallback _framebufferSizeCallback;
    private readonly GlfwCallbacks.WindowCloseCallback _windowCloseCallback;

    private string _title;

    public Vector2D<int> Size
    {
        get
        {
            _glfw.GetWindowSize(_windowHandle, out int w, out int h);
            return new Vector2D<int>(w, h);
        }
    }

    public Vector2D<int> FramebufferSize
    {
        get
        {
            _glfw.GetFramebufferSize(_windowHandle, out int w, out int h);
            return new Vector2D<int>(w, h);
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
    public event Action<Vector2D<int>>? FramebufferResize;
    public event Action? Closing;

    [LibraryImport("glfw3", EntryPoint = "glfwGetWin32Window")]
    private static partial nint GetWin32Window(WindowHandle* window);

    public GlfwWindow(WindowOptions options)
    {
        _options = options;
        _title = options.Title;

        _glfw = Glfw.GetApi();

        if (!_glfw.Init())
            throw new Exception("Failed to initialize GLFW.");

        _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);

        _windowHandle = _glfw.CreateWindow(options.Size.X, options.Size.Y, _title, null, null);

        if (_windowHandle == null)
            throw new Exception("Failed to create GLFW window.");

        _framebufferSizeCallback = OnFramebufferResize;
        _windowCloseCallback = OnWindowClose;
        _glfw.SetFramebufferSizeCallback(_windowHandle, _framebufferSizeCallback);
        _glfw.SetWindowCloseCallback(_windowHandle, _windowCloseCallback);
    }

    public void Run()
    {
        Load?.Invoke();

        double targetUpdateTime = _options.TargetUPS > 0 ? 1.0 / _options.TargetUPS : 1.0 / 100.0;
        double targetRenderTime = _options.TargetFPS > 0 ? 1.0 / _options.TargetFPS : 0.0;

        var sw = Stopwatch.StartNew();
        double previousTime = sw.Elapsed.TotalSeconds;
        double updateAccumulator = 0.0;
        double lastRenderTime = previousTime;

        while (!_glfw.WindowShouldClose(_windowHandle))
        {
            _glfw.PollEvents();

            double currentTime = sw.Elapsed.TotalSeconds;
            double frameTime = currentTime - previousTime;
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
                lastRenderTime = currentTime;
            }
            else
            {
                PreciseSleep(targetRenderTime - (currentTime - lastRenderTime));
            }
        }

        Closing?.Invoke();
    }

    public void Close() => _glfw.SetWindowShouldClose(_windowHandle, true);

    private static void PreciseSleep(double seconds)
    {
        if (seconds <= 0) return;

        double ms = seconds * 1000.0;
        if (ms > 2.0)
            Thread.Sleep((int)(ms - 2.0));

        long endTick = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < endTick)
            Thread.SpinWait(10);
    }

    private void OnFramebufferResize(WindowHandle* window, int width, int height)
        => FramebufferResize?.Invoke(new Vector2D<int>(width, height));

    private void OnWindowClose(WindowHandle* window)
        => Close();

    public void Dispose()
    {
        _glfw.DestroyWindow(_windowHandle);
        _glfw.Terminate();
        _glfw.Dispose();
        GC.SuppressFinalize(this);
    }
}
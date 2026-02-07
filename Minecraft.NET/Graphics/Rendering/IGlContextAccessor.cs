namespace Minecraft.NET.Graphics.Rendering;

public interface IGlContextAccessor
{
    GL Gl { get; }
    bool IsReady { get; }
    void SetGl(GL gl);
}

public class GlContextAccessor : IGlContextAccessor
{
    private GL? _gl;
    public GL Gl => _gl ?? throw new InvalidOperationException("Графический контекст ещё не инициализирован!");
    public bool IsReady => _gl != null;

    public void SetGl(GL gl) => _gl = gl;
}
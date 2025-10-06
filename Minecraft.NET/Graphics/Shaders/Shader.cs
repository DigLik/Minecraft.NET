using Silk.NET.OpenGL;
using System.Diagnostics;
using System.Numerics;

namespace Minecraft.NET.Graphics.Shaders;

public abstract class Shader : IDisposable
{
    protected readonly GL _gl;
    public uint Handle { get; }

    protected Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        var vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

        Handle = _gl.CreateProgram();
        _gl.AttachShader(Handle, vertexShader);
        _gl.AttachShader(Handle, fragmentShader);
        _gl.LinkProgram(Handle);

        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
            throw new Exception($"Shader program linking failed: {_gl.GetProgramInfoLog(Handle)}");

        _gl.DetachShader(Handle, vertexShader);
        _gl.DetachShader(Handle, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    protected Shader(GL gl, string computeSource)
    {
        _gl = gl;

        var computeShader = CompileShader(ShaderType.ComputeShader, computeSource);

        Handle = _gl.CreateProgram();

        _gl.AttachShader(Handle, computeShader);
        _gl.LinkProgram(Handle);

        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
            throw new Exception($"Shader program linking failed: {_gl.GetProgramInfoLog(Handle)}");

        _gl.DetachShader(Handle, computeShader);
        _gl.DeleteShader(computeShader);
    }

    public void Use() => _gl.UseProgram(Handle);

    protected int GetUniformLocation(string name)
    {
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
            Debug.WriteLine($"[WARNING] Uniform '{name}' not found in shader {GetType().Name}.");

        return location;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
            throw new Exception($"Shader compilation of type {type} failed: {_gl.GetShaderInfoLog(shader)}");

        return shader;
    }

    public void Dispose()
    {
        _gl.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }
}
using System.Diagnostics;

namespace Minecraft.NET.Graphics.Rendering;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    public readonly uint Handle;

    public Shader(GL gl, string vertexSource, string fragmentSource)
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

    public static string LoadFromFile(string path) => new([.. File.ReadAllText(path).Where(c => c < 128)]);

    public void Use() => _gl.UseProgram(Handle);

    public int GetUniformLocation(string name)
    {
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
            Debug.WriteLine($"[WARNING] Uniform '{name}' not found.");
        return location;
    }

    public void SetInt(int location, int value) => _gl.Uniform1(location, value);
    public void SetFloat(int location, float value) => _gl.Uniform1(location, value);
    public void SetBool(int location, bool value) => _gl.Uniform1(location, value ? 1 : 0);
    public unsafe void SetVector2(int location, Vector2 vector) => _gl.Uniform2(location, 1, (float*)&vector);
    public unsafe void SetVector3(int location, Vector3 vector) => _gl.Uniform3(location, 1, (float*)&vector);
    public unsafe void SetVector4(int location, Vector4 vector) => _gl.Uniform4(location, 1, (float*)&vector);
    public unsafe void SetMatrix4x4(int location, Matrix4x4 matrix) => _gl.UniformMatrix4(location, 1, false, (float*)&matrix);

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

    public void Dispose() => _gl.DeleteProgram(Handle);
}
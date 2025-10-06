using Silk.NET.OpenGL;
using System.Numerics;

namespace Minecraft.NET.Graphics.Shaders;

public sealed class BasicShader : Shader
{
    private const string VertexSource = """
        #version 460 core
        layout (location = 0) in vec3 aPosition;
        
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        
        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
        }
        """;

    private const string FragmentSource = """
        #version 460 core
        out vec4 FragColor;
        
        void main()
        {
            FragColor = vec4(1.0, 0.5, 0.2, 1.0); // Orange
        }
        """;

    private readonly int _locationModel;
    private readonly int _locationView;
    private readonly int _locationProjection;

    public BasicShader(GL gl) : base(gl, VertexSource, FragmentSource)
    {
        _locationModel = GetUniformLocation("model");
        _locationView = GetUniformLocation("view");
        _locationProjection = GetUniformLocation("projection");
    }

    public unsafe void SetModelMatrix(Matrix4x4 matrix)
        => _gl.UniformMatrix4(_locationModel, 1, false, (float*)&matrix);

    public unsafe void SetViewMatrix(Matrix4x4 matrix)
        => _gl.UniformMatrix4(_locationView, 1, false, (float*)&matrix);

    public unsafe void SetProjectionMatrix(Matrix4x4 matrix)
        => _gl.UniformMatrix4(_locationProjection, 1, false, (float*)&matrix);
}
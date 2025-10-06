using Silk.NET.OpenGL;
using System.Numerics;

namespace Minecraft.NET.Graphics.Shaders;

public sealed class BasicShader : Shader
{
    private const string VertexSource = """
        #version 460 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec2 aTexCoord;

        out vec2 TexCoord;

        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;

        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
            TexCoord = aTexCoord;
        }
        """;

    private const string FragmentSource = """
        #version 460 core
        out vec4 FragColor;

        in vec2 TexCoord;
        uniform sampler2D uTexture;

        void main()
        {
            FragColor = texture(uTexture, TexCoord);
        }
        """;

    private readonly int _locationModel;
    private readonly int _locationView;
    private readonly int _locationProjection;
    private readonly int _locationTexture;

    public BasicShader(GL gl) : base(gl, VertexSource, FragmentSource)
    {
        _locationModel = GetUniformLocation("model");
        _locationView = GetUniformLocation("view");
        _locationProjection = GetUniformLocation("projection");
        _locationTexture = GetUniformLocation("uTexture");
    }

    public unsafe void SetModelMatrix(Matrix4x4 matrix)
        => _gl.UniformMatrix4(_locationModel, 1, false, (float*)&matrix);

    public unsafe void SetViewMatrix(Matrix4x4 matrix)
        => _gl.UniformMatrix4(_locationView, 1, false, (float*)&matrix);

    public unsafe void SetProjectionMatrix(Matrix4x4 matrix)
        => _gl.UniformMatrix4(_locationProjection, 1, false, (float*)&matrix);

    public void SetTextureUnit(int unit)
        => _gl.Uniform1(_locationTexture, unit);
}
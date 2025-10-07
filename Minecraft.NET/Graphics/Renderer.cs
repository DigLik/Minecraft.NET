using Minecraft.NET.Core;
using Silk.NET.Maths;

namespace Minecraft.NET.Graphics;

public sealed class Renderer
{
    private GL _gl = null!;
    private Shader _chunkShader = null!;
    public Texture BlockTextureAtlas { get; private set; } = null!;
    private int _locModel, _locView, _locProj;

    public void Load(GL gl)
    {
        _gl = gl;

        BlockTextureAtlas = new Texture(_gl, "Assets/Textures/atlas.png");

        const string vertSource = """
            #version 460 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec2 aTexCoord;
            layout (location = 2) in float aOcclusion;

            out vec2 TexCoord;
            out float Occlusion;

            uniform mat4 model;
            uniform mat4 view;
            uniform mat4 projection;
            void main() {
                gl_Position = projection * view * model * vec4(aPosition, 1.0);
                TexCoord = aTexCoord;
                Occlusion = aOcclusion;
            }
            """;
        const string fragSource = """
            #version 460 core
            out vec4 FragColor;

            in vec2 TexCoord;
            in float Occlusion;

            uniform sampler2D uTexture;
            void main() {
                FragColor = texture(uTexture, TexCoord);
                if (FragColor.a < 0.1) discard;

                FragColor.rgb *= Occlusion;
            }
            """;

        _chunkShader = new Shader(_gl, vertSource, fragSource);
        _locModel = _chunkShader.GetUniformLocation("model");
        _locView = _chunkShader.GetUniformLocation("view");
        _locProj = _chunkShader.GetUniformLocation("projection");

        _gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
    }

    public void Render(IReadOnlyList<ChunkSection> chunks, Camera camera, Vector2D<int> viewportSize)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _chunkShader.Use();
        BlockTextureAtlas.Bind();

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix((float)viewportSize.X / viewportSize.Y);

        _chunkShader.SetMatrix4x4(_locView, view);
        _chunkShader.SetMatrix4x4(_locProj, projection);

        lock (chunks)
        {
            foreach (var chunk in chunks)
            {
                var mesh = chunk.Mesh;
                if (mesh == null || mesh.IndexCount == 0 || chunk.State != ChunkState.Rendered) continue;

                mesh.Bind();

                var model = Matrix4x4.CreateTranslation(chunk.Position * ChunkSize);
                _chunkShader.SetMatrix4x4(_locModel, model);

                unsafe
                {
                    _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null);
                }
            }
        }
    }
}
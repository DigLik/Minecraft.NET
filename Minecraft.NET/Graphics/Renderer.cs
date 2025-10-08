using Minecraft.NET.Core;
using Silk.NET.Maths;

namespace Minecraft.NET.Graphics;

public sealed class Renderer
{
    private GL _gl = null!;
    private Shader _chunkShader = null!;
    public Texture BlockTextureAtlas { get; private set; } = null!;
    private int _locMvp, _locModel;

    private int _locUseWireframeColor;
    private int _locWireframeColor;
    private readonly bool _showWireframe = true;

    private readonly Frustum _frustum = new();

    private static string LoadShaderSource(string path)
        => new([.. File.ReadAllText(path).Where(c => c < 128)]);

    public void Load(GL gl)
    {
        _gl = gl;

        BlockTextureAtlas = new Texture(_gl, "Assets/Textures/atlas.png");

        const float PixelPadding = 0.1f;
        const float TileSizeVal = Constants.TileSize;
        const float AtlasWidthVal = Constants.AtlasWidth;
        const float AtlasHeightVal = Constants.AtlasHeight;

        string vertSource = LoadShaderSource("Assets/Shaders/chunk.vert");
        string fragSource = LoadShaderSource("Assets/Shaders/chunk.frag");

        _chunkShader = new Shader(_gl, vertSource, fragSource);

        _locMvp = _chunkShader.GetUniformLocation("mvp");
        _locModel = _chunkShader.GetUniformLocation("model");
        _locUseWireframeColor = _chunkShader.GetUniformLocation("u_UseWireframeColor");
        _locWireframeColor = _chunkShader.GetUniformLocation("u_WireframeColor");

        var locTileAtlasSize = _chunkShader.GetUniformLocation("uTileAtlasSize");
        var locTexture = _chunkShader.GetUniformLocation("uTexture");
        var locTileSize = _chunkShader.GetUniformLocation("uTileSize");
        var locPixelPadding = _chunkShader.GetUniformLocation("uPixelPadding");

        _chunkShader.Use();
        _chunkShader.SetVector2(locTileAtlasSize, new Vector2(AtlasWidthVal, AtlasHeightVal));
        _chunkShader.SetInt(locTexture, 0);
        _chunkShader.SetFloat(locTileSize, TileSizeVal);
        _chunkShader.SetFloat(locPixelPadding, PixelPadding);

        _gl.UseProgram(0);

        _gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
    }

    public void Render(IReadOnlyCollection<ChunkSection> chunks, Camera camera, Vector2D<int> viewportSize)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _chunkShader.Use();
        BlockTextureAtlas.Bind();

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix((float)viewportSize.X / viewportSize.Y);
        var viewProjection = view * projection;

        _frustum.Update(viewProjection);

        _chunkShader.SetBool(_locUseWireframeColor, false);

        lock (chunks)
        {
            foreach (var chunk in chunks)
            {
                var mesh = chunk.Mesh;
                if (mesh == null || mesh.IndexCount == 0 || chunk.State != ChunkState.Rendered) continue;

                var chunkWorldPos = chunk.Position * ChunkSize;
                var box = new BoundingBox(chunkWorldPos, chunkWorldPos + new Vector3(ChunkSize));
                if (!_frustum.Intersects(box))
                    continue;

                mesh.Bind();

                var model = Matrix4x4.CreateTranslation(chunkWorldPos);
                var mvp = model * viewProjection;
                _chunkShader.SetMatrix4x4(_locMvp, mvp);
                _chunkShader.SetMatrix4x4(_locModel, model);

                unsafe
                {
                    _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null);
                }
            }
        }

        if (_showWireframe)
        {
            _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            _gl.Enable(EnableCap.PolygonOffsetLine);
            _gl.PolygonOffset(-1.0f, -1.0f);

            _chunkShader.SetBool(_locUseWireframeColor, true);
            _chunkShader.SetVector4(_locWireframeColor, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            lock (chunks)
            {
                foreach (var chunk in chunks)
                {
                    var mesh = chunk.Mesh;
                    if (mesh == null || mesh.IndexCount == 0 || chunk.State != ChunkState.Rendered) continue;

                    var chunkWorldPos = chunk.Position * ChunkSize;
                    var box = new BoundingBox(chunkWorldPos, chunkWorldPos + new Vector3(ChunkSize));
                    if (!_frustum.Intersects(box))
                        continue;

                    mesh.Bind();

                    var model = Matrix4x4.CreateTranslation(chunkWorldPos);
                    var mvp = model * viewProjection;
                    _chunkShader.SetMatrix4x4(_locMvp, mvp);
                    _chunkShader.SetMatrix4x4(_locModel, model);

                    unsafe
                    {
                        _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null);
                    }
                }
            }

            _gl.Disable(EnableCap.PolygonOffsetLine);
            _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        }
    }
}
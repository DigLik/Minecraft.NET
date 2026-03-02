using System.Text;

using Silk.NET.Shaderc;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class ShaderCompiler : IDisposable
{
    private readonly Shaderc _shaderc;
    private readonly Compiler* _compiler;
    private readonly CompileOptions* _options;

    public ShaderCompiler()
    {
        _shaderc = Shaderc.GetApi();
        _compiler = _shaderc.CompilerInitialize();
        _options = _shaderc.CompileOptionsInitialize();

        _shaderc.CompileOptionsSetSourceLanguage(_options, SourceLanguage.Glsl);

        uint vulkan12 = (1 << 22) | (2 << 12);
        _shaderc.CompileOptionsSetTargetEnv(_options, TargetEnv.Vulkan, vulkan12);
        _shaderc.CompileOptionsSetTargetSpirv(_options, (SpirvVersion)0x010500);
    }

    public byte[] Compile(string source, string fileName, ShaderKind kind, string entryPoint = "main")
    {
        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);

        CompilationResult* result;
        fixed (byte* pSource = sourceBytes)
        {
            result = _shaderc.CompileIntoSpv(
                _compiler,
                pSource,
                (nuint)sourceBytes.Length,
                kind,
                fileName,
                entryPoint,
                _options);
        }

        if (_shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
        {
            string error = _shaderc.ResultGetErrorMessageS(result);
            _shaderc.ResultRelease(result);
            throw new Exception($"Shader compilation failed for {fileName}:\n{error}");
        }

        nuint length = _shaderc.ResultGetLength(result);
        byte* bytes = _shaderc.ResultGetBytes(result);

        byte[] spv = new byte[length];
        fixed (byte* pSpv = spv)
        {
            Buffer.MemoryCopy(bytes, pSpv, length, length);
        }

        _shaderc.ResultRelease(result);
        return spv;
    }

    public void Dispose()
    {
        _shaderc.CompileOptionsRelease(_options);
        _shaderc.CompilerRelease(_compiler);
        _shaderc.Dispose();
    }
}
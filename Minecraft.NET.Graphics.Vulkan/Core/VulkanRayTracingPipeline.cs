using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public struct SbtProperties
{
    public uint HandleSize;
    public uint RegionAligned;
}

public unsafe class VulkanRayTracingPipeline : IDisposable
{
    private readonly VulkanDevice _device;

    public PipelineLayout PipelineLayout;
    public DescriptorSetLayout DescriptorSetLayout;
    public Pipeline Pipeline;

    public VulkanBuffer SbtBuffer = null!;
    public SbtProperties SbtProps;

    private const string RayGenShaderCode = @"#version 460
#extension GL_EXT_ray_tracing : require

layout(binding = 0, set = 0) uniform accelerationStructureEXT Scene;
layout(binding = 1, set = 0, rgba8) uniform image2D RenderTarget;

layout(binding = 2, set = 0) uniform Camera {
    mat4 ViewProj;
    mat4 InverseViewProj;
    vec4 CameraPos;
    vec4 SunDirection;
} cam;

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
    float reflectivity;
};

layout(location = 0) rayPayloadEXT Payload payload;

void main() {
    ivec2 launchIndex = ivec2(gl_LaunchIDEXT.xy);
    ivec2 launchDim = ivec2(gl_LaunchSizeEXT.xy);

    vec2 crd = vec2(launchIndex) / vec2(launchDim);
    crd = crd * 2.0 - 1.0;

    vec4 target = cam.InverseViewProj * vec4(crd.x, crd.y, 1.0, 1.0);
    vec3 rayDir = normalize(target.xyz / target.w - cam.CameraPos.xyz);

    uint rayFlags = gl_RayFlagsCullBackFacingTrianglesEXT;
    uint cullMask = 0xFF;
    float tmin = 0.001;
    float tmax = 10000.0;

    vec4 finalColor = vec4(0.0);
    vec3 currentOrigin = cam.CameraPos.xyz;
    vec3 currentDir = rayDir;
    float currentReflectivity = 0.0;

    for (int i = 0; i < 2; i++) {
        payload.reflectivity = 0.0;
        traceRayEXT(Scene, rayFlags, cullMask, 0, 0, 0, currentOrigin, tmin, currentDir, tmax, 0);

        if (i == 0) {
            finalColor = payload.color;
            currentReflectivity = payload.reflectivity;
        } else {
            finalColor.rgb = mix(finalColor.rgb, payload.color.rgb, currentReflectivity);
        }

        if (payload.reflectivity > 0.0) {
            currentOrigin = payload.hitPos + payload.normal * 0.01;
            currentDir = reflect(currentDir, payload.normal);
            currentReflectivity = payload.reflectivity;
        } else {
            break;
        }
    }

    imageStore(RenderTarget, launchIndex, finalColor);
}";

    private const string MissShaderCode = @"#version 460
#extension GL_EXT_ray_tracing : require

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
    float reflectivity;
};

layout(location = 0) rayPayloadInEXT Payload payload;

void main() {
    payload.color = vec4(0.4, 0.6, 0.9, 1.0);
    payload.reflectivity = 0.0;
}";

    private const string ClosestHitShaderCode = @"#version 460
#extension GL_EXT_ray_tracing : require
#extension GL_EXT_scalar_block_layout : require
#extension GL_EXT_ray_query : require

struct Payload {
    vec3 hitPos;
    vec3 normal;
    vec4 color;
    float reflectivity;
};

layout(location = 0) rayPayloadInEXT Payload payload;
hitAttributeEXT vec2 attribs;

struct ChunkVertex {
    vec4 Position;
    int TextureIndex;
    vec2 UV;
    int OverlayTextureIndex;
    vec4 Color;
    vec4 OverlayColor;
};

struct InstanceData {
    uint VertexOffset;
    uint IndexOffset;
    uint Pad1;
    uint Pad2;
};

layout(binding = 0, set = 0) uniform accelerationStructureEXT Scene;

layout(binding = 2, set = 0) uniform Camera {
    mat4 ViewProj;
    mat4 InverseViewProj;
    vec4 CameraPos;
    vec4 SunDirection;
} cam;

layout(binding = 3, set = 0) uniform sampler2DArray TexArray;

layout(binding = 4, set = 0, scalar) readonly buffer Vertices { ChunkVertex v[]; } vertices;
layout(binding = 5, set = 0, scalar) readonly buffer Indices { uint i[]; } indices;
layout(binding = 6, set = 0, scalar) readonly buffer Instances { InstanceData d[]; } instances;

void main() {
    uint instId = gl_InstanceID;
    uint primId = gl_PrimitiveID;

    InstanceData inst = instances.d[instId];
    
    uint i0 = indices.i[inst.IndexOffset + primId * 3 + 0];
    uint i1 = indices.i[inst.IndexOffset + primId * 3 + 1];
    uint i2 = indices.i[inst.IndexOffset + primId * 3 + 2];

    ChunkVertex v0 = vertices.v[inst.VertexOffset + i0];
    ChunkVertex v1 = vertices.v[inst.VertexOffset + i1];
    ChunkVertex v2 = vertices.v[inst.VertexOffset + i2];

    vec3 barycentrics = vec3(1.0 - attribs.x - attribs.y, attribs.x, attribs.y);

    vec2 uv = v0.UV * barycentrics.x + v1.UV * barycentrics.y + v2.UV * barycentrics.z;
    vec4 color = v0.Color * barycentrics.x + v1.Color * barycentrics.y + v2.Color * barycentrics.z;
    
    int texIndex = v0.TextureIndex;
    int overlayTexIndex = v0.OverlayTextureIndex;
    vec4 overlayColor = v0.OverlayColor * barycentrics.x + v1.OverlayColor * barycentrics.y + v2.OverlayColor * barycentrics.z;

    vec4 texColor = texture(TexArray, vec3(uv, float(texIndex)));
    
    if (overlayTexIndex >= 0) {
        vec4 overlayTex = texture(TexArray, vec3(uv, float(overlayTexIndex)));
        if (overlayTex.a > 0.5) {
            texColor = overlayTex * overlayColor;
        } else {
            texColor *= color;
        }
    } else {
        texColor *= color;
    }

    vec3 worldPos = gl_WorldRayOriginEXT + gl_WorldRayDirectionEXT * gl_HitTEXT;
    
    vec3 e1 = v1.Position.xyz - v0.Position.xyz;
    vec3 e2 = v2.Position.xyz - v0.Position.xyz;
    vec3 normal = normalize(cross(e1, e2));
    if (dot(normal, gl_WorldRayDirectionEXT) > 0.0) {
        normal = -normal;
    }
    
    vec3 shadowOrigin = worldPos + normal * 0.01;

    rayQueryEXT rq;
    rayQueryInitializeEXT(rq, Scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsOpaqueEXT | gl_RayFlagsSkipClosestHitShaderEXT, 0xFF, shadowOrigin, 0.001, cam.SunDirection.xyz, 1000.0);
    rayQueryProceedEXT(rq);
    if (rayQueryGetIntersectionTypeEXT(rq, true) != gl_RayQueryCommittedIntersectionNoneEXT) {
        texColor.rgb *= 0.4;
    }

    payload.hitPos = worldPos;
    payload.normal = normal;
    payload.color = texColor;

    if (texIndex == 0) {
        payload.reflectivity = 1.0;
    } else {
        payload.reflectivity = 0.0;
    }
}";

    public VulkanRayTracingPipeline(VulkanDevice device)
    {
        _device = device;
        CreateDescriptorSetLayout();
        CreatePipeline();
        CreateSBT();
    }

    private void CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding[] bindings = [
            new() { Binding = 0, DescriptorType = DescriptorType.AccelerationStructureKhr, DescriptorCount = 1, StageFlags = ShaderStageFlags.RaygenBitKhr | ShaderStageFlags.ClosestHitBitKhr },
            new() { Binding = 1, DescriptorType = DescriptorType.StorageImage, DescriptorCount = 1, StageFlags = ShaderStageFlags.RaygenBitKhr },
            new() { Binding = 2, DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.RaygenBitKhr | ShaderStageFlags.ClosestHitBitKhr },
            new() { Binding = 3, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr },
            new() { Binding = 4, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr },
            new() { Binding = 5, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr },
            new() { Binding = 6, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr }
        ];

        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 7, PBindings = pBindings
            };
            _device.Vk.CreateDescriptorSetLayout(_device.Device, in layoutInfo, null, out DescriptorSetLayout);
        }
    }

    private void CreatePipeline()
    {
        using var compiler = new ShaderCompiler();

        var rgenSpv = compiler.Compile(RayGenShaderCode, "raygen.glsl", Silk.NET.Shaderc.ShaderKind.RaygenShader, "main");
        var rmissSpv = compiler.Compile(MissShaderCode, "miss.glsl", Silk.NET.Shaderc.ShaderKind.MissShader, "main");
        var rchitSpv = compiler.Compile(ClosestHitShaderCode, "chit.glsl", Silk.NET.Shaderc.ShaderKind.ClosesthitShader, "main");

        ShaderModule rgenModule = CreateShaderModule(rgenSpv);
        ShaderModule rmissModule = CreateShaderModule(rmissSpv);
        ShaderModule rchitModule = CreateShaderModule(rchitSpv);

        var stages = stackalloc PipelineShaderStageCreateInfo[3];
        stages[0] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.RaygenBitKhr, Module = rgenModule, PName = (byte*)SilkMarshal.StringToPtr("main") };
        stages[1] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.MissBitKhr, Module = rmissModule, PName = (byte*)SilkMarshal.StringToPtr("main") };
        stages[2] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ClosestHitBitKhr, Module = rchitModule, PName = (byte*)SilkMarshal.StringToPtr("main") };

        var groups = stackalloc RayTracingShaderGroupCreateInfoKHR[3];
        groups[0] = new() { SType = StructureType.RayTracingShaderGroupCreateInfoKhr, Type = RayTracingShaderGroupTypeKHR.GeneralKhr, GeneralShader = 0, ClosestHitShader = Vk.ShaderUnusedKhr, AnyHitShader = Vk.ShaderUnusedKhr, IntersectionShader = Vk.ShaderUnusedKhr };
        groups[1] = new() { SType = StructureType.RayTracingShaderGroupCreateInfoKhr, Type = RayTracingShaderGroupTypeKHR.GeneralKhr, GeneralShader = 1, ClosestHitShader = Vk.ShaderUnusedKhr, AnyHitShader = Vk.ShaderUnusedKhr, IntersectionShader = Vk.ShaderUnusedKhr };
        groups[2] = new() { SType = StructureType.RayTracingShaderGroupCreateInfoKhr, Type = RayTracingShaderGroupTypeKHR.TrianglesHitGroupKhr, GeneralShader = Vk.ShaderUnusedKhr, ClosestHitShader = 2, AnyHitShader = Vk.ShaderUnusedKhr, IntersectionShader = Vk.ShaderUnusedKhr };

        DescriptorSetLayout layout = DescriptorSetLayout;
        PipelineLayoutCreateInfo pipelineLayoutInfo = new() { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &layout };
        _device.Vk.CreatePipelineLayout(_device.Device, in pipelineLayoutInfo, null, out PipelineLayout);

        RayTracingPipelineCreateInfoKHR pipelineInfo = new()
        {
            SType = StructureType.RayTracingPipelineCreateInfoKhr, StageCount = 3, PStages = stages,
            GroupCount = 3, PGroups = groups, MaxPipelineRayRecursionDepth = 1, Layout = PipelineLayout
        };

        _device.KhrRayTracingPipeline.CreateRayTracingPipelines(_device.Device, default, default, 1, in pipelineInfo, null, out Pipeline);

        _device.Vk.DestroyShaderModule(_device.Device, rgenModule, null);
        _device.Vk.DestroyShaderModule(_device.Device, rmissModule, null);
        _device.Vk.DestroyShaderModule(_device.Device, rchitModule, null);
        SilkMarshal.Free((nint)stages[0].PName);
        SilkMarshal.Free((nint)stages[1].PName);
        SilkMarshal.Free((nint)stages[2].PName);
    }

    private static uint AlignUp(uint size, uint alignment) => (size + alignment - 1) & ~(alignment - 1);

    private void CreateSBT()
    {
        var props = new PhysicalDeviceRayTracingPipelinePropertiesKHR { SType = StructureType.PhysicalDeviceRayTracingPipelinePropertiesKhr };
        var props2 = new PhysicalDeviceProperties2 { SType = StructureType.PhysicalDeviceProperties2, PNext = &props };
        _device.Vk.GetPhysicalDeviceProperties2(_device.PhysicalDevice, &props2);

        SbtProps = new SbtProperties
        {
            HandleSize = props.ShaderGroupHandleSize,
            RegionAligned = AlignUp(props.ShaderGroupHandleSize, props.ShaderGroupBaseAlignment)
        };

        uint sbtSize = SbtProps.RegionAligned * 3;
        SbtBuffer = new VulkanBuffer(_device, sbtSize, BufferUsageFlags.ShaderBindingTableBitKhr | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        byte[] handles = new byte[props.ShaderGroupHandleSize * 3];
        fixed (byte* pHandles = handles)
        {
            _device.KhrRayTracingPipeline.GetRayTracingShaderGroupHandles(_device.Device, Pipeline, 0, 3, (nuint)handles.Length, pHandles);
        }

        byte* mapped = (byte*)SbtBuffer.MappedMemory;
        for (int i = 0; i < 3; i++)
        {
            fixed (byte* ptr = &handles[i * props.ShaderGroupHandleSize])
                System.Buffer.MemoryCopy(ptr, mapped + (i * SbtProps.RegionAligned), props.ShaderGroupHandleSize, props.ShaderGroupHandleSize);
        }
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length };
        fixed (byte* pCode = code)
        {
            createInfo.PCode = (uint*)pCode;
            _device.Vk.CreateShaderModule(_device.Device, in createInfo, null, out ShaderModule module);
            return module;
        }
    }

    public void Dispose()
    {
        SbtBuffer?.Dispose();
        _device.Vk.DestroyPipeline(_device.Device, Pipeline, null);
        _device.Vk.DestroyPipelineLayout(_device.Device, PipelineLayout, null);
        _device.Vk.DestroyDescriptorSetLayout(_device.Device, DescriptorSetLayout, null);
    }
}
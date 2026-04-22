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
            new() { Binding = 3, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr | ShaderStageFlags.AnyHitBitKhr },
            new() { Binding = 4, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr | ShaderStageFlags.AnyHitBitKhr },
            new() { Binding = 5, DescriptorType = DescriptorType.StorageImage, DescriptorCount = 1, StageFlags = ShaderStageFlags.RaygenBitKhr },
            new() { Binding = 6, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ClosestHitBitKhr }
        ];

        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new() { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 7, PBindings = pBindings };
            _device.Vk.CreateDescriptorSetLayout(_device.Device, in layoutInfo, null, out DescriptorSetLayout);
        }
    }

    private static byte[] GetShaderResourceBytes(string name)
    {
        using var stream = System.Reflection.Assembly.GetEntryAssembly()!.GetManifestResourceStream("Minecraft.NET.Assets.Shaders." + name + ".spv");
        if (stream == null) throw new FileNotFoundException($"Shader resource {name} not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private void CreatePipeline()
    {
        byte[] rgenSpv = GetShaderResourceBytes("raygen");
        byte[] rmissSpv = GetShaderResourceBytes("miss");
        byte[] rchitSpv = GetShaderResourceBytes("chit");
        byte[] rahitSpv = GetShaderResourceBytes("ahit");

        ShaderModule rgenModule = CreateShaderModule(rgenSpv);
        ShaderModule rmissModule = CreateShaderModule(rmissSpv);
        ShaderModule rchitModule = CreateShaderModule(rchitSpv);
        ShaderModule rahitModule = CreateShaderModule(rahitSpv);

        var stages = stackalloc PipelineShaderStageCreateInfo[4];
        stages[0] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.RaygenBitKhr, Module = rgenModule, PName = (byte*)SilkMarshal.StringToPtr("main") };
        stages[1] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.MissBitKhr, Module = rmissModule, PName = (byte*)SilkMarshal.StringToPtr("main") };
        stages[2] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ClosestHitBitKhr, Module = rchitModule, PName = (byte*)SilkMarshal.StringToPtr("main") };
        stages[3] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.AnyHitBitKhr, Module = rahitModule, PName = (byte*)SilkMarshal.StringToPtr("main") };

        var groups = stackalloc RayTracingShaderGroupCreateInfoKHR[3];
        groups[0] = new() { SType = StructureType.RayTracingShaderGroupCreateInfoKhr, Type = RayTracingShaderGroupTypeKHR.GeneralKhr, GeneralShader = 0, ClosestHitShader = Vk.ShaderUnusedKhr, AnyHitShader = Vk.ShaderUnusedKhr, IntersectionShader = Vk.ShaderUnusedKhr };
        groups[1] = new() { SType = StructureType.RayTracingShaderGroupCreateInfoKhr, Type = RayTracingShaderGroupTypeKHR.GeneralKhr, GeneralShader = 1, ClosestHitShader = Vk.ShaderUnusedKhr, AnyHitShader = Vk.ShaderUnusedKhr, IntersectionShader = Vk.ShaderUnusedKhr };
        groups[2] = new() { SType = StructureType.RayTracingShaderGroupCreateInfoKhr, Type = RayTracingShaderGroupTypeKHR.TrianglesHitGroupKhr, GeneralShader = Vk.ShaderUnusedKhr, ClosestHitShader = 2, AnyHitShader = 3, IntersectionShader = Vk.ShaderUnusedKhr };

        DescriptorSetLayout layout = DescriptorSetLayout;
        PipelineLayoutCreateInfo pipelineLayoutInfo = new() { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &layout };
        _device.Vk.CreatePipelineLayout(_device.Device, in pipelineLayoutInfo, null, out PipelineLayout);

        RayTracingPipelineCreateInfoKHR pipelineInfo = new()
        {
            SType = StructureType.RayTracingPipelineCreateInfoKhr,
            StageCount = 4,
            PStages = stages,
            GroupCount = 3,
            PGroups = groups,
            MaxPipelineRayRecursionDepth = 1,
            Layout = PipelineLayout
        };

        _device.KhrRayTracingPipeline.CreateRayTracingPipelines(_device.Device, default, default, 1, in pipelineInfo, null, out Pipeline);

        _device.Vk.DestroyShaderModule(_device.Device, rgenModule, null);
        _device.Vk.DestroyShaderModule(_device.Device, rmissModule, null);
        _device.Vk.DestroyShaderModule(_device.Device, rchitModule, null);
        _device.Vk.DestroyShaderModule(_device.Device, rahitModule, null);
        SilkMarshal.Free((nint)stages[0].PName);
        SilkMarshal.Free((nint)stages[1].PName);
        SilkMarshal.Free((nint)stages[2].PName);
        SilkMarshal.Free((nint)stages[3].PName);
    }

    private static uint AlignUp(uint size, uint alignment) => (size + alignment - 1) & ~(alignment - 1);

    private void CreateSBT()
    {
        var props = new PhysicalDeviceRayTracingPipelinePropertiesKHR { SType = StructureType.PhysicalDeviceRayTracingPipelinePropertiesKhr };
        var props2 = new PhysicalDeviceProperties2 { SType = StructureType.PhysicalDeviceProperties2, PNext = &props };
        _device.Vk.GetPhysicalDeviceProperties2(_device.PhysicalDevice, &props2);

        SbtProps = new SbtProperties { HandleSize = props.ShaderGroupHandleSize, RegionAligned = AlignUp(props.ShaderGroupHandleSize, props.ShaderGroupBaseAlignment) };
        uint sbtSize = SbtProps.RegionAligned * 3;
        SbtBuffer = new VulkanBuffer(_device, sbtSize, BufferUsageFlags.ShaderBindingTableBitKhr | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        byte[] handles = new byte[props.ShaderGroupHandleSize * 3];
        fixed (byte* pHandles = handles)
            _device.KhrRayTracingPipeline.GetRayTracingShaderGroupHandles(_device.Device, Pipeline, 0, 3, (nuint)handles.Length, pHandles);

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
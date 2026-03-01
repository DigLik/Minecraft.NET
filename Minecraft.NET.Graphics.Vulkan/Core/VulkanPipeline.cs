using Minecraft.NET.Engine.Abstractions.Graphics;

using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanPipeline : IDisposable
{
    private readonly VulkanDevice _device;
    public PipelineLayout PipelineLayout;
    public DescriptorSetLayout DescriptorSetLayout;
    public Pipeline GraphicsPipeline;

    public VulkanPipeline(VulkanDevice device, VulkanSwapchain swapchain, VertexElement[] vertexLayout, uint vertexStride)
    {
        _device = device;
        CreateDescriptorSetLayout();
        CreateGraphicsPipeline(swapchain, vertexLayout, vertexStride);
    }

    private void CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0, DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1, StageFlags = ShaderStageFlags.VertexBit
        };

        DescriptorSetLayoutBinding samplerLayoutBinding = new()
        {
            Binding = 1, DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit
        };

        var bindings = stackalloc[] { uboLayoutBinding, samplerLayoutBinding };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 2, PBindings = bindings
        };
        _device.Vk.CreateDescriptorSetLayout(_device.Device, in layoutInfo, null, out DescriptorSetLayout);
    }

    private static Format GetVkFormat(VertexFormat format) => format switch
    {
        VertexFormat.Float => Format.R32Sfloat,
        VertexFormat.Float2 => Format.R32G32Sfloat,
        VertexFormat.Float3 => Format.R32G32B32Sfloat,
        VertexFormat.Float4 => Format.R32G32B32A32Sfloat,
        VertexFormat.Int => Format.R32Sint,
        VertexFormat.UInt => Format.R32Uint,
        _ => Format.Undefined
    };

    private void CreateGraphicsPipeline(VulkanSwapchain swapchain, VertexElement[] vertexLayout, uint vertexStride)
    {
        using var compiler = new ShaderCompiler();

        string shaderCode = File.ReadAllText("Assets/Shaders/chunk.hlsl");
        var vertSpv = compiler.Compile(shaderCode, "chunk.hlsl", Silk.NET.Shaderc.ShaderKind.VertexShader, "VSMain");
        var fragSpv = compiler.Compile(shaderCode, "chunk.hlsl", Silk.NET.Shaderc.ShaderKind.FragmentShader, "PSMain");

        ShaderModule vertModule = CreateShaderModule(vertSpv);
        ShaderModule fragModule = CreateShaderModule(fragSpv);

        PipelineShaderStageCreateInfo vertStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertModule,
            PName = (byte*)SilkMarshal.StringToPtr("VSMain")
        };

        PipelineShaderStageCreateInfo fragStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragModule,
            PName = (byte*)SilkMarshal.StringToPtr("PSMain")
        };

        var shaderStages = stackalloc[] { vertStageInfo, fragStageInfo };

        var bindingDescription = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = vertexStride,
            InputRate = VertexInputRate.Vertex
        };

        var attributeDescriptions = new VertexInputAttributeDescription[vertexLayout.Length];
        for (int i = 0; i < vertexLayout.Length; i++)
        {
            attributeDescriptions[i] = new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = vertexLayout[i].Location,
                Format = GetVkFormat(vertexLayout[i].Format),
                Offset = vertexLayout[i].Offset
            };
        }

        fixed (VertexInputAttributeDescription* pAttributes = attributeDescriptions)
        {
            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1, PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = (uint)vertexLayout.Length, PVertexAttributeDescriptions = pAttributes
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = Vk.False
            };

            Viewport viewport = new()
            {
                X = 0, Y = 0,
                Width = swapchain.Extent.Width, Height = swapchain.Extent.Height,
                MinDepth = 0.0f, MaxDepth = 1.0f
            };

            Rect2D scissor = new() { Offset = new Offset2D(0, 0), Extent = swapchain.Extent };

            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1, PViewports = &viewport,
                ScissorCount = 1, PScissors = &scissor
            };

            PipelineRasterizationStateCreateInfo rasterizer = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = Vk.False, RasterizerDiscardEnable = Vk.False,
                PolygonMode = PolygonMode.Fill, LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit, FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = Vk.False
            };

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = Vk.False, RasterizationSamples = SampleCountFlags.Count1Bit
            };

            PipelineDepthStencilStateCreateInfo depthStencil = new()
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = Vk.True, DepthWriteEnable = Vk.True,
                DepthCompareOp = CompareOp.Less, DepthBoundsTestEnable = Vk.False, StencilTestEnable = Vk.False
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new()
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = Vk.False
            };

            PipelineColorBlendStateCreateInfo colorBlending = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = Vk.False, AttachmentCount = 1, PAttachments = &colorBlendAttachment
            };

            PushConstantRange pushConstantRange = new()
            {
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Size = (uint)sizeof(Utils.Math.Vector3<float>)
            };

            DescriptorSetLayout layout = DescriptorSetLayout;
            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &layout,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange
            };

            _device.Vk.CreatePipelineLayout(_device.Device, in pipelineLayoutInfo, null, out PipelineLayout);

            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2, PStages = shaderStages,
                PVertexInputState = &vertexInputInfo, PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState, PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling, PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending, Layout = PipelineLayout,
                RenderPass = swapchain.RenderPass, Subpass = 0
            };

            _device.Vk.CreateGraphicsPipelines(_device.Device, default, 1, in pipelineInfo, null, out GraphicsPipeline);
        }

        _device.Vk.DestroyShaderModule(_device.Device, fragModule, null);
        _device.Vk.DestroyShaderModule(_device.Device, vertModule, null);
        SilkMarshal.Free((nint)vertStageInfo.PName);
        SilkMarshal.Free((nint)fragStageInfo.PName);
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length
        };

        fixed (byte* pCode = code)
        {
            createInfo.PCode = (uint*)pCode;
            _device.Vk.CreateShaderModule(_device.Device, in createInfo, null, out ShaderModule module);
            return module;
        }
    }

    public void Dispose()
    {
        _device.Vk.DestroyPipeline(_device.Device, GraphicsPipeline, null);
        _device.Vk.DestroyPipelineLayout(_device.Device, PipelineLayout, null);
        _device.Vk.DestroyDescriptorSetLayout(_device.Device, DescriptorSetLayout, null);
    }
}
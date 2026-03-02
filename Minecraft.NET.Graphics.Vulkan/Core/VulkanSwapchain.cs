using System.Runtime.CompilerServices;

using Minecraft.NET.Utils.Math;

using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanSwapchain : IDisposable
{
    private readonly VulkanDevice _device;

    public SwapchainKHR Swapchain;
    public Format ImageFormat;
    public Extent2D Extent;

    public Image[] Images = [];
    public ImageView[] ImageViews = [];
    public Framebuffer[] Framebuffers = [];

    public Image[] DepthImages = [];
    public DeviceMemory[] DepthImageMemories = [];
    public ImageView[] DepthImageViews = [];

    public RenderPass RenderPass;
    public Format DepthFormat;

    public VulkanSwapchain(VulkanDevice device, Vector2<int> windowSize)
    {
        _device = device;
        DepthFormat = FindDepthFormat();

        CreateSwapchain(windowSize);
        CreateImageViews();
        CreateRenderPass();
        CreateDepthResources();
        CreateFramebuffers();
    }

    private Format FindDepthFormat()
    {
        Format[] candidates = [Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint];

        foreach (var format in candidates)
        {
            _device.Vk.GetPhysicalDeviceFormatProperties(_device.PhysicalDevice, format, out var props);

            if ((props.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) != 0)
                return format;
        }

        return Format.D32Sfloat;
    }

    private void CreateSwapchain(Vector2<int> windowSize)
    {
        _device.KhrSurface.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _device.Surface, out var capabilities);

        Extent = new Extent2D((uint)Math.Max(1, windowSize.X), (uint)Math.Max(1, windowSize.Y));
        ImageFormat = Format.B8G8R8A8Unorm;

        uint imageCount = 3;
        if (capabilities.MinImageCount > imageCount)
            imageCount = capabilities.MinImageCount;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        uint presentModeCount = 0;
        _device.KhrSurface.GetPhysicalDeviceSurfacePresentModes(_device.PhysicalDevice, _device.Surface, ref presentModeCount, null);
        var presentModes = new PresentModeKHR[presentModeCount];
        _device.KhrSurface.GetPhysicalDeviceSurfacePresentModes(_device.PhysicalDevice, _device.Surface, ref presentModeCount, out presentModes[0]);

        PresentModeKHR presentMode = PresentModeKHR.FifoKhr;
        foreach (var mode in presentModes)
        {
            if (mode == PresentModeKHR.MailboxKhr)
            {
                presentMode = mode;
                break;
            }
        }

        uint[] queueFamilyIndices = [_device.GraphicsFamilyIndex, _device.PresentFamilyIndex];
        bool sameQueue = _device.GraphicsFamilyIndex == _device.PresentFamilyIndex;

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _device.Surface,
            MinImageCount = imageCount,
            ImageFormat = ImageFormat,
            ImageColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr,
            ImageExtent = Extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = sameQueue ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = sameQueue ? 0u : 2u,
            PQueueFamilyIndices = sameQueue ? null : (uint*)Unsafe.AsPointer(ref queueFamilyIndices[0]),
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = Vk.True
        };

        if (_device.KhrSwapchain.CreateSwapchain(_device.Device, in createInfo, null, out Swapchain) != Result.Success)
            throw new Exception("Failed to create swapchain!");

        _device.KhrSwapchain.GetSwapchainImages(_device.Device, Swapchain, ref imageCount, null);
        Images = new Image[imageCount];
        _device.KhrSwapchain.GetSwapchainImages(_device.Device, Swapchain, ref imageCount, out Images[0]);
    }

    private void CreateImageViews()
    {
        ImageViews = new ImageView[Images.Length];

        for (int i = 0; i < Images.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = Images[i],
                ViewType = ImageViewType.Type2D,
                Format = ImageFormat,
                Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            _device.Vk.CreateImageView(_device.Device, in createInfo, null, out ImageViews[i]);
        }
    }

    private void CreateDepthResources()
    {
        DepthImages = new Image[Images.Length];
        DepthImageMemories = new DeviceMemory[Images.Length];
        DepthImageViews = new ImageView[Images.Length];

        for (int i = 0; i < Images.Length; i++)
        {
            ImageCreateInfo imageInfo = new()
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(Extent.Width, Extent.Height, 1),
                MipLevels = 1, ArrayLayers = 1,
                Format = DepthFormat,
                Tiling = ImageTiling.Optimal,
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsageFlags.DepthStencilAttachmentBit,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.Count1Bit
            };

            _device.Vk.CreateImage(_device.Device, in imageInfo, null, out DepthImages[i]);

            _device.Vk.GetImageMemoryRequirements(_device.Device, DepthImages[i], out var memRequirements);

            MemoryAllocateInfo allocInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = _device.FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out DepthImageMemories[i]);
            _device.Vk.BindImageMemory(_device.Device, DepthImages[i], DepthImageMemories[i], 0);

            ImageViewCreateInfo viewInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = DepthImages[i],
                ViewType = ImageViewType.Type2D,
                Format = DepthFormat,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1)
            };

            _device.Vk.CreateImageView(_device.Device, in viewInfo, null, out DepthImageViews[i]);
        }
    }

    private void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = ImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentDescription depthAttachment = new()
        {
            Format = DepthFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference colorRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        AttachmentReference depthRef = new() { Attachment = 1, Layout = ImageLayout.DepthStencilAttachmentOptimal };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorRef,
            PDepthStencilAttachment = &depthRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };

        var attachments = stackalloc[] { colorAttachment, depthAttachment };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        _device.Vk.CreateRenderPass(_device.Device, in renderPassInfo, null, out RenderPass);
    }

    private void CreateFramebuffers()
    {
        Framebuffers = new Framebuffer[ImageViews.Length];

        for (int i = 0; i < ImageViews.Length; i++)
        {
            var attachments = stackalloc[] { ImageViews[i], DepthImageViews[i] };

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = RenderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = Extent.Width,
                Height = Extent.Height,
                Layers = 1
            };

            _device.Vk.CreateFramebuffer(_device.Device, in framebufferInfo, null, out Framebuffers[i]);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < DepthImages.Length; i++)
        {
            _device.Vk.DestroyImageView(_device.Device, DepthImageViews[i], null);
            _device.Vk.DestroyImage(_device.Device, DepthImages[i], null);
            _device.Vk.FreeMemory(_device.Device, DepthImageMemories[i], null);
        }

        foreach (var fb in Framebuffers) _device.Vk.DestroyFramebuffer(_device.Device, fb, null);
        foreach (var iv in ImageViews) _device.Vk.DestroyImageView(_device.Device, iv, null);
        _device.Vk.DestroyRenderPass(_device.Device, RenderPass, null);
        _device.KhrSwapchain.DestroySwapchain(_device.Device, Swapchain, null);
    }
}
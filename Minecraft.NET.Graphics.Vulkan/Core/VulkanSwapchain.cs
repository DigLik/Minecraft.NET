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

    public VulkanSwapchain(VulkanDevice device, Vector2<int> windowSize)
    {
        _device = device;
        CreateSwapchain(windowSize);
        CreateImageViews();
    }

    private void CreateSwapchain(Vector2<int> windowSize)
    {
        _device.KhrSurface.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _device.Surface, out var capabilities);

        Extent = new Extent2D((uint)Math.Max(1, windowSize.X), (uint)Math.Max(1, windowSize.Y));
        ImageFormat = Format.B8G8R8A8Unorm;

        uint imageCount = 3;
        if (capabilities.MinImageCount > imageCount) imageCount = capabilities.MinImageCount;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount) imageCount = capabilities.MaxImageCount;

        uint presentModeCount = 0;
        _device.KhrSurface.GetPhysicalDeviceSurfacePresentModes(_device.PhysicalDevice, _device.Surface, ref presentModeCount, null);
        var presentModes = new PresentModeKHR[presentModeCount];
        _device.KhrSurface.GetPhysicalDeviceSurfacePresentModes(_device.PhysicalDevice, _device.Surface, ref presentModeCount, out presentModes[0]);

        PresentModeKHR presentMode = PresentModeKHR.FifoKhr;
        foreach (var mode in presentModes)
            if (mode == PresentModeKHR.MailboxKhr) { presentMode = mode; break; }

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
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
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

    public void Dispose()
    {
        foreach (var iv in ImageViews) _device.Vk.DestroyImageView(_device.Device, iv, null);
        _device.KhrSwapchain.DestroySwapchain(_device.Device, Swapchain, null);
    }
}
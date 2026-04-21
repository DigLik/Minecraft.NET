using Minecraft.NET.Engine.Abstractions.Graphics;

using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanTextureArray : ITextureArray
{
    private readonly VulkanDevice _device;

    public Image Image;
    public DeviceMemory ImageMemory;
    public ImageView ImageView;
    public Sampler Sampler;

    public VulkanTextureArray(VulkanDevice device, int width, int height, byte[][] pixels)
    {
        _device = device;

        uint layers = (uint)pixels.Length;
        ulong layerSize = (ulong)(width * height * 4);
        ulong imageSize = layerSize * layers;

        VulkanBuffer stagingBuffer = new(_device, imageSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        for (int i = 0; i < layers; i++)
        {
            fixed (byte* pPixels = pixels[i])
            {
                System.Buffer.MemoryCopy(pPixels, (byte*)stagingBuffer.MappedMemory + (layerSize * (ulong)i), layerSize, layerSize);
            }
        }

        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = layers,
            Format = Format.R8G8B8A8Unorm,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit
        };

        _device.Vk.CreateImage(_device.Device, in imageInfo, null, out Image);

        _device.Vk.GetImageMemoryRequirements(_device.Device, Image, out var memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = _device.FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out ImageMemory);
        _device.Vk.BindImageMemory(_device.Device, Image, ImageMemory, 0);

        TransitionImageLayout(ImageLayout.Undefined, ImageLayout.TransferDstOptimal, layers);
        CopyBufferToImage(stagingBuffer.Buffer, (uint)width, (uint)height, layers);
        TransitionImageLayout(ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, layers);

        stagingBuffer.Dispose();

        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Image,
            ViewType = ImageViewType.Type2DArray,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, layers)
        };

        _device.Vk.CreateImageView(_device.Device, in viewInfo, null, out ImageView);

        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Nearest,
            MinFilter = Filter.Nearest,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = Vk.False,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = Vk.False,
            CompareEnable = Vk.False,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f
        };

        _device.Vk.CreateSampler(_device.Device, in samplerInfo, null, out Sampler);
    }

    private void TransitionImageLayout(ImageLayout oldLayout, ImageLayout newLayout, uint layers)
    {
        CommandBuffer cmd = _device.BeginSingleTimeCommands();

        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = Image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, layers)
        };

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = AccessFlags2.None;
            barrier.DstAccessMask = AccessFlags2.TransferWriteBit;
            barrier.SrcStageMask = PipelineStageFlags2.TopOfPipeBit;
            barrier.DstStageMask = PipelineStageFlags2.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags2.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags2.None;
            barrier.SrcStageMask = PipelineStageFlags2.TransferBit;
            barrier.DstStageMask = PipelineStageFlags2.BottomOfPipeBit;
        }
        else throw new Exception("Unsupported layout transition!");

        DependencyInfo depInfo = new() { SType = StructureType.DependencyInfo, ImageMemoryBarrierCount = 1, PImageMemoryBarriers = &barrier };
        _device.Vk.CmdPipelineBarrier2(cmd, in depInfo);

        _device.EndSingleTimeCommands(cmd);
    }

    private void CopyBufferToImage(Silk.NET.Vulkan.Buffer buffer, uint width, uint height, uint layers)
    {
        CommandBuffer cmd = _device.BeginSingleTimeCommands();

        BufferImageCopy2 region = new()
        {
            SType = StructureType.BufferImageCopy2,
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, layers),
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1)
        };

        CopyBufferToImageInfo2 copyInfo = new()
        {
            SType = StructureType.CopyBufferToImageInfo2,
            SrcBuffer = buffer,
            DstImage = Image,
            DstImageLayout = ImageLayout.TransferDstOptimal,
            RegionCount = 1,
            PRegions = &region
        };

        _device.Vk.CmdCopyBufferToImage2(cmd, in copyInfo);

        _device.EndSingleTimeCommands(cmd);
    }

    public void Dispose()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);
        _device.Vk.DestroySampler(_device.Device, Sampler, null);
        _device.Vk.DestroyImageView(_device.Device, ImageView, null);
        _device.Vk.DestroyImage(_device.Device, Image, null);
        _device.Vk.FreeMemory(_device.Device, ImageMemory, null);
    }
}
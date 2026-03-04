using System.Runtime.InteropServices;

using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanDevice : IDisposable
{
    public readonly Vk Vk;
    public KhrSurface KhrSurface = null!;
    public readonly KhrSwapchain KhrSwapchain;

    public KhrAccelerationStructure KhrAccelerationStructure;
    public KhrRayTracingPipeline KhrRayTracingPipeline;
    public KhrDeferredHostOperations KhrDeferredHostOperations;

    public Instance Instance;
    public PhysicalDevice PhysicalDevice;
    public Device Device;
    public Queue GraphicsQueue;
    public Queue PresentQueue;
    public SurfaceKHR Surface;

    public CommandPool TransferCommandPool;

    public uint GraphicsFamilyIndex;
    public uint PresentFamilyIndex;

    public readonly Lock QueueLock = new();

    public VulkanDevice(void* windowHandle)
    {
        Vk = Vk.GetApi();

        CreateInstance();
        CreateSurface(windowHandle);
        PickPhysicalDevice();
        CreateLogicalDevice();

        if (!Vk.TryGetDeviceExtension(Instance, Device, out KhrSwapchain)) throw new Exception("Vulkan KHR_swapchain extension not found.");
        if (!Vk.TryGetDeviceExtension(Instance, Device, out KhrAccelerationStructure)) throw new Exception("VK_KHR_acceleration_structure not found.");
        if (!Vk.TryGetDeviceExtension(Instance, Device, out KhrRayTracingPipeline)) throw new Exception("VK_KHR_ray_tracing_pipeline not found.");
        if (!Vk.TryGetDeviceExtension(Instance, Device, out KhrDeferredHostOperations)) throw new Exception("VK_KHR_deferred_host_operations not found.");
    }

    private void CreateInstance()
    {
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("Minecraft.NET"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("Minecraft.NET Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13
        };

        var glfw = Glfw.GetApi();
        byte** glfwExtensions = glfw.GetRequiredInstanceExtensions(out uint glfwExtensionCount);

        var extensions = new List<string>();
        for (int i = 0; i < glfwExtensionCount; i++)
            extensions.Add(Marshal.PtrToStringAnsi((IntPtr)glfwExtensions[i])!);

        var pExtensions = SilkMarshal.StringArrayToPtr([.. extensions]);

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensions.Count,
            PpEnabledExtensionNames = (byte**)pExtensions,
        };

        if (Vk.CreateInstance(in createInfo, null, out Instance) != Result.Success)
            throw new Exception("Failed to create Vulkan Instance!");

        SilkMarshal.Free((nint)pExtensions);

        if (!Vk.TryGetInstanceExtension(Instance, out KhrSurface))
            throw new Exception("Vulkan KHR_surface extension not found.");
    }

    private void CreateSurface(void* windowHandle)
    {
        var glfw = Glfw.GetApi();
        VkNonDispatchableHandle surfaceHandle;
        if (glfw.CreateWindowSurface(new VkHandle(Instance.Handle), (WindowHandle*)windowHandle, null, &surfaceHandle) != (int)Result.Success)
            throw new Exception("Failed to create window surface!");

        Surface = new SurfaceKHR(surfaceHandle.Handle);
    }

    private void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        Vk.EnumeratePhysicalDevices(Instance, ref deviceCount, null);
        var devices = new PhysicalDevice[deviceCount];
        Vk.EnumeratePhysicalDevices(Instance, ref deviceCount, ref devices[0]);

        foreach (var device in devices)
        {
            uint queueFamilyCount = 0;
            Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);
            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, out queueFamilies[0]);

            uint? graphicsFamily = null;
            uint? presentFamily = null;

            for (uint i = 0; i < queueFamilyCount; i++)
            {
                if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit)) graphicsFamily = i;

                KhrSurface.GetPhysicalDeviceSurfaceSupport(device, i, Surface, out var presentSupport);
                if (presentSupport) presentFamily = i;

                if (graphicsFamily.HasValue && presentFamily.HasValue) break;
            }

            if (graphicsFamily.HasValue && presentFamily.HasValue)
            {
                PhysicalDevice = device;
                GraphicsFamilyIndex = graphicsFamily.Value;
                PresentFamilyIndex = presentFamily.Value;
                return;
            }
        }
        throw new Exception("Failed to find suitable GPU!");
    }

    private void CreateLogicalDevice()
    {
        var uniqueQueueFamilies = new HashSet<uint> { GraphicsFamilyIndex, PresentFamilyIndex };
        var queueCreateInfos = new DeviceQueueCreateInfo[uniqueQueueFamilies.Count];
        float queuePriority = 1.0f;

        int i = 0;
        foreach (var queueFamily in uniqueQueueFamilies)
        {
            queueCreateInfos[i++] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = queueFamily,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        var deviceExtensions = new[] {
            KhrSwapchain.ExtensionName,
            KhrAccelerationStructure.ExtensionName,
            KhrRayTracingPipeline.ExtensionName,
            KhrDeferredHostOperations.ExtensionName,
            "VK_KHR_ray_query"
        };
        var pDeviceExtensions = SilkMarshal.StringArrayToPtr(deviceExtensions);

        PhysicalDeviceBufferDeviceAddressFeatures bdaFeatures = new()
        {
            SType = StructureType.PhysicalDeviceBufferDeviceAddressFeatures,
            BufferDeviceAddress = Vk.True
        };

        PhysicalDeviceAccelerationStructureFeaturesKHR asFeatures = new()
        {
            SType = StructureType.PhysicalDeviceAccelerationStructureFeaturesKhr,
            AccelerationStructure = Vk.True,
            PNext = &bdaFeatures
        };

        PhysicalDeviceRayTracingPipelineFeaturesKHR rtFeatures = new()
        {
            SType = StructureType.PhysicalDeviceRayTracingPipelineFeaturesKhr,
            RayTracingPipeline = Vk.True,
            PNext = &asFeatures
        };

        PhysicalDeviceRayQueryFeaturesKHR rqFeatures = new()
        {
            SType = StructureType.PhysicalDeviceRayQueryFeaturesKhr,
            RayQuery = Vk.True,
            PNext = &rtFeatures
        };

        PhysicalDeviceFeatures2 deviceFeatures = new()
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            Features = new PhysicalDeviceFeatures { SamplerAnisotropy = Vk.True },
            PNext = &rqFeatures
        };

        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            DeviceCreateInfo createInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)queueCreateInfos.Length,
                PQueueCreateInfos = pQueueCreateInfos,
                PNext = &deviceFeatures,
                EnabledExtensionCount = (uint)deviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)pDeviceExtensions
            };

            if (Vk.CreateDevice(PhysicalDevice, in createInfo, null, out Device) != Result.Success)
                throw new Exception("Failed to create logical device!");
        }

        SilkMarshal.Free((nint)pDeviceExtensions);

        Vk.GetDeviceQueue(Device, GraphicsFamilyIndex, 0, out GraphicsQueue);
        Vk.GetDeviceQueue(Device, PresentFamilyIndex, 0, out PresentQueue);

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit,
            QueueFamilyIndex = GraphicsFamilyIndex
        };

        Vk.CreateCommandPool(Device, in poolInfo, null, out TransferCommandPool);
    }

    public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out var memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                return (uint)i;

        throw new Exception("Failed to find suitable memory type!");
    }

    public CommandBuffer BeginSingleTimeCommands()
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = TransferCommandPool,
            CommandBufferCount = 1
        };

        Vk.AllocateCommandBuffers(Device, in allocInfo, out CommandBuffer commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        Vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        return commandBuffer;
    }

    public void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        Vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        lock (QueueLock)
        {
            Vk.QueueSubmit(GraphicsQueue, 1, in submitInfo, default);
            Vk.QueueWaitIdle(GraphicsQueue);
        }

        Vk.FreeCommandBuffers(Device, TransferCommandPool, 1, in commandBuffer);
    }

    public void Dispose()
    {
        KhrDeferredHostOperations?.Dispose();
        KhrRayTracingPipeline?.Dispose();
        KhrAccelerationStructure?.Dispose();

        Vk.DestroyCommandPool(Device, TransferCommandPool, null);
        Vk.DestroyDevice(Device, null);
        KhrSurface.DestroySurface(Instance, Surface, null);
        Vk.DestroyInstance(Instance, null);
        Vk.Dispose();
    }
}
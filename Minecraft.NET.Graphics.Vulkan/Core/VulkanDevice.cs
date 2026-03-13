using System.Runtime.InteropServices;

using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

#if DEBUG
using Silk.NET.Vulkan.Extensions.EXT;
#endif

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanDevice : IDisposable
{
    public readonly Vk Vk;
    public KhrSurface KhrSurface = null!;
    public readonly KhrSwapchain KhrSwapchain;

    public KhrAccelerationStructure KhrAccelerationStructure;
    public KhrRayTracingPipeline KhrRayTracingPipeline;
    public KhrDeferredHostOperations KhrDeferredHostOperations;

#if DEBUG
    public ExtDebugUtils? ExtDebugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
    private readonly string[] _validationLayers = ["VK_LAYER_KHRONOS_validation"];
#endif

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
        SetupDebugMessenger();
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
#if DEBUG
        if (!CheckValidationLayerSupport())
            throw new Exception("Validation layers requested, but not available!");
#endif

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("Minecraft.NET"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("Minecraft.NET Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = new Version32(1, 4, 0)
        };

        var glfw = Glfw.GetApi();
        byte** glfwExtensions = glfw.GetRequiredInstanceExtensions(out uint glfwExtensionCount);

        var extensions = new List<string>();
        for (int i = 0; i < glfwExtensionCount; i++)
            extensions.Add(Marshal.PtrToStringAnsi((IntPtr)glfwExtensions[i])!);

#if DEBUG
        extensions.Add(ExtDebugUtils.ExtensionName);
#endif

        var pExtensions = SilkMarshal.StringArrayToPtr([.. extensions]);

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensions.Count,
            PpEnabledExtensionNames = (byte**)pExtensions,
        };

#if DEBUG
        var pValidationLayers = SilkMarshal.StringArrayToPtr(_validationLayers);
        createInfo.EnabledLayerCount = (uint)_validationLayers.Length;
        createInfo.PpEnabledLayerNames = (byte**)pValidationLayers;

        DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
        PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
        createInfo.PNext = &debugCreateInfo;
#else
        createInfo.EnabledLayerCount = 0;
        createInfo.PNext = null;
#endif

        if (Vk.CreateInstance(in createInfo, null, out Instance) != Result.Success)
            throw new Exception("Failed to create Vulkan Instance!");

        SilkMarshal.Free(pExtensions);
#if DEBUG
        SilkMarshal.Free((nint)pValidationLayers);
#endif

        if (!Vk.TryGetInstanceExtension(Instance, out KhrSurface))
            throw new Exception("Vulkan KHR_surface extension not found.");
    }

#if DEBUG
    private bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        Vk.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        Vk.EnumerateInstanceLayerProperties(ref layerCount, ref availableLayers[0]);

        foreach (var layerName in _validationLayers)
        {
            bool layerFound = false;
            foreach (var layerProperties in availableLayers)
            {
                var name = Marshal.PtrToStringAnsi((IntPtr)layerProperties.LayerName);
                if (name == layerName)
                {
                    layerFound = true;
                    break;
                }
            }
            if (!layerFound) return false;
        }
        return true;
    }

    private void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt;
        createInfo.PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback;
    }

    private void SetupDebugMessenger()
    {
        if (!Vk.TryGetInstanceExtension(Instance, out ExtDebugUtils)) return;

        DebugUtilsMessengerCreateInfoEXT createInfo = new();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        if (ExtDebugUtils!.CreateDebugUtilsMessenger(Instance, in createInfo, null, out _debugMessenger) != Result.Success)
            throw new Exception("Failed to set up debug messenger!");
    }

    private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        string message = Marshal.PtrToStringAnsi((IntPtr)pCallbackData->PMessage) ?? "Unknown Vulkan Error";
        
        if (messageSeverity >= DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt)
            Console.Error.WriteLine($"[Vulkan Error] {message}");
        else if (messageSeverity >= DebugUtilsMessageSeverityFlagsEXT.WarningBitExt)
            Console.WriteLine($"[Vulkan Warning] {message}");
        else
            Console.WriteLine($"[Vulkan] {message}");

        return Vk.False;
    }
#else
    private static void SetupDebugMessenger() { }
#endif

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

        PhysicalDeviceVulkan12Features vk12Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            TimelineSemaphore = Vk.True,
            BufferDeviceAddress = Vk.True
        };

        PhysicalDeviceVulkan13Features vk13Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            Synchronization2 = Vk.True,
            PNext = &vk12Features
        };

        PhysicalDeviceAccelerationStructureFeaturesKHR asFeatures = new()
        {
            SType = StructureType.PhysicalDeviceAccelerationStructureFeaturesKhr,
            AccelerationStructure = Vk.True,
            PNext = &vk13Features
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

        SilkMarshal.Free(pDeviceExtensions);

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

        CommandBufferSubmitInfo cmdInfo = new() { SType = StructureType.CommandBufferSubmitInfo, CommandBuffer = commandBuffer };
        SubmitInfo2 submitInfo = new() { SType = StructureType.SubmitInfo2, CommandBufferInfoCount = 1, PCommandBufferInfos = &cmdInfo };

        lock (QueueLock)
        {
            Vk.QueueSubmit2(GraphicsQueue, 1, in submitInfo, default);
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

#if DEBUG
        if (ExtDebugUtils != null)
        {
            ExtDebugUtils.DestroyDebugUtilsMessenger(Instance, _debugMessenger, null);
            ExtDebugUtils.Dispose();
        }
#endif

        KhrSurface.DestroySurface(Instance, Surface, null);
        Vk.DestroyInstance(Instance, null);
        Vk.Dispose();
    }
}
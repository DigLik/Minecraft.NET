using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Streamline;

public enum Result : int
{
    eOk = 0,
    eErrorIO,
    eErrorDriverOutOfDate,
    eErrorOSOutOfDate,
    eErrorOSDisabledHWS,
    eErrorDeviceNotCreated,
    eErrorNoSupportedAdapterFound,
    eErrorAdapterNotSupported,
    eErrorNoPlugins,
    eErrorVulkanAPI,
    eErrorDXGIAPI,
    eErrorD3DAPI,
    eErrorNRDAPI,
    eErrorNVAPI,
    eErrorReflexAPI,
    eErrorNGXFailed,
    eErrorJSONParsing,
    eErrorMissingProxy,
    eErrorMissingResourceState,
    eErrorInvalidIntegration,
    eErrorMissingInputParameter,
    eErrorNotInitialized,
    eErrorComputeFailed,
    eErrorInitNotCalled,
    eErrorExceptionHandler,
    eErrorInvalidParameter,
    eErrorMissingConstants,
    eErrorDuplicatedConstants,
    eErrorMissingOrInvalidAPI,
    eErrorCommonConstantsMissing,
    eErrorUnsupportedInterface,
    eErrorFeatureMissing,
    eErrorFeatureNotSupported,
    eErrorFeatureMissingHooks,
    eErrorFeatureFailedToLoad,
    eErrorFeatureWrongPriority,
    eErrorFeatureMissingDependency,
    eErrorFeatureManagerInvalidState,
    eErrorInvalidState,
    eWarnOutOfVRAM
}

public enum Feature : uint
{
    kFeatureDLSS = 0,
    kFeatureNIS = 2,
    kFeatureReflex = 3,
    kFeaturePCL = 4,
    kFeatureDeepDVC = 5,
    kFeatureLatewarp = 6,
    kFeatureDLSS_G = 1000,
    kFeatureDLSS_RR = 1001,
    kFeatureNvPerf = 1002,
    kFeatureDirectSR = 1003,
    kFeatureImGUI = 9999,
    kFeatureCommon = uint.MaxValue
}

public enum BufferType : uint
{
    kBufferTypeDepth = 0,
    kBufferTypeMotionVectors = 1,
    kBufferTypeHUDLessColor = 2,
    kBufferTypeScalingInputColor = 3,
    kBufferTypeScalingOutputColor = 4,
    kBufferTypeNormals = 5,
    kBufferTypeRoughness = 6,
    kBufferTypeAlbedo = 7,
    kBufferTypeSpecularAlbedo = 8,
    kBufferTypeIndirectAlbedo = 9,
    kBufferTypeSpecularMotionVectors = 10,
    kBufferTypeDisocclusionMask = 11,
    kBufferTypeEmissive = 12,
    kBufferTypeExposure = 13,
    kBufferTypeNormalRoughness = 14,
    kBufferTypeDiffuseHitNoisy = 15,
    kBufferTypeDiffuseHitDenoised = 16,
    kBufferTypeSpecularHitNoisy = 17,
    kBufferTypeSpecularHitDenoised = 18,
    kBufferTypeShadowNoisy = 19,
    kBufferTypeShadowDenoised = 20,
    kBufferTypeAmbientOcclusionNoisy = 21,
    kBufferTypeAmbientOcclusionDenoised = 22,
    kBufferTypeUIColorAndAlpha = 23,
    kBufferTypeShadowHint = 24,
    kBufferTypeReflectionHint = 25,
    kBufferTypeParticleHint = 26,
    kBufferTypeTransparencyHint = 27,
    kBufferTypeAnimatedTextureHint = 28,
    kBufferTypeBiasCurrentColorHint = 29,
    kBufferTypeRaytracingDistance = 30,
    kBufferTypeReflectionMotionVectors = 31,
    kBufferTypePosition = 32,
    kBufferTypeInvalidDepthMotionHint = 33,
    kBufferTypeAlpha = 34,
    kBufferTypeOpaqueColor = 35,
    kBufferTypeReactiveMaskHint = 36,
    kBufferTypeTransparencyAndCompositionMaskHint = 37,
    kBufferTypeReflectedAlbedo = 38,
    kBufferTypeColorBeforeParticles = 39,
    kBufferTypeColorBeforeTransparency = 40,
    kBufferTypeColorBeforeFog = 41,
    kBufferTypeSpecularHitDistance = 42,
    kBufferTypeSpecularRayDirectionHitDistance = 43,
    kBufferTypeSpecularRayDirection = 44,
    kBufferTypeDiffuseHitDistance = 45,
    kBufferTypeDiffuseRayDirectionHitDistance = 46,
    kBufferTypeDiffuseRayDirection = 47,
    kBufferTypeHiResDepth = 48,
    kBufferTypeLinearDepth = 49,
    kBufferTypeBidirectionalDistortionField = 50,
    kBufferTypeTransparencyLayer = 51,
    kBufferTypeTransparencyLayerOpacity = 52,
    kBufferTypeBackbuffer = 53,
    kBufferTypeNoWarpMask = 54,
    kBufferTypeColorAfterParticles = 55,
    kBufferTypeColorAfterTransparency = 56,
    kBufferTypeColorAfterFog = 57,
    kBufferTypeScreenSpaceSubsurfaceScatteringGuide = 58,
    kBufferTypeColorBeforeScreenSpaceSubsurfaceScattering = 59,
    kBufferTypeColorAfterScreenSpaceSubsurfaceScattering = 60,
    kBufferTypeScreenSpaceRefractionGuide = 61,
    kBufferTypeColorBeforeScreenSpaceRefraction = 62,
    kBufferTypeColorAfterScreenSpaceRefraction = 63,
    kBufferTypeDepthOfFieldGuide = 64,
    kBufferTypeColorBeforeDepthOfField = 65,
    kBufferTypeColorAfterDepthOfField = 66,
    kBufferTypeScalingOutputAlpha = 67,
    kBufferTypeUIAlpha = 68
}

public enum LogLevel : uint
{
    eOff = 0,
    eDefault,
    eVerbose,
    eCount
}

public enum ResourceType : sbyte
{
    eTex2d = 0,
    eBuffer,
    eCommandQueue,
    eCommandBuffer,
    eCommandPool,
    eFence,
    eSwapchain,
    eHostFence,
    eUnknown,
    eCount
}

public enum ResourceLifecycle : int
{
    eOnlyValidNow = 0,
    eValidUntilPresent,
    eValidUntilEvaluate
}

public enum Boolean : byte
{
    eFalse = 0,
    eTrue = 1,
    eInvalid = 2
}

public enum DLSSMode : uint
{
    eOff = 0,
    eMaxPerformance,
    eBalanced,
    eMaxQuality,
    eUltraPerformance,
    eUltraQuality,
    eDLAA,
    eCount
}

public enum DLSSDPreset : uint
{
    eDefault = 0,
    ePresetD = 4,
    ePresetE = 5,
    ePresetF = 6,
    ePresetG = 7,
    ePresetH = 8,
    ePresetI = 9,
    ePresetJ = 10,
    ePresetK = 11,
    ePresetL = 12,
    ePresetM = 13,
    ePresetN = 14,
    ePresetO = 15,
    eCount
}

public enum DLSSPreset : uint
{
    eDefault = 0,
    ePresetE = 5,
    ePresetF = 6,
    ePresetG = 7,
    ePresetH = 8,
    ePresetI = 9,
    ePresetJ = 10,
    ePresetK = 11,
    ePresetL = 12,
    ePresetM = 13,
    ePresetN = 14,
    ePresetO = 15,
    eCount
}

public enum DLSSDNormalRoughnessMode : uint
{
    eUnpacked = 0,
    ePacked,
    eCount
}

[StructLayout(LayoutKind.Sequential)]
public struct StructType
{
    public uint Data1;
    public ushort Data2;
    public ushort Data3;
    public unsafe fixed byte Data4[8];

    public StructType(uint d1, ushort d2, ushort d3, byte[] d4)
    {
        Data1 = d1;
        Data2 = d2;
        Data3 = d3;
        unsafe
        {
            fixed (byte* p = Data4)
            {
                for (int i = 0; i < 8; i++) p[i] = d4[i];
            }
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct BaseStructure
{
    public BaseStructure* Next;
    public StructType StructType;
    public nuint StructVersion;

    public BaseStructure(StructType t, uint v)
    {
        Next = null;
        StructType = t;
        StructVersion = (nuint)v;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Preferences
{
    public BaseStructure Base;
    public byte ShowConsole;
    private byte pad0, pad1, pad2; // 3 bytes padding
    public uint LogLevel;
    public char** PathsToPlugins;
    public uint NumPathsToPlugins;
    private uint pad3; // 4 bytes padding
    public char* PathToLogsAndData;
    public void* AllocateCallback;
    public void* ReleaseCallback;
    public void* LogMessageCallback;
    public ulong Flags;
    public uint* FeaturesToLoad;
    public uint NumFeaturesToLoad;
    public uint ApplicationId;
    public uint Engine;
    public byte* EngineVersion;
    public byte* ProjectId;
    public uint RenderAPI;
    private uint pad4; // 4 bytes padding

    public static Preferences Create()
    {
        var p = new Preferences();
        p.Base = new BaseStructure(new StructType(0x1ca10965, 0xbf8e, 0x432b, new byte[] { 0x8d, 0xa1, 0x67, 0x16, 0xd8, 0x79, 0xfb, 0x14 }), 1);
        p.ShowConsole = 0;
        p.LogLevel = (uint)Streamline.LogLevel.eDefault;
        p.PathsToPlugins = null;
        p.NumPathsToPlugins = 0;
        p.PathToLogsAndData = null;
        p.AllocateCallback = null;
        p.ReleaseCallback = null;
        p.LogMessageCallback = null;
        p.Flags = 0x01 | 0x08 | 0x40 | 0x80; // eDisableCLStateTracking | eAllowOTA | eLoadDownloadedPlugins | eUseFrameBasedResourceTagging
        p.FeaturesToLoad = null;
        p.NumFeaturesToLoad = 0;
        p.ApplicationId = 0;
        p.Engine = 0; // eCustom
        p.EngineVersion = null;
        p.ProjectId = null;
        p.RenderAPI = 2; // eVulkan
        return p;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VulkanInfo
{
    public BaseStructure Base;
    public void* Device;
    public void* Instance;
    public void* PhysicalDevice;
    public uint ComputeQueueIndex;
    public uint ComputeQueueFamily;
    public uint GraphicsQueueIndex;
    public uint GraphicsQueueFamily;
    public uint OpticalFlowQueueIndex;
    public uint OpticalFlowQueueFamily;
    public byte UseNativeOpticalFlowMode; // C++ bool (1 byte)
    private byte pad0, pad1, pad2; // 3 bytes padding
    public uint ComputeQueueCreateFlags;
    public uint GraphicsQueueCreateFlags;
    public uint OpticalFlowQueueCreateFlags;

    public static VulkanInfo Create()
    {
        var info = new VulkanInfo();
        info.Base = new BaseStructure(new StructType(0xeed6fd5, 0x82cd, 0x43a9, new byte[] { 0xbd, 0xb5, 0x47, 0xa5, 0xba, 0x2f, 0x45, 0xd6 }), 3);
        info.Device = null;
        info.Instance = null;
        info.PhysicalDevice = null;
        info.ComputeQueueIndex = 0;
        info.ComputeQueueFamily = 0;
        info.GraphicsQueueIndex = 0;
        info.GraphicsQueueFamily = 0;
        info.OpticalFlowQueueIndex = 0;
        info.OpticalFlowQueueFamily = 0;
        info.UseNativeOpticalFlowMode = 0;
        info.ComputeQueueCreateFlags = 0;
        info.GraphicsQueueCreateFlags = 0;
        info.OpticalFlowQueueCreateFlags = 0;
        return info;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Extent
{
    public uint Top;
    public uint Left;
    public uint Width;
    public uint Height;

    public Extent(uint w, uint h)
    {
        Top = 0; Left = 0; Width = w; Height = h;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Resource
{
    public BaseStructure Base;
    public ResourceType Type;
    public void* Native;
    public void* Memory;
    public void* View;
    public uint State;
    public uint Width;
    public uint Height;
    public uint NativeFormat;
    public uint MipLevels;
    public uint ArrayLayers;
    public ulong GpuVirtualAddress;
    public uint Flags;
    public uint Usage;
    public uint Reserved;

    public Resource(ResourceType type, void* native, void* mem, void* view, uint state, uint width, uint height, uint format, uint usage)
    {
        Base = new BaseStructure(new StructType(0x3a9d70cf, 0x2418, 0x4b72, new byte[] { 0x83, 0x91, 0x13, 0xf8, 0x72, 0x1c, 0x72, 0x61 }), 1);
        Type = type;
        Native = native;
        Memory = mem;
        View = view;
        State = state;
        Width = width;
        Height = height;
        NativeFormat = format;
        MipLevels = 1;
        ArrayLayers = 1;
        GpuVirtualAddress = 0;
        Flags = 0;
        Usage = usage;
        Reserved = 0;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ResourceTag
{
    public BaseStructure Base;
    public Resource* Resource;
    public BufferType Type;
    public ResourceLifecycle Lifecycle;
    public Extent Extent;

    public ResourceTag(Resource* r, BufferType t, ResourceLifecycle l, Extent e)
    {
        Base = new BaseStructure(new StructType(0x4c6a5aad, 0xb445, 0x496c, new byte[] { 0x87, 0xff, 0x1a, 0xf3, 0x84, 0x5b, 0xe6, 0x53 }), 1);
        Resource = r;
        Type = t;
        Lifecycle = l;
        Extent = e;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FrameToken
{
    public BaseStructure Base;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ViewportHandle
{
    public BaseStructure Base;
    public uint Value;

    public ViewportHandle(uint val)
    {
        Base = new BaseStructure(new StructType(0x171b6435, 0x9b3c, 0x4fc8, new byte[] { 0x99, 0x94, 0xfb, 0xe5, 0x25, 0x69, 0xaa, 0xa4 }), 1);
        Value = val;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AdapterInfo
{
    public BaseStructure Base;
    public byte* DeviceLUID;
    public uint DeviceLUIDSizeInBytes;
    public void* VkPhysicalDevice;

    public AdapterInfo(void* vkPhysDev)
    {
        Base = new BaseStructure(new StructType(0x0677315f, 0xa746, 0x4492, new byte[] { 0x9f, 0x42, 0xcb, 0x61, 0x42, 0xc9, 0xc3, 0xd4 }), 1);
        DeviceLUID = null;
        DeviceLUIDSizeInBytes = 0;
        VkPhysicalDevice = vkPhysDev;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DLSSDOptions
{
    public BaseStructure Base;
    public DLSSMode Mode;
    public uint OutputWidth;
    public uint OutputHeight;
    public float Sharpness;
    public float PreExposure;
    public float ExposureScale;
    public Boolean ColorBuffersHDR;
    public Boolean IndicatorInvertAxisX;
    public Boolean IndicatorInvertAxisY;
    private byte pad0; // 1 byte padding to align to 4-byte boundaries
    public DLSSDNormalRoughnessMode NormalRoughnessMode;
    public Matrix4x4 WorldToCameraView;
    public Matrix4x4 CameraViewToWorld;
    public Boolean AlphaUpscalingEnabled;
    private byte pad1, pad2, pad3; // 3 bytes padding to align to 4-byte boundaries
    public DLSSDPreset DlaaPreset;
    public DLSSDPreset QualityPreset;
    public DLSSDPreset BalancedPreset;
    public DLSSDPreset PerformancePreset;
    public DLSSDPreset UltraPerformancePreset;
    public DLSSDPreset UltraQualityPreset;

    public static DLSSDOptions Create()
    {
        var opt = new DLSSDOptions();
        opt.Base = new BaseStructure(new StructType(0x0ad87504, 0x774e, 0x4bf3, new byte[] { 0x96, 0x33, 0xa4, 0x4d, 0x1f, 0x7f, 0x9c, 0xb8 }), 3);
        opt.Mode = DLSSMode.eOff;
        opt.OutputWidth = uint.MaxValue;
        opt.OutputHeight = uint.MaxValue;
        opt.Sharpness = 0.0f;
        opt.PreExposure = 1.0f;
        opt.ExposureScale = 1.0f;
        opt.ColorBuffersHDR = Boolean.eTrue;
        opt.IndicatorInvertAxisX = Boolean.eFalse;
        opt.IndicatorInvertAxisY = Boolean.eFalse;
        opt.NormalRoughnessMode = DLSSDNormalRoughnessMode.eUnpacked;
        opt.WorldToCameraView = Matrix4x4.Identity;
        opt.CameraViewToWorld = Matrix4x4.Identity;
        opt.AlphaUpscalingEnabled = Boolean.eFalse;
        opt.DlaaPreset = DLSSDPreset.eDefault;
        opt.QualityPreset = DLSSDPreset.eDefault;
        opt.BalancedPreset = DLSSDPreset.eDefault;
        opt.PerformancePreset = DLSSDPreset.eDefault;
        opt.UltraPerformancePreset = DLSSDPreset.eDefault;
        opt.UltraQualityPreset = DLSSDPreset.eDefault;
        return opt;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DLSSOptions
{
    public BaseStructure Base;
    public DLSSMode Mode;
    public uint OutputWidth;
    public uint OutputHeight;
    public float Sharpness;
    public float PreExposure;
    public float ExposureScale;
    public Boolean ColorBuffersHDR;
    public Boolean IndicatorInvertAxisX;
    public Boolean IndicatorInvertAxisY;
    private byte pad0; // 1 byte padding to align to 4-byte boundary
    public DLSSPreset DlaaPreset;
    public DLSSPreset QualityPreset;
    public DLSSPreset BalancedPreset;
    public DLSSPreset PerformancePreset;
    public DLSSPreset UltraPerformancePreset;
    public DLSSPreset UltraQualityPreset;
    public Boolean UseAutoExposure;
    public Boolean AlphaUpscalingEnabled;
    private byte pad1; // 1 byte padding
    private byte pad2; // 1 byte padding

    public static DLSSOptions Create()
    {
        var opt = new DLSSOptions();
        opt.Base = new BaseStructure(new StructType(0x6ac826e4, 0x4c61, 0x4101, new byte[] { 0xa9, 0x2d, 0x63, 0x8d, 0x42, 0x10, 0x57, 0xb8 }), 3);
        opt.Mode = DLSSMode.eOff;
        opt.OutputWidth = uint.MaxValue;
        opt.OutputHeight = uint.MaxValue;
        opt.Sharpness = 0.0f;
        opt.PreExposure = 1.0f;
        opt.ExposureScale = 1.0f;
        opt.ColorBuffersHDR = Boolean.eTrue;
        opt.IndicatorInvertAxisX = Boolean.eFalse;
        opt.IndicatorInvertAxisY = Boolean.eFalse;
        opt.DlaaPreset = DLSSPreset.eDefault;
        opt.QualityPreset = DLSSPreset.eDefault;
        opt.BalancedPreset = DLSSPreset.eDefault;
        opt.PerformancePreset = DLSSPreset.eDefault;
        opt.UltraPerformancePreset = DLSSPreset.eDefault;
        opt.UltraQualityPreset = DLSSPreset.eDefault;
        opt.UseAutoExposure = Boolean.eFalse;
        opt.AlphaUpscalingEnabled = Boolean.eFalse;
        return opt;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DLSSOptimalSettings
{
    public BaseStructure Base;
    public uint OptimalRenderWidth;
    public uint OptimalRenderHeight;
    public float OptimalSharpness;
    public uint RenderWidthMin;
    public uint RenderHeightMin;
    public uint RenderWidthMax;
    public uint RenderHeightMax;

    public static DLSSOptimalSettings Create()
    {
        var set = new DLSSOptimalSettings();
        set.Base = new BaseStructure(new StructType(0xef1d0957, 0xfd58, 0x4df7, new byte[] { 0xb5, 0x04, 0x8b, 0x69, 0xd8, 0xaa, 0x6b, 0x76 }), 1);
        return set;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DLSSState
{
    public BaseStructure Base;
    public ulong EstimatedVRAMUsageInBytes;

    public static DLSSState Create()
    {
        var state = new DLSSState();
        state.Base = new BaseStructure(new StructType(0x9366b056, 0x8c01, 0x463c, new byte[] { 0xbb, 0x91, 0xe6, 0x87, 0x82, 0x63, 0x6c, 0xe9 }), 1);
        return state;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DLSSDOptimalSettings
{
    public BaseStructure Base;
    public uint OptimalRenderWidth;
    public uint OptimalRenderHeight;
    public float OptimalSharpness;
    public uint RenderWidthMin;
    public uint RenderHeightMin;
    public uint RenderWidthMax;
    public uint RenderHeightMax;

    public static DLSSDOptimalSettings Create()
    {
        var set = new DLSSDOptimalSettings();
        set.Base = new BaseStructure(new StructType(0xfbd0c637, 0xa28f, 0x41f2, new byte[] { 0xbc, 0x91, 0xb4, 0x21, 0xfa, 0xee, 0x8e, 0x1e }), 1);
        return set;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DLSSDState
{
    public BaseStructure Base;
    public ulong EstimatedVRAMUsageInBytes;

    public static DLSSDState Create()
    {
        var state = new DLSSDState();
        state.Base = new BaseStructure(new StructType(0x71873c14, 0xf8ca, 0x4767, new byte[] { 0x9e, 0xaf, 0x3b, 0x43, 0x93, 0xea, 0x98, 0xfa }), 1);
        return state;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Constants
{
    public BaseStructure Base;
    public Matrix4x4 CameraViewToClip;
    public Matrix4x4 ClipToCameraView;
    public Matrix4x4 ClipToLensClip;
    public Matrix4x4 ClipToPrevClip;
    public Matrix4x4 PrevClipToClip;
    public Vector2 JitterOffset;
    public Vector2 MvecScale;
    public Vector2 CameraPinholeOffset;
    public Vector3 CameraPos;
    public Vector3 CameraUp;
    public Vector3 CameraRight;
    public Vector3 CameraFwd;
    public float CameraNear;
    public float CameraFar;
    public float CameraFOV;
    public float CameraAspectRatio;
    public float MotionVectorsInvalidValue;
    public Boolean DepthInverted;
    public Boolean CameraMotionIncluded;
    public Boolean MotionVectors3D;
    public Boolean Reset;
    public Boolean OrthographicProjection;
    public Boolean MotionVectorsDilated;
    public Boolean MotionVectorsJittered;
    private byte pad0; // 1 byte padding to align to 4-byte boundaries
    public float MinRelativeLinearDepthObjectSeparation;

    public static Constants Create()
    {
        var c = new Constants();
        c.Base = new BaseStructure(new StructType(0xdcd35ad7, 0x4e4a, 0x4bad, new byte[] { 0xa9, 0x0c, 0xe0, 0xc4, 0x9e, 0xb2, 0x3a, 0xfe }), 2);
        c.CameraViewToClip = Matrix4x4.Identity;
        c.ClipToCameraView = Matrix4x4.Identity;
        c.ClipToLensClip = Matrix4x4.Identity;
        c.ClipToPrevClip = Matrix4x4.Identity;
        c.PrevClipToClip = Matrix4x4.Identity;
        c.JitterOffset = Vector2.Zero;
        c.MvecScale = Vector2.One;
        c.CameraPinholeOffset = Vector2.Zero;
        c.CameraPos = Vector3.Zero;
        c.CameraUp = Vector3.Zero;
        c.CameraRight = Vector3.Zero;
        c.CameraFwd = Vector3.Zero;
        c.CameraNear = float.MaxValue;
        c.CameraFar = float.MaxValue;
        c.CameraFOV = float.MaxValue;
        c.CameraAspectRatio = float.MaxValue;
        c.MotionVectorsInvalidValue = float.MaxValue;
        c.DepthInverted = Boolean.eInvalid;
        c.CameraMotionIncluded = Boolean.eInvalid;
        c.MotionVectors3D = Boolean.eInvalid;
        c.Reset = Boolean.eInvalid;
        c.OrthographicProjection = Boolean.eFalse;
        c.MotionVectorsDilated = Boolean.eFalse;
        c.MotionVectorsJittered = Boolean.eFalse;
        c.MinRelativeLinearDepthObjectSeparation = 40.0f;
        return c;
    }
}

public enum PCLMarker : uint
{
    eSimulationStart = 0,
    eSimulationEnd = 1,
    eRenderSubmitStart = 2,
    eRenderSubmitEnd = 3,
    ePresentStart = 4,
    ePresentEnd = 5,
    eTriggerFlash = 7,
    ePCLatencyPing = 8,
    eOutOfBandRenderSubmitStart = 9,
    eOutOfBandRenderSubmitEnd = 10,
    eOutOfBandPresentStart = 11,
    eOutOfBandPresentEnd = 12,
    eControllerInputSample = 13,
    eDeltaTCalculation = 14,
    eLateWarpPresentStart = 15,
    eLateWarpPresentEnd = 16,
    eCameraConstructed = 17,
    eLateWarpRenderSubmitStart = 18,
    eLateWarpRenderSubmitEnd = 19,
    eVendorInternalAsyncPresentStart = 20,
    eVendorInternalAsyncPresentEnd = 21,
    eNumPresentsInBatch = 22,
    eMaximum
}

public enum ReflexMode : uint
{
    eOff = 0,
    eLowLatency = 1,
    eLowLatencyWithBoost = 2
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ReflexOptions
{
    public BaseStructure Base;
    public uint Mode; // ReflexMode
    public uint FrameLimitUs;
    public byte UseMarkersToOptimize; // bool is 1 byte in C++
    private byte pad0; // 1 byte padding
    public ushort VirtualKey;
    public uint IdThread;

    public static ReflexOptions Create()
    {
        var opt = new ReflexOptions();
        opt.Base = new BaseStructure(new StructType(0xf03af81a, 0x6d0b, 0x4902, new byte[] { 0xa6, 0x51, 0xc4, 0x96, 0x5e, 0x21, 0x54, 0x34 }), 1);
        opt.Mode = 0; // eOff
        opt.FrameLimitUs = 0;
        opt.UseMarkersToOptimize = 0;
        opt.VirtualKey = 0;
        opt.IdThread = 0;
        return opt;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ReflexCameraData
{
    public BaseStructure Base;
    public Matrix4x4 WorldToViewMatrix;
    public Matrix4x4 ViewToClipMatrix;
    public Matrix4x4 PrevRenderedWorldToViewMatrix;
    public Matrix4x4 PrevRenderedViewToClipMatrix;

    public static ReflexCameraData Create()
    {
        var d = new ReflexCameraData();
        d.Base = new BaseStructure(new StructType(0xc83cbb02, 0xb4e2, 0x4260, new byte[] { 0x9c, 0xa2, 0xd0, 0xc3, 0xde, 0x3a, 0x96, 0x84 }), 1);
        d.WorldToViewMatrix = Matrix4x4.Identity;
        d.ViewToClipMatrix = Matrix4x4.Identity;
        d.PrevRenderedWorldToViewMatrix = Matrix4x4.Identity;
        d.PrevRenderedViewToClipMatrix = Matrix4x4.Identity;
        return d;
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ReflexPredictedCameraData
{
    public BaseStructure Base;
    public Matrix4x4 PredictedWorldToViewMatrix;
    public Matrix4x4 PredictedViewToClipMatrix;

    public static ReflexPredictedCameraData Create()
    {
        var d = new ReflexPredictedCameraData();
        d.Base = new BaseStructure(new StructType(0x8b960090, 0xa807, 0x4c85, new byte[] { 0xb0, 0x2f, 0x10, 0x69, 0x95, 0x0d, 0x06, 0x6c }), 1);
        d.PredictedWorldToViewMatrix = Matrix4x4.Identity;
        d.PredictedViewToClipMatrix = Matrix4x4.Identity;
        return d;
    }
}

public static unsafe partial class StreamlineAPI
{
    private const string DllName = "sl.interposer.dll";

    [LibraryImport(DllName)]
    public static partial int slInit(Preferences* pref, ulong sdkVersion);

    [LibraryImport(DllName)]
    public static partial int slShutdown();

    [LibraryImport(DllName)]
    public static partial int slSetVulkanInfo(VulkanInfo* info);

    [LibraryImport(DllName)]
    public static partial int slIsFeatureSupported(uint feature, AdapterInfo* adapterInfo);

    [LibraryImport(DllName)]
    public static partial int slSetTagForFrame(FrameToken* frame, ViewportHandle* viewport, ResourceTag* tags, uint numTags, void* cmdBuffer);

    [LibraryImport(DllName)]
    public static partial int slSetConstants(Constants* values, FrameToken* frame, ViewportHandle* viewport);

    [LibraryImport(DllName)]
    public static partial int slEvaluateFeature(uint feature, FrameToken* frame, void** inputs, uint numInputs, void* cmdBuffer);

    [LibraryImport(DllName)]
    public static partial int slAllocateResources(void* cmdBuffer, uint feature, ViewportHandle* viewport);

    [LibraryImport(DllName)]
    public static partial int slFreeResources(uint feature, ViewportHandle* viewport);

    [LibraryImport(DllName)]
    public static partial int slGetNewFrameToken(FrameToken** token, uint* frameIndex);

    [LibraryImport(DllName)]
    public static partial int slGetFeatureFunction(uint feature, byte* functionName, void** function);

    [LibraryImport(DllName, EntryPoint = "vkQueuePresentKHR")]
    public static partial int vkQueuePresentKHR(void* queue, void* presentInfo);

    // Feature loaded function pointers
    public static delegate* unmanaged[Cdecl]<DLSSOptions*, DLSSOptimalSettings*, int> slDLSSGetOptimalSettings;
    public static delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSState*, int> slDLSSGetState;
    public static delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSOptions*, int> slDLSSSetOptions;

    public static delegate* unmanaged[Cdecl]<DLSSDOptions*, DLSSDOptimalSettings*, int> slDLSSDGetOptimalSettings;
    public static delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSDState*, int> slDLSSDGetState;
    public static delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSDOptions*, int> slDLSSDSetOptions;

    public static delegate* unmanaged[Cdecl]<FrameToken*, int> slReflexSleep;
    public static delegate* unmanaged[Cdecl]<ReflexOptions*, int> slReflexSetOptions;
    public static delegate* unmanaged[Cdecl]<ViewportHandle*, FrameToken*, ReflexCameraData*, int> slReflexSetCameraData;
    public static delegate* unmanaged[Cdecl]<ViewportHandle*, FrameToken*, ReflexPredictedCameraData*, int> slReflexGetPredictedCameraData;
    public static delegate* unmanaged[Cdecl]<PCLMarker, FrameToken*, int> slPCLSetMarker;

    public static void LoadDLSSFunctions()
    {
        void* func = null;
        
        fixed (byte* name = "slDLSSGetOptimalSettings\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureDLSS, name, &func);
        slDLSSGetOptimalSettings = (delegate* unmanaged[Cdecl]<DLSSOptions*, DLSSOptimalSettings*, int>)func;

        func = null;
        fixed (byte* name = "slDLSSGetState\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureDLSS, name, &func);
        slDLSSGetState = (delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSState*, int>)func;

        func = null;
        fixed (byte* name = "slDLSSSetOptions\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureDLSS, name, &func);
        slDLSSSetOptions = (delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSOptions*, int>)func;
    }

    public static void LoadDLSSDFunctions()
    {
        void* func = null;
        
        fixed (byte* name = "slDLSSDGetOptimalSettings\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureDLSS_RR, name, &func);
        slDLSSDGetOptimalSettings = (delegate* unmanaged[Cdecl]<DLSSDOptions*, DLSSDOptimalSettings*, int>)func;

        func = null;
        fixed (byte* name = "slDLSSDGetState\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureDLSS_RR, name, &func);
        slDLSSDGetState = (delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSDState*, int>)func;

        func = null;
        fixed (byte* name = "slDLSSDSetOptions\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureDLSS_RR, name, &func);
        slDLSSDSetOptions = (delegate* unmanaged[Cdecl]<ViewportHandle*, DLSSDOptions*, int>)func;
    }

    public static void LoadReflexFunctions()
    {
        void* func = null;
        
        fixed (byte* name = "slReflexSleep\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureReflex, name, &func);
        slReflexSleep = (delegate* unmanaged[Cdecl]<FrameToken*, int>)func;

        func = null;
        fixed (byte* name = "slReflexSetOptions\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureReflex, name, &func);
        slReflexSetOptions = (delegate* unmanaged[Cdecl]<ReflexOptions*, int>)func;

        func = null;
        fixed (byte* name = "slReflexSetCameraData\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureReflex, name, &func);
        slReflexSetCameraData = (delegate* unmanaged[Cdecl]<ViewportHandle*, FrameToken*, ReflexCameraData*, int>)func;

        func = null;
        fixed (byte* name = "slReflexGetPredictedCameraData\0"u8)
            slGetFeatureFunction((uint)Feature.kFeatureReflex, name, &func);
        slReflexGetPredictedCameraData = (delegate* unmanaged[Cdecl]<ViewportHandle*, FrameToken*, ReflexPredictedCameraData*, int>)func;
    }

    public static void LoadPCLFunctions()
    {
        void* func = null;
        
        fixed (byte* name = "slPCLSetMarker\0"u8)
        {
            int res = slGetFeatureFunction((uint)Feature.kFeaturePCL, name, &func);
            LogInfo($"[Streamline] slGetFeatureFunction(slPCLSetMarker) returned: {(Result)res}, pointer: {(IntPtr)func:X}");
        }
        slPCLSetMarker = (delegate* unmanaged[Cdecl]<PCLMarker, FrameToken*, int>)func;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "SetDllDirectoryW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string? lpPathName);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "CreateFileW")]
    private static partial IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("ucrtbase.dll", EntryPoint = "__acrt_iob_func")]
    private static partial IntPtr __acrt_iob_func(uint index);

    [LibraryImport("ucrtbase.dll", StringMarshalling = StringMarshalling.Utf8, EntryPoint = "freopen")]
    private static partial IntPtr freopen(string filename, string mode, IntPtr stream);

    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogInfo(string message) => Console.WriteLine(message);

    [System.Diagnostics.Conditional("DEBUG")]
    private static void CheckDebug(ref bool isDebug) => isDebug = true;

    private static bool IsDebugBuild()
    {
        bool isDebug = false;
        CheckDebug(ref isDebug);
        return isDebug;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void LogCallback(uint type, byte* msg)
    {
        try
        {
            uint minLogLevel = 1u; // Warning и выше
            if (type < minLogLevel) return;

            string message = Marshal.PtrToStringAnsi((IntPtr)msg) ?? "";
            Console.WriteLine($"[Streamline Native Log] Type: {type}, Msg: {message}");
        }
        catch
        {
            // Игнорируем исключения при записи в консоль в unmanaged callback, чтобы не уронить процесс
        }
    }

    private struct SilenceContext
    {
        public IntPtr OriginalStdoutHandle;
        public IntPtr OriginalStderrHandle;
        public IntPtr NulHandle;
        public bool IsRedirected;
        public bool IsCrtRedirected;
    }

    private static SilenceContext BeginSilence()
    {
        var ctx = new SilenceContext
        {
            OriginalStdoutHandle = IntPtr.Zero,
            OriginalStderrHandle = IntPtr.Zero,
            NulHandle = IntPtr.Zero,
            IsRedirected = false,
            IsCrtRedirected = false
        };

        // В Debug-сборке не подавляем вывод, чтобы видеть все системные логи
        if (IsDebugBuild())
        {
            return ctx;
        }

        try
        {
            // Win32 API level redirection
            ctx.OriginalStdoutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            ctx.OriginalStderrHandle = GetStdHandle(STD_ERROR_HANDLE);

            ctx.NulHandle = CreateFile(
                "NUL",
                GENERIC_WRITE,
                FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (ctx.NulHandle != (IntPtr)(-1))
            {
                SetStdHandle(STD_OUTPUT_HANDLE, ctx.NulHandle);
                SetStdHandle(STD_ERROR_HANDLE, ctx.NulHandle);
                ctx.IsRedirected = true;
            }

            // CRT level redirection
            IntPtr stdoutStream = __acrt_iob_func(1);
            IntPtr stderrStream = __acrt_iob_func(2);
            if (stdoutStream != IntPtr.Zero && stderrStream != IntPtr.Zero)
            {
                freopen("NUL", "w", stdoutStream);
                freopen("NUL", "w", stderrStream);
                ctx.IsCrtRedirected = true;
            }
        }
        catch
        {
            // Игнорируем ошибки перенаправления
        }

        return ctx;
    }

    private static void EndSilence(in SilenceContext ctx)
    {
        if (IsDebugBuild())
        {
            return;
        }

        // В Release-сборке мы сознательно НЕ восстанавливаем CRT-вывод в CONOUT$,
        // чтобы избежать ошибок/крэшей в headless-окружениях и гарантировать
        // отсутствие лишних низкоуровневых логов в консоли.

        // Restore Win32 level redirection
        if (ctx.IsRedirected)
        {
            try
            {
                if (ctx.OriginalStdoutHandle != IntPtr.Zero)
                    SetStdHandle(STD_OUTPUT_HANDLE, ctx.OriginalStdoutHandle);
                if (ctx.OriginalStderrHandle != IntPtr.Zero)
                    SetStdHandle(STD_ERROR_HANDLE, ctx.OriginalStderrHandle);

                if (ctx.NulHandle != IntPtr.Zero && ctx.NulHandle != (IntPtr)(-1))
                    CloseHandle(ctx.NulHandle);
            }
            catch
            {
                // Игнорируем ошибки восстановления
            }
        }
    }

    public static void RunSilenced(Action action)
    {
        var ctx = BeginSilence();
        try
        {
            action();
        }
        finally
        {
            EndSilence(ctx);
        }
    }

    public static T RunSilenced<T>(Func<T> func)
    {
        var ctx = BeginSilence();
        try
        {
            return func();
        }
        finally
        {
            EndSilence(ctx);
        }
    }

    public static int EarlyInitStreamline()
    {
        try
        {
            string binariesPath = Path.Combine(AppContext.BaseDirectory, "binaries", "x64");
            SetDllDirectory(binariesPath);
        }
        catch
        {
            // Игнорируем ошибки при настройке путей
        }

        int initRes = -1;
        Exception? initException = null;

        var ctx = BeginSilence();
        try
        {
            var pref = Preferences.Create();
            pref.ShowConsole = 0;
            pref.LogLevel = IsDebugBuild() ? (uint)LogLevel.eDefault : (uint)LogLevel.eOff;
            pref.LogMessageCallback = (delegate* unmanaged[Cdecl]<uint, byte*, void>)&LogCallback;
            
            uint* features = stackalloc uint[4];
            features[0] = (uint)Feature.kFeatureDLSS;
            features[1] = (uint)Feature.kFeatureDLSS_RR;
            features[2] = (uint)Feature.kFeatureReflex;
            features[3] = (uint)Feature.kFeaturePCL;
            pref.FeaturesToLoad = features;
            pref.NumFeaturesToLoad = 4;
            pref.RenderAPI = 2; // eVulkan
            pref.ApplicationId = 0x10DE; // Set dummy Application ID to suppress production warning

            ulong sdkVersion = (2UL << 48) | (11UL << 32) | (1UL << 16) | 0xfedcUL;
            initRes = slInit(&pref, sdkVersion);
        }
        catch (Exception ex)
        {
            initException = ex;
        }
        finally
        {
            EndSilence(ctx);
        }

        if (initException != null)
        {
            Console.WriteLine($"[Streamline] Failed to load or initialize sl.interposer.dll during early init: {initException.Message}");
            return -1;
        }

        if (initRes != (int)Result.eOk)
        {
            Console.WriteLine($"[Streamline] slInit failed: {(Result)initRes}");
        }
        else
        {
            LogInfo($"[Streamline] slInit early initialization succeeded.");
        }

        return initRes;
    }
}

namespace VRCFaceTracking.Core.Library;

public struct ModuleRuntimeInfo
{
    public ExtTrackingModule Module;
    public ASL asl;
    public CancellationTokenSource UpdateCancellationToken;
    public Thread UpdateThread;
}
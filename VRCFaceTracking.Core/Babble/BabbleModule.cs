using Babble.Core;

namespace VRCFaceTracking.BabbleNative;

public class BabbleModule : ExtTrackingModule
{
    private const int TIMEOUT_MS = 100;

    public override (bool SupportsEye, bool SupportsExpression) Supported => (false, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        // Just skip init if we're already started
        if (!BabbleCore.Instance.IsRunning)
            BabbleCore.Instance.Start();
        return Supported;
    }

    public override void Teardown()
    {
        BabbleCore.Instance.Stop();
    }

    public override void Update() 
    {
        if (BabbleCore.Instance.IsRunning)
        {
            if (BabbleCore.Instance.GetExpressionData(out var expressions))
            {
                foreach (var exp in expressions)
                {
                    UnifiedTracking.Data.Shapes[(int)BabbleMapping.Mapping[exp.Key]].Weight = exp.Value;
                }
            }

            if (BabbleCore.Instance.GetImage(out var image, out var dimensions))
            {
                UnifiedTracking.LipImageData.ImageData = image;
                UnifiedTracking.LipImageData.ImageSize = dimensions;
            }
        }

        // Make sure the VRCFT update thread doesn't clog our CPU
        Thread.Sleep(TIMEOUT_MS); 
    }
}

using Babble.Core;

namespace VRCFaceTracking.BabbleNative;

public class BabbleModule : ExtTrackingModule
{
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
        if (!BabbleCore.Instance.IsRunning)
        {
            Thread.Sleep(10);
            return;
        }

        if (BabbleCore.Instance.GetExpressionData(out var expressions))
        {
            foreach (var exp in expressions)
            {
                UnifiedTracking.Data.Shapes[(int)BabbleMapping.Mapping[exp.Key]].Weight = exp.Value;
            }
        }
    }
}

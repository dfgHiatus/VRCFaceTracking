﻿using Babble.Core;

namespace VRCFaceTracking.BabbleNative;

public class BabbleModule : ExtTrackingModule
{
    private CancellationTokenSource _cancellationTokenSource;

    public override (bool SupportsEye, bool SupportsExpression) Supported => (false, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        // Just skip init if we're already started
        if (!BabbleCore.Instance.IsRunning)
            BabbleCore.Instance.Start();
        _cancellationTokenSource = new();
        Task.Run(GetDataAsync);
        return Supported;
    }

    private Task GetDataAsync()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (BabbleCore.Instance.IsRunning)
            {
                if (BabbleCore.Instance.GetExpressionData(out var expressions))
                {
                    foreach (var exp in expressions)
                    {
                        UnifiedTracking.Data.Shapes[(int) BabbleMapping.Mapping[exp.Key]].Weight = exp.Value;
                    }
                }

                if (BabbleCore.Instance.GetImage(out var image, out var dimensions))
                {
                    UnifiedTracking.LipImageData.ImageData = image;
                    UnifiedTracking.LipImageData.ImageSize = dimensions;
                }
            }

            Task.Delay(100);
        }

        return Task.CompletedTask;
    }

    public override void Teardown()
    {
        _cancellationTokenSource.Cancel();
        BabbleCore.Instance.Stop();
        _cancellationTokenSource.Dispose();
    }

    public override void Update() { }
}

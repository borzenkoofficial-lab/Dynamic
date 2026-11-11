using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Dynamic.Core;

public sealed class CapsuleMotionController
{
    private readonly Compositor _compositor = CompositionTarget.GetCompositorForCurrentThread();
    private SpringVector3NaturalMotionAnimation? _scaleSpring;

    public void SetEmphasis(UIElement target, bool emphasized, double intensity = 0.08)
    {
        ArgumentNullException.ThrowIfNull(target);

        _scaleSpring ??= _compositor.CreateSpringVector3Animation();
        _scaleSpring.Target = "Scale";
        _scaleSpring.DampingRatio = 0.82f;
        _scaleSpring.Period = TimeSpan.FromMilliseconds(280);

        var scale = emphasized ? (float)(1.0 + Math.Clamp(intensity, 0, 0.2)) : 1.0f;
        _scaleSpring.FinalValue = new Vector3(scale, scale, 1.0f);
        target.StartAnimation(_scaleSpring);
    }
}
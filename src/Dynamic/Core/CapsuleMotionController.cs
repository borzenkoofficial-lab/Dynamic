using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;

namespace Dynamic.Core;

public sealed class CapsuleMotionController
{
    private readonly Compositor _compositor = CompositionTarget.GetCompositorForCurrentThread();
    private SpringVector3NaturalMotionAnimation? _scaleSpring;
    private SpringVector3NaturalMotionAnimation? _translationSpring;
    private SpringVector3NaturalMotionAnimation? _magneticScaleSpring;

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

    public void SetMagneticPose(UIElement target, Vector3 translation, float scale)
    {
        ArgumentNullException.ThrowIfNull(target);

        _translationSpring ??= _compositor.CreateSpringVector3Animation();
        _translationSpring.Target = "Translation";
        _translationSpring.DampingRatio = 0.78f;
        _translationSpring.Period = TimeSpan.FromMilliseconds(360);
        _translationSpring.FinalValue = translation;
        target.StartAnimation(_translationSpring);

        _magneticScaleSpring ??= _compositor.CreateSpringVector3Animation();
        _magneticScaleSpring.Target = "Scale";
        _magneticScaleSpring.DampingRatio = 0.84f;
        _magneticScaleSpring.Period = TimeSpan.FromMilliseconds(320);
        _magneticScaleSpring.FinalValue = new Vector3(scale, scale, 1.0f);
        target.StartAnimation(_magneticScaleSpring);
    }
}
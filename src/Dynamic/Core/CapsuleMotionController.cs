using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

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
        SetCenterPoint(target);

        _scaleSpring ??= _compositor.CreateSpringVector3Animation();
        _scaleSpring.Target = "Scale";
        _scaleSpring.DampingRatio = 0.82f;
        _scaleSpring.Period = TimeSpan.FromMilliseconds(280);

        var scale = emphasized ? (float)(1.0 + Math.Clamp(intensity, 0, 0.2)) : 1.0f;
        _scaleSpring.FinalValue = new Vector3(scale, scale, 1.0f);
        target.StartAnimation(_scaleSpring);
    }

    public void FadeOpacity(UIElement target, float opacity, int durationMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        var animation = _compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1.0f, Math.Clamp(opacity, 0.0f, 1.0f));
        animation.Duration = TimeSpan.FromMilliseconds(Math.Max(1, durationMilliseconds));
        target.StartAnimation("Opacity", animation);
    }

    public void Reveal(UIElement target, float scale = 1.0f, int durationMilliseconds = 260)
    {
        ArgumentNullException.ThrowIfNull(target);
        SetCenterPoint(target);
        var visualScale = _compositor.CreateSpringVector3Animation();
        visualScale.Target = "Scale";
        visualScale.DampingRatio = 0.82f;
        visualScale.Period = TimeSpan.FromMilliseconds(300);
        visualScale.FinalValue = new Vector3(scale, scale, 1.0f);
        target.Opacity = 0.0;
        target.StartAnimation(visualScale);

        var opacity = _compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(1.0f, 1.0f);
        opacity.Duration = TimeSpan.FromMilliseconds(Math.Max(1, durationMilliseconds));
        target.StartAnimation("Opacity", opacity);
    }

    private static void SetCenterPoint(UIElement target)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        visual.CenterPoint = new Vector3(
            (float)(target.ActualWidth * 0.5),
            (float)(target.ActualHeight * 0.5),
            0.0f);
    }

    public void SetMagneticPose(UIElement target, Vector3 translation, float scale)
    {
        ArgumentNullException.ThrowIfNull(target);
        SetCenterPoint(target);

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
using System.Numerics;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Dynamic.Core;

public sealed class CapsuleMotionController
{
    private Compositor? _compositor;
    private SpringVector3NaturalMotionAnimation? _translationSpring;
    private SpringVector3NaturalMotionAnimation? _magneticScaleSpring;
    private AmbientLight? _ambientLight;
    private PointLight? _pointerLight;
    private Visual? _lightingVisual;

    private Visual GetVisual(FrameworkElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        _compositor ??= visual.Compositor;
        return visual;
    }

    public void AttachDynamicLighting(FrameworkElement host)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            var visual = GetVisual(host);
            _lightingVisual = visual;

            _ambientLight = _compositor!.CreateAmbientLight();
            _ambientLight.Color = Color.FromArgb(255, 48, 52, 62);
            _ambientLight.Targets.Add(visual);

            _pointerLight = _compositor.CreatePointLight();
            _pointerLight.Color = Color.FromArgb(255, 190, 255, 215);
            _pointerLight.CoordinateSpace = visual;
            _pointerLight.Offset = new Vector3((float)(host.ActualWidth * 0.5), 0, 150);
            _pointerLight.Intensity = 0.55f;
            _pointerLight.ConstantAttenuation = 1.0f;
            _pointerLight.LinearAttenuation = 0.0018f;
            _pointerLight.QuadraticAttenuation = 0.000004f;
            _pointerLight.MaxAttenuationCutoff = 520.0f;
            _pointerLight.Targets.Add(visual);
        }
        catch (Exception)
        {
            DisposeLighting();
        }
    }

    public void UpdateDynamicLight(Vector2 position)
    {
        if (_pointerLight is null) return;
        _pointerLight.Offset = new Vector3(position, 150.0f);
    }

    public void DisposeLighting()
    {
        if (_lightingVisual is not null)
        {
            _pointerLight?.Targets.Remove(_lightingVisual);
            _ambientLight?.Targets.Remove(_lightingVisual);
        }

        _pointerLight = null;
        _ambientLight = null;
        _lightingVisual = null;
    }

    public void SetEmphasis(FrameworkElement target, bool emphasized, double intensity = 0.08)
    {
        ArgumentNullException.ThrowIfNull(target);
        var visual = GetVisual(target);
        visual.CenterPoint = new Vector3((float)(target.ActualWidth * 0.5), (float)(target.ActualHeight * 0.5), 0);

        var scaleSpring = _compositor!.CreateSpringVector3Animation();
        scaleSpring.DampingRatio = 0.82f;
        scaleSpring.Period = TimeSpan.FromMilliseconds(280);

        var scale = emphasized ? (float)(1.0 + Math.Clamp(intensity, 0, 0.2)) : 1.0f;
        scaleSpring.FinalValue = new Vector3(scale, scale, 1.0f);
        visual.StartAnimation("Scale", scaleSpring);
    }

    public void FadeOpacity(FrameworkElement target, float opacity, int durationMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        var visual = GetVisual(target);
        var animation = _compositor!.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1.0f, Math.Clamp(opacity, 0.0f, 1.0f));
        animation.Duration = TimeSpan.FromMilliseconds(Math.Max(1, durationMilliseconds));
        visual.StartAnimation("Opacity", animation);
    }

    public void Reveal(FrameworkElement target, float scale = 1.0f, int durationMilliseconds = 260)
    {
        ArgumentNullException.ThrowIfNull(target);
        var visual = GetVisual(target);
        visual.CenterPoint = new Vector3((float)(target.ActualWidth * 0.5), (float)(target.ActualHeight * 0.5), 0);
        visual.Scale = new Vector3(scale * 0.965f, scale * 0.965f, 1.0f);

        var visualScale = _compositor!.CreateSpringVector3Animation();
        visualScale.DampingRatio = 0.82f;
        visualScale.Period = TimeSpan.FromMilliseconds(300);
        visualScale.FinalValue = new Vector3(scale, scale, 1.0f);
        visual.StartAnimation("Scale", visualScale);

        var opacity = _compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(1.0f, 1.0f);
        opacity.Duration = TimeSpan.FromMilliseconds(Math.Max(1, durationMilliseconds));
        visual.StartAnimation("Opacity", opacity);
    }

    public void SetVisualizer(FrameworkElement[] bars, bool active)
    {
        ArgumentNullException.ThrowIfNull(bars);
        for (var index = 0; index < bars.Length; index++)
        {
            var bar = bars[index];
            var visual = GetVisual(bar);
            if (!active)
            {
                visual.StopAnimation("Scale.Y");
                visual.Scale = Vector3.One;
                continue;
            }

            visual.CenterPoint = new Vector3((float)(bar.ActualWidth * 0.5), (float)(bar.ActualHeight * 0.5), 0);
            var animation = _compositor!.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0.0f, 0.35f + (index % 2) * 0.12f);
            animation.InsertKeyFrame(0.28f, 0.75f + (index % 3) * 0.24f);
            animation.InsertKeyFrame(0.58f, 1.35f - (index % 2) * 0.3f);
            animation.InsertKeyFrame(1.0f, 0.4f + (index % 3) * 0.16f);
            animation.Duration = TimeSpan.FromMilliseconds(420 + index * 65);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            visual.StartAnimation("Scale.Y", animation);
        }
    }

    public void SetMagneticPose(FrameworkElement target, Vector3 translation, float scale)
    {
        ArgumentNullException.ThrowIfNull(target);
        var visual = GetVisual(target);
        visual.CenterPoint = new Vector3((float)(target.ActualWidth * 0.5), (float)(target.ActualHeight * 0.5), 0);

        _translationSpring ??= _compositor!.CreateSpringVector3Animation();
        _translationSpring.DampingRatio = 0.78f;
        _translationSpring.Period = TimeSpan.FromMilliseconds(360);
        _translationSpring.FinalValue = translation;
        visual.StartAnimation("Translation", _translationSpring);

        _magneticScaleSpring ??= _compositor!.CreateSpringVector3Animation();
        _magneticScaleSpring.DampingRatio = 0.84f;
        _magneticScaleSpring.Period = TimeSpan.FromMilliseconds(320);
        _magneticScaleSpring.FinalValue = new Vector3(scale, scale, 1.0f);
        visual.StartAnimation("Scale", _magneticScaleSpring);
    }
}

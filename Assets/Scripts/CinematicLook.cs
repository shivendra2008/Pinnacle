using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum LookPreset { Apartment, Market }

/// <summary>
/// The "film look": a full URP post-processing stack (ACES tonemapping, HQ bloom, vignette, split-toned colour grade,
/// bokeh depth of field with auto-focus, camera motion blur, grain, chromatic aberration), plus helpers for
/// soft shadows, ambient light and a real-time reflection probe. SSAO lives in the PC renderer asset.
/// </summary>
public static class CinematicLook
{
    public static Volume Apply(Transform parent, Camera cam, LookPreset preset)
    {
        // camera
        var cd = cam.GetUniversalAdditionalCameraData();
        cd.renderPostProcessing = true;
        cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cd.antialiasingQuality = AntialiasingQuality.High;
        cd.renderShadows = true;
        cam.allowHDR = true;
        cam.allowMSAA = true;

        var go = new GameObject("FilmLook_" + preset);
        go.transform.SetParent(parent, false);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 50;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.sharedProfile = prof;
        bool apt = preset == LookPreset.Apartment;

        var tm = prof.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.ACES);

        var bloom = prof.Add<Bloom>(true);
        bloom.threshold.Override(apt ? 1.1f : 0.95f);
        bloom.intensity.Override(apt ? 0.4f : 0.7f);
        bloom.scatter.Override(apt ? 0.62f : 0.72f);
        bloom.tint.Override(apt ? new Color(1f, 0.88f, 0.7f) : new Color(0.8f, 0.85f, 1f));
        bloom.highQualityFiltering.Override(true);

        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(apt ? 0.27f : 0.24f);
        vig.smoothness.Override(0.5f);
        vig.rounded.Override(false);

        var ca = prof.Add<ColorAdjustments>(true);
        ca.postExposure.Override(apt ? 0.92f : 1.28f);     // lifted to offset the stronger contrast curve
        ca.contrast.Override(apt ? 16f : 18f);
        ca.saturation.Override(apt ? -16f : -8f);         // muted, filmic colour: the single biggest step away from the "cartoon" look
        ca.colorFilter.Override(apt ? new Color(1f, 0.96f, 0.9f) : new Color(0.92f, 0.95f, 1f));

        var wb = prof.Add<WhiteBalance>(true);
        wb.temperature.Override(apt ? 12f : -9f);
        wb.tint.Override(apt ? 3f : 6f);

        var smh = prof.Add<ShadowsMidtonesHighlights>(true);     // teal shadows, warm highlights
        smh.shadows.Override(apt ? new Vector4(0.88f, 0.98f, 1.08f, -0.05f) : new Vector4(0.84f, 0.95f, 1.12f, -0.05f));
        smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        smh.highlights.Override(apt ? new Vector4(1.08f, 1.0f, 0.9f, 0.02f) : new Vector4(1.1f, 0.96f, 0.92f, 0.02f));

        var dof = prof.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(4f);
        dof.focalLength.Override(50f);
        dof.aperture.Override(apt ? 3.4f : 3.0f);
        dof.bladeCount.Override(7);

        var mb = prof.Add<MotionBlur>(true);
        mb.mode.Override(MotionBlurMode.CameraOnly);
        mb.quality.Override(MotionBlurQuality.Medium);
        mb.intensity.Override(0.28f);
        mb.clamp.Override(0.06f);

        var chroma = prof.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.1f);

        var lens = prof.Add<LensDistortion>(true);          // a touch of anamorphic barrel, like a real lens
        lens.intensity.Override(-0.07f);
        lens.scale.Override(1.02f);

        var grain = prof.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium1);
        grain.intensity.Override(apt ? 0.24f : 0.28f);
        grain.response.Override(0.8f);

        // auto-focus follows what you look at (or the person you are talking to)
        var af = cam.gameObject.GetComponent<AutoFocus>();
        if (af == null) af = cam.gameObject.AddComponent<AutoFocus>();
        af.dof = dof;
        af.defaultDistance = apt ? 4f : 8f;

        var ctrl = go.AddComponent<LookController>();
        ctrl.profile = prof;
        return vol;
    }

    /// <summary>Soft, well-biased shadows for a light.</summary>
    public static void Shadows(Light l, float strength = 1f, float bias = 0.03f, float normalBias = 0.6f, LightShadows type = LightShadows.Soft)
    {
        l.shadows = type;
        l.shadowStrength = strength;
        l.shadowBias = bias;
        l.shadowNormalBias = normalBias;
        l.shadowResolution = LightShadowResolution.High;
    }

    /// <summary>Gradient (trilight) ambient: cheap fake global illumination that SSAO then grounds.</summary>
    public static void Ambient(Color sky, Color equator, Color ground, float reflection = 1f)
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = ground;
        RenderSettings.reflectionIntensity = reflection;
    }

    public static ReflectionProbe Probe(Transform parent, Vector3 localPos, Vector3 size, Color background, int resolution = 128)
    {
        var go = new GameObject("ReflectionProbe");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Realtime;
        p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        p.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        p.size = size;
        p.resolution = resolution;
        p.boxProjection = true;
        p.intensity = 0.6f;
        p.clearFlags = ReflectionProbeClearFlags.SolidColor;   // never reflect the default blue sky
        p.backgroundColor = background;
        var c = go.AddComponent<LookController>();   // refreshes the probe a moment after the scene is built
        c.probe = p;
        return p;
    }
}

/// <summary>Moves the depth-of-field focus to whatever is in front of the camera.</summary>
public class AutoFocus : MonoBehaviour
{
    public DepthOfField dof;
    public float defaultDistance = 5f;
    public static Transform FocusOn;       // set during conversations so the speaker is always sharp
    float current = 4f;

    void Update()
    {
        if (dof == null) return;
        float target = defaultDistance;
        if (FocusOn != null)
            target = Vector3.Distance(transform.position, FocusOn.position + Vector3.up * 1.5f);
        else if (Physics.Raycast(transform.position, transform.forward, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            target = hit.distance;
        current = Mathf.Lerp(current, Mathf.Max(0.4f, target), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        dof.focusDistance.value = current;
    }

    void OnDestroy() { if (FocusOn != null) FocusOn = null; }
}

/// <summary>F2 cycles Cinematic / Balanced / Performance; also re-renders the reflection probe.</summary>
public class LookController : MonoBehaviour
{
    public VolumeProfile profile;
    public ReflectionProbe probe;
    static int tier = 0;               // default: Cinematic (F2 to switch); survives restarts
    float shownUntil;
    GUIStyle style;

    void Start()
    {
        if (profile != null) ApplyTier();
        if (probe != null) { probe.RenderProbe(); Invoke(nameof(Refresh), 0.6f); Invoke(nameof(Refresh), 3f); }
    }

    void Refresh() { if (probe != null) probe.RenderProbe(); }

    void Update()
    {
        if (profile == null) return;
        var kb = Keyboard.current;
        if (kb != null && kb.f2Key.wasPressedThisFrame)
        {
            tier = (tier + 1) % 3;
            ApplyTier();
            shownUntil = Time.unscaledTime + 2.5f;
        }
    }

    void ApplyTier()
    {
        // 0 = Cinematic (everything), 1 = Balanced (no DOF / motion blur / aberration), 2 = Performance (also no grain, cheap bloom)
        Set<DepthOfField>(tier == 0);
        Set<MotionBlur>(tier == 0);
        Set<ChromaticAberration>(tier == 0);
        Set<LensDistortion>(tier == 0);
        Set<FilmGrain>(tier <= 1);
        if (profile.TryGet(out Bloom b)) b.highQualityFiltering.value = tier <= 1;
    }

    void Set<T>(bool on) where T : VolumeComponent
    {
        if (profile.TryGet(out T c)) c.active = on;
    }

    void OnGUI()
    {
        if (profile == null || Time.unscaledTime > shownUntil) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = UiKit.Gold;
        }
        string[] names = { "CINEMATIC", "BALANCED", "PERFORMANCE" };
        var r = new Rect(Screen.width / 2f - 170, 70, 340, 42);
        UiKit.Card(r, 0.8f);
        GUI.Label(r, "Graphics: " + names[tier] + "   (F2)", style);
    }
}

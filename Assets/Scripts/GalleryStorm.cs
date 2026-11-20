using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The storm outside the Curator's Gallery: rain beyond the windows and on the glass, and lightning that comes in
/// through the tall east window (and, dimmer, the high windows behind the bookcase) as a cold, shadow-casting light,
/// followed by thunder after the time sound takes to cover the distance (about 3 s per km).
///
/// It is the ONLY writer of: the lightning lights, the moonlight spots (their level is `moonLevel`, set by the
/// scenes), the glass and sky materials' flash, and the outdoor rain's brightness. Candles belong to CandleFlicker;
/// exposure belongs to GalleryExposure, which ignores the lightning (a flash must read as a flash, not be
/// compensated away).
///
/// A flash is a short burst of strokes (a main stroke, one or two restrikes, a faint afterglow), never a strobe:
/// strikes are scheduled by the scenes on dramatic beats, plus rare distant ones (25-70 s apart) when autoStrikes is on.
/// </summary>
public class GalleryStorm : MonoBehaviour
{
    public static GalleryStorm Active;

    public Light mainFlash;                                   // outside the east window, casts the window's shadow into the hall
    public readonly List<Light> backFlash = new List<Light>(); // outside the high windows behind the bookcase
    public readonly List<Light> moon = new List<Light>(); readonly List<float> moonBase = new List<float>();
    public readonly List<Material> glass = new List<Material>();
    public readonly List<Material> skies = new List<Material>();
    public readonly List<ParticleSystemRenderer> rain = new List<ParticleSystemRenderer>();
    public float mainPeak = 14f, backPeak = 5f;
    public float moonLevel = 1f;
    public bool autoStrikes = true;
    public float Flash { get; private set; }
    /// <summary>Called at a strike with its distance (km); the sound side plays the thunder after the delay.</summary>
    public event Action<float, float> Struck;
    public event Action<float, float> Thunder;     // (distance km, strength) when the thunder arrives

    struct Pulse { public float t0, peak, rise, decay; }
    readonly List<Pulse> pulses = new List<Pulse>();
    struct Pending { public float at, km, strength; }
    readonly List<Pending> thunder = new List<Pending>();
    float nextAuto;
    static readonly Color SkyTop = new Color(0.030f, 0.038f, 0.060f), SkyBottom = new Color(0.055f, 0.060f, 0.075f);

    public void AddMoon(Light l) { moon.Add(l); moonBase.Add(l.intensity); }
    readonly List<Color> skyBase = new List<Color>();
    /// <summary>A sky surface (unlit) that lights up with the flashes; `baseColor` is its level between them.</summary>
    public void AddSky(Material m, Color baseColor) { skies.Add(m); skyBase.Add(baseColor); m.SetColor("_BaseColor", baseColor); }

    void OnEnable() { Active = this; nextAuto = Time.time + UnityEngine.Random.Range(18f, 30f); }
    void OnDisable() { if (Active == this) Active = null; }

    /// <summary>A lightning strike now, `km` away (0.5 close .. 5 distant), strength 0..1.</summary>
    public void Strike(float km, float strength = 1f)
    {
        km = Mathf.Clamp(km, 0.3f, 8f);
        float s = Mathf.Clamp01(strength) * Mathf.Clamp01(1.3f / (0.6f + km));         // farther is dimmer
        float t = Time.time;
        var rnd = new System.Random(Mathf.RoundToInt(t * 1000f));
        float R() { return (float)rnd.NextDouble(); }
        pulses.Add(new Pulse { t0 = t, peak = s, rise = 0.02f, decay = 0.11f + 0.06f * R() });
        float tt = t + 0.12f + 0.1f * R();
        pulses.Add(new Pulse { t0 = tt, peak = s * (0.6f + 0.3f * R()), rise = 0.02f, decay = 0.09f + 0.06f * R() });
        if (R() < 0.7f) pulses.Add(new Pulse { t0 = tt + 0.16f + 0.18f * R(), peak = s * (0.3f + 0.25f * R()), rise = 0.025f, decay = 0.12f });
        pulses.Add(new Pulse { t0 = t + 0.02f, peak = s * 0.07f, rise = 0.05f, decay = 0.8f });       // afterglow in the clouds
        thunder.Add(new Pending { at = t + km * 1000f / 343f, km = km, strength = Mathf.Clamp01(strength) });
        if (Struck != null) Struck(km, strength);
        nextAuto = Mathf.Max(nextAuto, t + 25f);           // never two in quick succession
    }

    void Update()
    {
        float t = Time.time;
        if (Time.deltaTime <= 0f) return;                   // paused: hold the frame as it is
        if (autoStrikes && t > nextAuto)
        {
            nextAuto = t + UnityEngine.Random.Range(28f, 70f);
            Strike(UnityEngine.Random.Range(2.5f, 6f), UnityEngine.Random.Range(0.4f, 0.8f));      // far away: dim, late, low thunder
        }
        // flash level from the strokes
        float f = 0f;
        for (int i = pulses.Count - 1; i >= 0; i--)
        {
            var p = pulses[i];
            float u = t - p.t0;
            if (u < 0f) continue;
            if (u > p.rise + p.decay * 7f) { pulses.RemoveAt(i); continue; }
            f += u < p.rise ? p.peak * (u / p.rise) : p.peak * Mathf.Exp(-(u - p.rise) / p.decay);
        }
        Flash = f;
        for (int i = thunder.Count - 1; i >= 0; i--)
            if (t >= thunder[i].at) { var th = thunder[i]; thunder.RemoveAt(i); if (Thunder != null) Thunder(th.km, th.strength); }

        // lights: off between flashes (no cost), shadows only from the main window
        if (mainFlash != null) { mainFlash.intensity = mainPeak * f; mainFlash.enabled = f > 0.004f; }
        foreach (var l in backFlash) if (l != null) { l.intensity = backPeak * f; l.enabled = f > 0.004f; }
        for (int i = 0; i < moon.Count; i++) if (moon[i] != null) { moon[i].intensity = moonBase[i] * moonLevel; moon[i].enabled = moonLevel > 0.01f; }
        foreach (var m in glass) if (m != null) m.SetFloat("_Flash", f * 3.5f);
        for (int i = 0; i < skies.Count; i++)
            if (skies[i] != null) skies[i].SetColor("_BaseColor", (i < skyBase.Count ? skyBase[i] : Color.Lerp(SkyBottom, SkyTop, 0.5f)) * (1f + f * 18f));
        foreach (var r in rain) if (r != null && r.sharedMaterial != null) r.sharedMaterial.SetColor("_BaseColor", new Color(0.55f, 0.6f, 0.7f, 0.16f + 0.5f * Mathf.Clamp01(f)));
    }
}

/// <summary>
/// The Gallery's exposure, like a camera operator riding the iris: it meters the light falling on the subject (the
/// Curator's face, or the player's eye in Act V) from the lamps and candles that are lit, and sets the post-exposure
/// so skin sits at a readable level. Slow (about a second), limited to two stops either way, and blind to lightning.
/// It is the only writer of the Gallery profile's postExposure.
/// </summary>
public class GalleryExposure : MonoBehaviour
{
    public ColorAdjustments adjust;
    public Func<Vector3> subject;
    public float target = 0.85f;           // illuminance on the face that maps to "normal" (tuned on the face renders)
    public float bias = 0f;                // scenes can darken or lift a moment on purpose
    public float smoothing = 1.1f;
    public float Exposure { get; private set; }
    public float Metered { get; private set; }
    readonly List<Light> lights = new List<Light>();
    float rescan;

    void LateUpdate()
    {
        if (adjust == null || subject == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (Time.time > rescan)
        {
            rescan = Time.time + 1f;
            lights.Clear();
            foreach (var l in FindObjectsByType<Light>()) lights.Add(l);
        }
        Vector3 p = subject();
        var storm = GalleryStorm.Active;
        float e = 0f;
        foreach (var l in lights)
        {
            if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) continue;
            if (storm != null && (l == storm.mainFlash || storm.backFlash.Contains(l))) continue;
            if (l.type == LightType.Directional) { e += l.intensity * 0.6f; continue; }
            Vector3 d = p - l.transform.position; float d2 = Mathf.Max(0.04f, d.sqrMagnitude);
            float r2 = l.range * l.range; if (d2 > r2) continue;
            float win = 1f - (d2 / r2) * (d2 / r2); win *= win;                 // URP's range window
            float att = win / d2;
            if (l.type == LightType.Spot)
            {
                float c = Vector3.Dot(l.transform.forward, d.normalized);
                float co = Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad), ci = Mathf.Cos(l.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
                att *= Mathf.Clamp01((c - co) / Mathf.Max(1e-3f, ci - co));
            }
            e += l.intensity * att * 0.6f;                                      // ~ an average cosine on a face
        }
        Color amb = RenderSettings.ambientSkyColor * 0.4f + RenderSettings.ambientEquatorColor * 0.5f + RenderSettings.ambientGroundColor * 0.1f;
        e += amb.grayscale * 1.5f;
        Metered = e;
        float want = Mathf.Clamp(Mathf.Log(target / Mathf.Max(0.02f, e), 2f), -0.7f, 1.5f) + bias;
        Exposure = Mathf.Lerp(Exposure, want, 1f - Mathf.Exp(-dt / smoothing));
        adjust.postExposure.value = Exposure;
    }
}

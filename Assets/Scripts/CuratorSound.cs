using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sound for the Curator's Gallery. Every sound has a cause: footsteps are raised by the feet landing
/// (CharacterNPC.FootContact), page and cover sounds by the hand performance, thunder by the storm after the delay
/// sound needs to arrive. Nothing runs on a timer.
///
/// Recordings live in Resources/Audio/Curator/&lt;category&gt;/ (see CREDITS.txt there). Several takes per category
/// are picked at random without repeating the last one, with a little pitch and level variation, so nothing
/// sounds like a loop. A category with no recordings falls back to the old synthesised placeholder, and the
/// missing categories are listed once in the log (they are placeholders, not finished sound).
///
/// Mix: dialogue is the priority (the beds duck about 4 dB under the Curator's voice, attack 0.15 s, release
/// 0.7 s); effects are partly spatial (spatial blend 0.55, gentle roll-off) inside the hall's reverb; the rain is
/// two layers - the storm outside through the walls (low-passed) and the rain on the east window, placed at it.
/// </summary>
public class CuratorSound : MonoBehaviour
{
    public static readonly string[] Categories =
    {
        "steps_wood", "cloth", "book_take", "book_down", "book_open", "page", "candle_blow", "match",
        "rain_outside", "rain_window", "thunder_near", "thunder_far", "room_tone",
    };

    readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, int> last = new Dictionary<string, int>();
    readonly Dictionary<string, AudioClip> fallback = new Dictionary<string, AudioClip>();
    readonly List<AudioSource> pool = new List<AudioSource>();
    public AudioSource rainOut, rainWindow, roomTone;
    public AudioSource voice;                                       // the Curator's voice (ducking key)
    float duck;
    /// <summary>Gain for the scene's own beds while the Curator speaks (1 = no ducking).</summary>
    public float DuckGain { get { return Mathf.Lerp(1f, 0.63f, duck); } }
    public float rainLevel = 1f;
    /// <summary>All the beds (rain, room tone) together: a scene fades them with its picture.</summary>
    public float master = 1f;
    float dieIn;
    static bool reported;

    /// <summary>
    /// The scene is over (or skipped): the beds leave with a short fade instead of being cut off mid-sample, then this
    /// removes itself. It detaches first, so it outlives the scene that made it by `dur` seconds.
    /// </summary>
    public void FadeOutAndDie(float dur)
    {
        if (dieIn > 0f) return;
        transform.SetParent(null, true);
        dieIn = Mathf.Max(0.05f, dur);
    }

    public static CuratorSound Make(Transform parent, Vector3 windowWorld, Vector3 hallCentreWorld)
    {
        var go = new GameObject("CuratorSound");
        go.transform.SetParent(parent, false);
        var s = go.AddComponent<CuratorSound>();
        s.Load();
        // the hall's acoustics: a big wooden room full of books - long but damped
        var rz = new GameObject("HallReverb").AddComponent<AudioReverbZone>();
        rz.transform.SetParent(go.transform, false);
        rz.transform.position = hallCentreWorld;
        rz.minDistance = 14f; rz.maxDistance = 30f;
        rz.reverbPreset = AudioReverbPreset.User;
        rz.room = -900; rz.roomHF = -700; rz.decayTime = 1.7f; rz.decayHFRatio = 0.55f;
        rz.reflections = -1400; rz.reflectionsDelay = 0.025f; rz.reverb = -700; rz.reverbDelay = 0.04f; rz.diffusion = 90f; rz.density = 80f;
        s.rainOut = s.Loop("rain_outside", go.transform, hallCentreWorld, 0f, 0.0f);
        if (s.rainOut != null) { var lp = s.rainOut.gameObject.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 1300f; lp.lowpassResonanceQ = 0.7f; }
        s.rainWindow = s.Loop("rain_window", go.transform, windowWorld, 0.8f, 0.0f);
        if (s.rainWindow != null) { s.rainWindow.minDistance = 3f; s.rainWindow.maxDistance = 22f; }
        s.roomTone = s.Loop("room_tone", go.transform, hallCentreWorld, 0f, 0.0f);
        return s;
    }

    void Load()
    {
        var missing = new List<string>();
        foreach (var c in Categories)
        {
            var found = Resources.LoadAll<AudioClip>("Audio/Curator/" + c);
            clips[c] = found;
            if (found.Length == 0) missing.Add(c);
        }
        if (missing.Count > 0 && !reported)
        {
            reported = true;
            Debug.LogWarning("[CuratorSound] no recordings for: " + string.Join(", ", missing) + " - placeholder sounds are used for these");
        }
    }

    /// <summary>The categories that have real recordings (for tests and the report).</summary>
    public List<string> Recorded() { var l = new List<string>(); foreach (var kv in clips) if (kv.Value.Length > 0) l.Add(kv.Key); return l; }

    public void SetFallback(string category, AudioClip clip) { fallback[category] = clip; }

    AudioClip Pick(string category)
    {
        AudioClip[] list;
        if (!clips.TryGetValue(category, out list) || list.Length == 0)
        {
            AudioClip f; return fallback.TryGetValue(category, out f) ? f : null;
        }
        int prev; last.TryGetValue(category, out prev);
        int i = list.Length == 1 ? 0 : Random.Range(0, list.Length - 1);
        if (list.Length > 1 && i >= prev) i++;                          // never the same take twice in a row
        last[category] = i;
        return list[i];
    }

    AudioSource Loop(string category, Transform parent, Vector3 at, float spatial, float volume)
    {
        var clip = Pick(category);
        if (clip == null) return null;
        var src = new GameObject("Loop_" + category).AddComponent<AudioSource>();
        src.transform.SetParent(parent, false);
        src.transform.position = at;
        src.clip = clip; src.loop = true; src.volume = volume; src.spatialBlend = spatial;
        src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 4f; src.maxDistance = 40f; src.dopplerLevel = 0f;
        src.time = Random.Range(0f, Mathf.Max(0f, clip.length - 0.1f));     // start somewhere in the take
        src.Play();
        return src;
    }

    AudioSource Free()
    {
        foreach (var s in pool) if (s != null && !s.isPlaying) return s;
        var n = new GameObject("Sfx").AddComponent<AudioSource>();
        n.transform.SetParent(transform, false);
        n.playOnAwake = false; n.dopplerLevel = 0f; n.rolloffMode = AudioRolloffMode.Linear; n.minDistance = 2f; n.maxDistance = 26f;
        pool.Add(n);
        return n;
    }

    /// <summary>Plays a take from a category at a point (spatial 0..1), level in dB relative to the take's own level.</summary>
    public AudioSource Play(string category, Vector3 at, float db = 0f, float spatial = 0.55f, float pitchVar = 0.04f)
    {
        var clip = Pick(category);
        if (clip == null) return null;
        var s = Free();
        s.transform.position = at;
        s.clip = clip; s.loop = false; s.spatialBlend = spatial;
        s.volume = Mathf.Clamp01(Mathf.Pow(10f, (db + Random.Range(-1.5f, 1.5f)) / 20f));
        s.pitch = 1f + Random.Range(-pitchVar, pitchVar);
        s.Play();
        return s;
    }

    /// <summary>A foot landing: heavier or lighter by strength, on the wooden floor (the velvet runner softens it).</summary>
    public void Step(Vector3 at, float strength, bool onRunner)
    {
        Play("steps_wood", at, Mathf.Lerp(-16f, -8f, strength) - (onRunner ? 6f : 0f), 0.6f, 0.05f);
        if (Random.value < 0.35f) Play("cloth", at + Vector3.up, -22f, 0.5f, 0.06f);
    }

    /// <summary>Thunder `km` away: near ones crack then roll, far ones only rumble. Louder when closer.</summary>
    public void Thunder(float km, float strength)
    {
        string cat = km < 1.5f ? "thunder_near" : "thunder_far";
        float db = Mathf.Lerp(-4f, -16f, Mathf.InverseLerp(0.5f, 6f, km)) + Mathf.Lerp(-4f, 0f, strength);
        var s = Play(cat, transform.position, db, 0f, 0.06f);
        if (s != null)
        {
            var lp = s.GetComponent<AudioLowPassFilter>();
            if (lp == null) lp = s.gameObject.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = Mathf.Lerp(9000f, 1800f, Mathf.InverseLerp(0.5f, 6f, km));    // distance takes the top off
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool speaking = voice != null && voice.isPlaying && !AudioListener.pause;
        duck = Mathf.MoveTowards(duck, speaking ? 1f : 0f, dt / (speaking ? 0.15f : 0.7f));
        if (dieIn > 0f)
        {
            master = Mathf.MoveTowards(master, 0f, dt / dieIn);
            if (master <= 0f) { Destroy(gameObject); return; }
        }
        float k = Mathf.Lerp(1f, 0.63f, duck) * master;  // about -4 dB under the voice
        if (rainOut != null) rainOut.volume = 0.42f * rainLevel * k;
        if (rainWindow != null) rainWindow.volume = 0.3f * rainLevel * k;
        if (roomTone != null) roomTone.volume = 0.25f * k;
    }
}

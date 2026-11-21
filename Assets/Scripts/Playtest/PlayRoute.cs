using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Scripted play-through for measurement and footage in a player build, so every build walks the same route.
/// Does nothing unless PINNACLE_ROUTE is set. Launch the built app's binary directly so the environment reaches it:
///   PINNACLE_ROUTE=market PINNACLE_PROFILE_OUT=/tmp/run1 PINNACLE_SAVE_DIR=/tmp/run1/saves \
///   Pinnacle.app/Contents/MacOS/Pinnacle -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
///
/// Routes (PINNACLE_ROUTE):
///   market   Act II from chapter select: cold entry, look at Bhaskar, walk through the bazaar gate to Vikram, talk
///            (first answer, then thank him), turn and look back through the crowd, walk at the watching woman so
///            she bolts, watch her ride out, pick up her card, the phone, the SUV arrival, the timed choice, the outcome.
///   walk     The market route's entry, walk and look back only (for quick A/B runs).
///   act1intro A new game: the prologue skipped, Act I's opening timed, its voices checked over free movement.
///   signs    Stills at eye height of the stall boards (front, side, behind, the strap) and of shop signs on both sides.
///   act1     Act I resumed in Priya's room: slow pans past the window latch and through the door to the stairwell
///            window, stop, back away (light and glazing stability).
/// Options: PINNACLE_ROUTE_TRUST=note|appeal|threat (Vikram's first challenge), PINNACLE_ROUTE_OUTCOME=hide|walk|plate|
///   timeout (the SUV), PINNACLE_ROUTE_RESUME=1 (enter Act II from a saved game: Bhaskar and Vikram already done),
///   PINNACLE_ROUTE_CURATOR=1 (carry on into the Curator's post-market visit and stop at its callback to Act III;
///   PINNACLE_ROUTE_CURATOR_SKIP=12 skips it with Enter after 12 s),
///   PINNACLE_ROUTE_CAPTURE=1 or a list of segments: approach,suv,curator for the market, act1 for Act I (footage: fixed
///   30 fps while a segment records (approach, suv, curator; act1; prologue), every frame with its UI as JPEG, the mix rendered offline to WAV, one folder per
///   segment; the stretches between run in real time; Tools/route_video.py makes the MP4), PINNACLE_ROUTE_SHOTS=1 (a few PNG stills; not for timing runs),
///   PINNACLE_ROUTE_INPUTTESTS=1 (Bhaskar's gated questions with keys and a virtual gamepad, a held key, pausing in the
///   SUV choice), PINNACLE_ROUTE_CENSUS=1 (census.txt: triangles and shadow casters per mesh once the market is built),
///   PINNACLE_ROUTE_EXP=a,b,... (A/B switches applied once the market is built: cascades=N, shadowdist=M, shadowres=N,
///   msaa=N, smaa=0, renderscale=X, softshadows=0, noshadows, propshadows=0, hideprops, hidecrowd).
/// Writes frames.csv (per frame: wall-clock time, CPU/GPU times where reported, render counters, section), summary.txt
/// (cold entry, steady play, the SUV, by section; median, p95, p99, worst, frames over 50 ms), checks.txt (what the
/// route saw the game do) and, in a development build, markers.csv + spikes.txt (Profiler markers per frame and the
/// biggest ones in every frame over 50 ms). Then it quits.
/// </summary>
[DefaultExecutionOrder(-800)]          // after GameInput (-900), before the story scripts read their input
public class PlayRoute : MonoBehaviour
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    string route, outDir, trustPick, outcome;
    bool resumeRun, curatorAfter, capture, shots, census, inputTests;
    Gamepad pad;
    string experiments;
    readonly HashSet<string> segments = new HashSet<string>();      // footage segments to record (PINNACLE_ROUTE_CAPTURE)

    // ------------------------------------------------------------------ virtual input
    Keyboard vk;
    readonly HashSet<Key> held = new HashSet<Key>();
    Key tapKey = Key.None; int tapStage;
    bool keysDirty = true;

    // ------------------------------------------------------------------ the game
    PriyaRoomBuilder builder; PriyaRoomStory story; PriyaRoomPlayer player;
    MonoBehaviour market;                  // MarketStory (read by reflection: works on any version of it)
    float yaw, pitch;

    // ------------------------------------------------------------------ samples
    string section = "boot";
    bool sampling; double prevT;
    readonly List<double> tAt = new List<double>();
    readonly List<float> frameMs = new List<float>(), cpuMs = new List<float>(), mainMs = new List<float>(), renderMs = new List<float>(), gpuMs = new List<float>();
    readonly List<string> secOf = new List<string>();
    readonly FrameTiming[] ft = new FrameTiming[1];
    static readonly string[] CounterNames = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count", "GC Allocated In Frame" };
    readonly List<ProfilerRecorder> counters = new List<ProfilerRecorder>();
    readonly List<string> counterLabel = new List<string>();
    readonly List<List<long>> counterVal = new List<List<long>>();
    readonly List<ProfilerRecorder> markers = new List<ProfilerRecorder>();
    readonly List<string> markerLabel = new List<string>();
    readonly List<List<float>> markerVal = new List<List<float>>();
    readonly StringBuilder checks = new StringBuilder(), log = new StringBuilder();
    readonly Dictionary<string, double> stamps = new Dictionary<string, double>();
    double tRequest;
    int failures;

    // ------------------------------------------------------------------ footage
    int capFrame; BinaryWriter wav; long wavSamples; int channels, sampleRate; string capDir;
    StringBuilder srt; int srtIndex; string srtLine; int srtStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PINNACLE_ROUTE"))) return;
        Application.runInBackground = true;          // a measurement must not pause when the window loses focus
        var go = new GameObject("PlayRoute");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayRoute>();
    }

    static string Env(string n, string d) { var v = Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(v) ? d : v; }
    // wall-clock time, except while capturing footage: then the game runs on a fixed 30 fps clock and so does the route
    double Now => capture ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;

    void Start()
    {
        route = Env("PINNACLE_ROUTE", "market");
        outDir = Env("PINNACLE_PROFILE_OUT", Path.Combine(Application.persistentDataPath, "route"));
        Directory.CreateDirectory(outDir);
        trustPick = Env("PINNACLE_ROUTE_TRUST", "note");
        outcome = Env("PINNACLE_ROUTE_OUTCOME", "hide");
        resumeRun = Env("PINNACLE_ROUTE_RESUME", "") == "1";
        curatorAfter = Env("PINNACLE_ROUTE_CURATOR", "") == "1";
        foreach (var seg in Env("PINNACLE_ROUTE_CAPTURE", "").Split(',')) if (seg.Trim().Length > 0) segments.Add(seg.Trim() == "1" ? "route" : seg.Trim());
        capture = segments.Count > 0;
        shots = Env("PINNACLE_ROUTE_SHOTS", "") == "1";
        census = Env("PINNACLE_ROUTE_CENSUS", "") == "1";
        inputTests = Env("PINNACLE_ROUTE_INPUTTESTS", "") == "1";
        experiments = Env("PINNACLE_ROUTE_EXP", "");
        UnityEngine.Random.InitState(20261121);       // the crowd's and the traffic's dice roll the same way every run

        // The virtual keyboard must keep working when the window is not in front.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        vk = InputSystem.AddDevice<Keyboard>("RouteKeyboard");
        vk.MakeCurrent();

        foreach (var n in CounterNames)
        {
            var r = ProfilerRecorder.StartNew(n == "GC Allocated In Frame" ? ProfilerCategory.Memory : ProfilerCategory.Render, n, 1);
            counters.Add(r); counterLabel.Add(n); counterVal.Add(new List<long>());
        }
        if (Debug.isDebugBuild) StartMarkers();
        Log("PlayRoute " + route + " | " + DateTime.Now.ToString("u") + " | Unity " + Application.unityVersion + " | " + (Debug.isDebugBuild ? "development" : "release") + " build");
        StartCoroutine(Run());
    }

    // Profiler markers are recorded in development builds only (the release player does not time them).
    void StartMarkers()
    {
        var all = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(all);
        var names = new StringBuilder();
        string[] want =
        {
            "PlayerLoop", "BehaviourUpdate", "LateBehaviourUpdate", "CoroutinesDelayedCalls", "FixedBehaviourUpdate", "Physics.Simulate",
            "Physics.Processing", "Physics.UpdateBodies", "Physics.SyncColliderTransform", "Physics.Interpolation", "GC.Collect", "Shader.CreateGPUProgram", "CreateGPUProgram",
            "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForRenderThread", "Gfx.WaitForGfxCommandsFromMainThread", "Gfx.PresentFrame",
            "Inl_UniversalRenderPipeline.RenderSingleCameraInternal", "UniversalRenderPipeline.RenderSingleCamera", "CullScriptable", "Culling",
            "MainLightShadow", "AdditionalLightsShadow", "Inl_ScriptableRenderer.Execute", "RenderLoop.ScheduleDraw", "MeshSkinning.Update", "MeshSkinning.SkinOnGPU",
            "Animators.Update", "Director.ProcessFrame", "Animator.ProcessGraphJob", "Animation.Update", "Loading.ReadObject", "Loading.AwakeFromLoad",
            "Mesh.CombineMeshes", "GameObject.Activate", "Instantiate", "Resources.Load", "AudioClip.SetData", "Texture2D.Apply", "Mesh.UploadMeshData",
            "Canvas.SendWillRenderCanvases", "GUI.Repaint", "GUIUtility.ProcessEvent", "UIEvents.IMGUIRenderOverlays", "Main Thread", "Render Thread",
        };
        var wanted = new HashSet<string>(want);
        foreach (var h in all)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            names.Append(d.Category.Name).Append('\t').Append(d.Name).Append('\n');
            bool loop = d.Name.StartsWith("PreUpdate.") || d.Name.StartsWith("Update.") || d.Name.StartsWith("PreLateUpdate.") || d.Name.StartsWith("PostLateUpdate.")
                        || d.Name.StartsWith("FixedUpdate.") || d.Name.StartsWith("EarlyUpdate.") || d.Name.StartsWith("Initialization.");
            if (!loop && !wanted.Contains(d.Name)) continue;
            if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
            if (markerLabel.Contains(d.Name)) continue;
            var r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            if (!r.IsRunning) r.Start();
            markers.Add(r); markerLabel.Add(d.Name); markerVal.Add(new List<float>());
        }
        File.WriteAllText(Path.Combine(outDir, "markers_available.txt"), names.ToString());
    }

    // ------------------------------------------------------------------ reflection helpers
    static FieldInfo FieldOf(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f; }
        return null;
    }
    static object Get(object o, string n) { if (o == null) return null; var f = FieldOf(o.GetType(), n); return f != null ? f.GetValue(o) : null; }
    static bool Has(object o, string n) { return o != null && FieldOf(o.GetType(), n) != null; }
    static bool GetBool(object o, string n) { var v = Get(o, n); return v is bool b && b; }
    static object Call(object o, string n, params object[] a)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
            foreach (var m in t.GetMethods(Any))
                if (m.Name == n && m.GetParameters().Length == a.Length) return m.Invoke(o, a);
        throw new MissingMethodException(o.GetType().Name + "." + n);
    }
    string Phase() { var p = Get(market, "phase"); return p != null ? p.ToString() : ""; }
    bool Free => Phase() == "Free" && !PriyaRoomPlayer.Locked && !GameState.Paused;
    T Npc<T>(string n) where T : class { return Get(market, n) as T; }

    void Log(string s) { log.AppendLine(s); Debug.Log("[ROUTE] " + s); }
    void Check(bool ok, string what) { checks.AppendLine((ok ? "PASS  " : "FAIL  ") + what); if (!ok) failures++; Log((ok ? "PASS  " : "FAIL  ") + what); }
    void Note(string what) { checks.AppendLine("NOTE  " + what); Log("NOTE  " + what); }
    void Stamp(string name) { if (!stamps.ContainsKey(name)) stamps[name] = Now - tRequest; }

    // ------------------------------------------------------------------ input
    void Hold(Key k, bool down)
    {
        if (down ? held.Add(k) : held.Remove(k)) keysDirty = true;
    }
    void Tap(Key k) { tapKey = k; tapStage = 1; keysDirty = true; }
    void ReleaseAll() { held.Clear(); keysDirty = true; }

    void Update()
    {
        // keyboard state for the next input update: held keys, plus a tapped key for exactly one update
        if (vk == null) return;
        if (Keyboard.current != vk) vk.MakeCurrent();
        if (tapStage == 2) { tapStage = 0; tapKey = Key.None; keysDirty = true; }
        if (!keysDirty && tapStage == 0) return;
        var keys = new List<Key>(held);
        if (tapStage == 1) { keys.Add(tapKey); tapStage = 2; }
        InputSystem.QueueStateEvent(vk, new KeyboardState(keys.ToArray()));
        keysDirty = false;
    }

    // ------------------------------------------------------------------ sampling
    void LateUpdate()
    {
        if (!sampling || capture) return;
        double now = Now;
        FrameTimingManager.CaptureFrameTimings();
        uint n = FrameTimingManager.GetLatestTimings(1, ft);
        tAt.Add(now - tRequest);
        frameMs.Add((float)((now - prevT) * 1000.0));
        prevT = now;
        cpuMs.Add(n > 0 ? (float)ft[0].cpuFrameTime : -1f);
        mainMs.Add(n > 0 ? (float)ft[0].cpuMainThreadFrameTime : -1f);
        renderMs.Add(n > 0 ? (float)ft[0].cpuRenderThreadFrameTime : -1f);
        gpuMs.Add(n > 0 ? (float)ft[0].gpuFrameTime : -1f);
        secOf.Add(section);
        for (int i = 0; i < counters.Count; i++) counterVal[i].Add(counters[i].Valid ? counters[i].LastValue : -1);
        for (int i = 0; i < markers.Count; i++) markerVal[i].Add(markers[i].Valid ? markers[i].LastValue * 1e-6f : -1f);
    }

    void StartSampling() { sampling = true; prevT = Now; }

    // ------------------------------------------------------------------ steering
    void SyncLook() { if (player != null) { yaw = player.Yaw; pitch = player.Pitch; } }

    void Steer(float wantYaw, float wantPitch, float yawRate, float pitchRate)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, yawRate * dt);
        pitch = Mathf.MoveTowards(pitch, wantPitch, pitchRate * dt);
        player.SetLook(yaw, pitch);
    }

    void AngleTo(Vector3 p, out float y, out float x)
    {
        Vector3 d = p - player.cam.position;
        y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        x = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
    }

    IEnumerator LookAt(Vector3 p, float rate, float hold)
    {
        SyncLook();
        float t = 0;
        while (t < 6f)
        {
            if (!Free) yield break;
            AngleTo(p, out float y, out float x);
            Steer(y, x, rate, rate * 0.6f);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, y)) < 0.5f && Mathf.Abs(pitch - x) < 0.5f) break;
            t += Time.deltaTime; yield return null;
        }
        for (float h = 0; h < hold; h += Time.deltaTime)
        {
            if (!Free) yield break;
            AngleTo(p, out float y, out float x);
            Steer(y, x, rate, rate * 0.6f);
            yield return null;
        }
    }

    IEnumerator TurnBy(float degrees, float rate)
    {
        SyncLook();
        float target = yaw + degrees, turned = 0f;
        while (Mathf.Abs(turned) < Mathf.Abs(degrees) - 0.01f)
        {
            if (!Free) yield break;
            float step = Mathf.Sign(degrees) * Mathf.Min(rate * Time.deltaTime, Mathf.Abs(degrees) - Mathf.Abs(turned));
            turned += step; yaw += step;
            pitch = Mathf.MoveTowards(pitch, 2f, 30f * Time.deltaTime);
            player.SetLook(yaw, pitch);
            yield return null;
        }
        yaw = target; player.SetLook(yaw, pitch);
    }

    /// <summary>Walks like a player: turns toward the goal (at mouse-like speed), holds W, sidesteps when blocked.</summary>
    IEnumerator WalkTo(Vector3 goal, float stopDist, float timeout, bool sprint = false)
    {
        SyncLook();
        float t = 0, checkAt = 1.2f, dodge = 0f; int dodges = 0;
        Vector3 last = player.transform.position;
        while (t < timeout)
        {
            if (!Free) { ReleaseAll(); yield return null; t += Time.deltaTime; continue; }
            Vector3 d = goal - player.transform.position; d.y = 0;
            if (d.magnitude < stopDist) break;
            float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Steer(want, 3f, 140f, 40f);
            float off = Mathf.Abs(Mathf.DeltaAngle(yaw, want));
            Hold(Key.W, off < 55f);
            Hold(Key.LeftShift, sprint && off < 20f);
            if (dodge > 0f) { dodge -= Time.deltaTime; Hold(dodges % 2 == 0 ? Key.D : Key.A, dodge > 0f); }
            if (t > checkAt)
            {
                if ((player.transform.position - last).magnitude < 0.3f && off < 55f) { dodge = 0.8f; dodges++; }
                last = player.transform.position; checkAt = t + 1.2f;
            }
            t += Time.deltaTime;
            yield return null;
        }
        ReleaseAll();
        if (t >= timeout) Note("walk to " + goal.ToString("F1") + " timed out at " + player.transform.position.ToString("F1"));
    }

    IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
    {
        double t0 = Now;
        while (!cond())
        {
            if (Now - t0 > timeout) { Check(false, what + " (timed out after " + timeout + " s)"); yield break; }
            yield return null;
        }
    }

    // ------------------------------------------------------------------ the run
    IEnumerator Run()
    {
        for (int i = 0; i < 900 && (builder == null || story == null || builder.PlayerCtrl == null); i++)
        {
            builder = FindAnyObjectByType<PriyaRoomBuilder>(); story = FindAnyObjectByType<PriyaRoomStory>();
            yield return null;
        }
        if (builder == null || story == null) { Log("the game did not boot"); Finish(1); yield break; }
        for (int i = 0; i < 90; i++) yield return null;           // the title screen settles
        player = builder.PlayerCtrl;
        SaveSystem.Profile.playerName = "Route";
        if (segments.Contains("route")) StartCapture("route");
        if (route == "act1") yield return ActOne();
        else if (route == "act1intro") yield return ActOneIntro();
        else if (route == "signs") yield return Signs();
        else yield return Market(route == "walk");
        Finish(failures == 0 ? 0 : 2);
    }

    // ================================================================== ACT II
    IEnumerator Market(bool walkOnly = false)
    {
        if (resumeRun)
        {
            // a saved game from the middle of Act II: Bhaskar and Vikram already done, the woman not yet resolved
            var d = SaveSystem.Data;
            d.actsCompleted = Mathf.Max(d.actsCompleted, 1);
            SaveSystem.ResetAct(2);
            d.bhaskarDone = true; d.vikramDone = true; d.hasUsb = true;
            SaveSystem.AddEvidence("Route: a note from the saved game");
            SaveSystem.Save();
            Check(SaveSystem.Act2InProgress, "a mid-Act II save counts as in progress");
        }
        section = "entry";
        tRequest = Now;
        StartSampling();
        Call(story, "OnMenuPlay", 2, resumeRun);
        Stamp("request");
        while (market == null) { market = FindAnyObjectByType<MarketStory>(); yield return null; }
        bool hasLoading = Has(market, "loading");
        double t0 = Now;
        while (true)
        {
            bool built = hasLoading ? !GetBool(market, "loading") : (Get(market, "crowd") is System.Collections.ICollection c && c.Count > 0);
            if (built) Stamp("world_built");
            var fade = Get(market, "fade");
            if (built && fade is float f && f < 0.5f) Stamp("first_visible");
            if (Phase() == "Free" && !PriyaRoomPlayer.Locked) { Stamp("first_control"); break; }
            if (Now - t0 > 90f) { Check(false, "Act II never handed over control"); yield break; }
            yield return null;
        }
        Log("entry: control after " + stamps["first_control"].ToString("F2") + " s");
        if (census) Census(market.transform, "census.txt");
        if (experiments.Length > 0) ApplyExperiments(market.transform);
        if (shots) Shot("01_entry");

        var world = Get(market, "world") as MarketWorld;
        var bhaskar = Npc<CharacterNPC>("bhaskar");
        // Arjun's opening line runs while the player walks (patched builds); note whether it is still playing
        var voice = Get(market, "voice") as AudioSource;
        if (voice != null) Note("opening narration playing at first control: " + voice.isPlaying);

        // --------------------------------------------------------- Bhaskar, then the walk to Vikram
        if (bhaskar != null && !resumeRun)
        {
            section = "bhaskar";
            yield return LookAt(bhaskar.transform.position + Vector3.up * 1.55f, 110f, 1.2f);
            if (inputTests) yield return BhaskarInputs(bhaskar);
        }
        section = "walk";
        Vector3[] path = resumeRun
            ? new[] { new Vector3(-0.6f, 0, 31.5f) }
            : new[] { new Vector3(-0.6f, 0, 5f), new Vector3(-0.6f, 0, 12.5f), new Vector3(-0.7f, 0, 22f), new Vector3(-1.2f, 0, 30.5f) };
        for (int i = 0; i < path.Length; i++)
        {
            if (i == path.Length - 2 || (path.Length == 1 && i == 0)) Segment("approach", true);
            yield return WalkTo(path[i], 0.6f, 25f);
            if (i == 1 && shots) Shot("02_gate");
        }
        if (walkOnly)
        {
            section = "lookback";
            yield return TurnBy(180f, 120f);
            for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
            yield return TurnBy(-180f, 120f);
            sampling = false;
            WriteTiming();
            yield break;
        }
        if (!resumeRun)
        {
            yield return WalkTo(new Vector3(-1.75f, 0, 32.7f), 0.3f, 8f);
            Stamp("reached_vikram");
            // ----------------------------------------------------- Vikram
            var vikram = Npc<CharacterNPC>("vikram");
            section = "approach";
            yield return LookAt(vikram.transform.position + Vector3.up * 1.4f, 90f, 0.4f);
            if (shots) Shot("03_stall");
            yield return WaitFor(() => ((string)Get(market, "prompt") ?? "").Contains("Vikram"), 4f, "the talk prompt appears at Vikram's stall");
            Tap(Key.E);
            yield return WaitFor(() => Phase() == "Dialogue", 2f, "E starts Vikram's conversation");
            section = "talk";
            Stamp("talk_start");
            yield return Conversation();
            Stamp("talk_end");
        }
        yield return WaitFor(() => Free, 10f, "control returns after Vikram");

        // --------------------------------------------------------- look back through the crowd
        section = "lookback";
        Stamp("lookback");
        yield return TurnBy(180f, 120f);
        if (shots) Shot("05_lookback");
        for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
        yield return TurnBy(-120f, 120f);

        // --------------------------------------------------------- the watching woman
        section = "reddy";
        var reddy = Npc<CharacterNPC>("reddy");
        Check(reddy != null, "the watching woman is in the market after Vikram");
        if (reddy != null)
        {
            Stamp("reddy");
            // walk toward her until she bolts, then follow her with the eyes until she rides out of the far gate
            double r0 = Now;
            SyncLook();
            while (!GetBool(market, "reddyFled") && Now - r0 < 20f)
            {
                if (Free)
                {
                    Vector3 to = new Vector3(0.6f, 0, 35.2f) - player.transform.position; to.y = 0;
                    AngleTo(reddy.transform.position + Vector3.up * 1.5f, out float y, out float x);
                    Steer(y, x, 120f, 60f);
                    Hold(Key.W, to.magnitude > 0.4f);
                }
                yield return null;
            }
            ReleaseAll();
            Check(GetBool(market, "reddyFled"), "she bolts when Arjun walks at her");
            Stamp("reddy_bolts");
            double f0 = Now;
            while (!GetBool(market, "reddyResolved") && Now - f0 < 30f)
            {
                var rr = Npc<CharacterNPC>("reddy");
                if (rr != null && Free)
                {
                    AngleTo(rr.transform.position + Vector3.up * 1.2f, out float y, out float x);
                    Steer(y, x, 150f, 60f);
                }
                if (shots && Now - f0 > 3.0 && Now - f0 < 3.05) Shot("06_reddy");
                yield return null;
            }
            Check(GetBool(market, "reddyResolved"), "her ride out of the lane completes");
            Stamp("reddy_gone");
        }

        // --------------------------------------------------------- her card
        section = "card";
        var card = Get(market, "idCard") as GameObject;
        if (card != null)
        {
            Vector3 cp = card.transform.position;
            yield return WalkTo(new Vector3(cp.x - 0.45f, 0, cp.z - 1.5f), 0.35f, 12f);
            yield return LookAt(cp, 80f, 0.3f);
            yield return WaitFor(() => ((string)Get(market, "prompt") ?? "").Contains("ID card"), 4f, "the card prompt appears");
            Tap(Key.E);
            yield return WaitFor(() => Get(market, "idCard") as GameObject == null, 3f, "E picks up her card");
            Check(SaveSystem.Data.hasIdCard, "the card is saved as evidence");
        }
        // back toward the stall: the phone rings when Arjun is close
        double h0 = Now;
        while (!GetBool(market, "hookStarted") && Now - h0 < 20f)
        {
            if (Free)
            {
                Vector3 to = new Vector3(-1.2f, 0, 34f) - player.transform.position; to.y = 0;
                Steer(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 3f, 120f, 40f);
                Hold(Key.W, to.magnitude > 0.5f);
            }
            yield return null;
        }
        ReleaseAll();
        Check(GetBool(market, "hookStarted"), "the phone call starts near the stall");

        // --------------------------------------------------------- the SUV
        section = "suv";
        Stamp("phone");
        Segment("suv", true);
        yield return WaitFor(() => GetBool(market, "choiceActive"), 40f, "the timed SUV choice opens");
        Stamp("choice_open");
        if (shots) Shot("07_choice");
        section = "choice";
        float deadline0 = Get(market, "choiceDeadline") is float dl ? dl : -1f;
        Check(deadline0 > 0f && Mathf.Abs(deadline0 - Time.time - 15f) < 0.6f, "the SUV choice gives 15 s");
        int want = outcome == "walk" ? 1 : outcome == "plate" ? 2 : outcome == "timeout" ? -1 : 0;
        if (inputTests && want >= 0)
        {
            // pause inside the timed choice: the clock stops, a key is ignored, and after resuming it still works
            for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
            float leftBefore = (float)Get(market, "choiceDeadline") - Time.time;
            Tap(Key.Escape);
            for (int i = 0; i < 4; i++) yield return null;
            Check(GameState.Paused, "Escape pauses during the SUV choice");
            double p0 = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - p0 < 3.0) yield return null;
            Tap(Key.Digit3);
            for (int i = 0; i < 4; i++) yield return null;
            float leftPaused = (float)Get(market, "choiceDeadline") - Time.time;
            Check(GetBool(market, "choiceActive") && Mathf.Abs(leftPaused - leftBefore) < 0.2f, "paused for 3 s: the choice stays open and its clock stands still (" + leftBefore.ToString("F2") + " s -> " + leftPaused.ToString("F2") + " s left)");
            Tap(Key.Escape);
            for (int i = 0; i < 4; i++) yield return null;
            Check(!GameState.Paused && GetBool(market, "choiceActive"), "resuming returns to the open choice");
        }
        if (want >= 0)
        {
            for (float t = 0; t < 3f; t += Time.deltaTime) yield return null;
            Tap(want == 0 ? Key.Digit1 : want == 1 ? Key.Digit2 : Key.Digit3);
            yield return WaitFor(() => !GetBool(market, "choiceActive"), 2f, "a key picks the SUV answer");
        }
        else yield return WaitFor(() => !GetBool(market, "choiceActive"), 17f, "the SUV choice times out");
        int got = Get(market, "choiceResult") is int cr ? cr : -9;
        Check(got == (want >= 0 ? want : 1), "SUV result " + got + " (wanted " + (want >= 0 ? want : 1) + ")");
        Stamp("choice_made");
        section = "outcome";
        yield return WaitFor(() => Phase() == "Ending", 70f, "Act II reaches its ending");
        Stamp("act_end");
        if (want == 2) Check(SaveSystem.Data.hasPlate, "the plate is saved");
        Check(SaveSystem.Data.actsCompleted >= 2, "Act II is recorded as complete");
        Segment("suv", false);
        sampling = false;
        WriteTiming();
        if (curatorAfter)
        {
            Segment("curator", true);
            yield return CuratorVisit();
            for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
            Segment("curator", false);
        }
    }

    /// <summary>
    /// Bhaskar's optional questions as an input test: a number on a gated question only focuses it, the gamepad's stick
    /// and south button choose "Ask about Priya", the arrow keys then pick the newly opened question with Enter, a key
    /// held across the dial's opening does not answer, and "End the ride" closes the talk.
    /// </summary>
    IEnumerator BhaskarInputs(CharacterNPC bhaskar)
    {
        if (pad == null) { try { pad = InputSystem.AddDevice<Gamepad>("RouteGamepad"); pad.MakeCurrent(); } catch (Exception e) { Note("no virtual gamepad: " + e.Message); } }
        yield return WaitFor(() => ((string)Get(market, "prompt") ?? "").Contains("Bhaskar"), 4f, "the talk prompt appears at Bhaskar");
        Tap(Key.E);
        yield return WaitFor(() => Get(market, "dlgOptions") is string[], 20f, "Bhaskar's questions open as a compass");
        for (float t = 0; t < 0.5f; t += Time.deltaTime) yield return null;
        Tap(Key.Digit2);                                             // "The caller" is gated until Priya has been asked about
        for (int i = 0; i < 6; i++) yield return null;
        Check(Get(market, "dlgOptions") is string[], "a number on a gated question does not answer it");
        if (pad != null)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.left });
            for (int i = 0; i < 4; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.left, buttons = (uint)(1 << (int)GamepadButton.South) });
            for (int i = 0; i < 3; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            for (int i = 0; i < 3; i++) yield return null;
            int got = Get(market, "menuResult") is int mr ? mr : -9;
            Check(got == 0, "the gamepad's stick and south button choose 'Ask about Priya' (result " + got + ")");
        }
        else Tap(Key.Digit1);
        // the next dial: hold 4 through its opening (no answer), then arrows + Enter choose "The caller"
        Hold(Key.Digit4, true);
        object first = Get(market, "dlgOptions");
        double t1 = Now;
        while (Now - t1 < 90f && (!(Get(market, "dlgOptions") is string[] o) || ReferenceEquals(o, first))) yield return null;
        for (float t = 0; t < 0.6f; t += Time.deltaTime) yield return null;
        Check(Get(market, "dlgOptions") is string[], "a key held across the dial's opening does not answer");
        Hold(Key.Digit4, false);
        Tap(Key.RightArrow);
        for (int i = 0; i < 3; i++) yield return null;
        Tap(Key.Enter);
        for (int i = 0; i < 4; i++) yield return null;
        int got2 = Get(market, "menuResult") is int mr2 ? mr2 : -9;
        Check(got2 == 1, "the arrow keys and Enter choose 'The caller' once Priya has been asked about (result " + got2 + ")");
        object second = Get(market, "dlgOptions");
        double t2 = Now;
        while (Now - t2 < 90f && Phase() == "Dialogue" && (!(Get(market, "dlgOptions") is string[] o2) || ReferenceEquals(o2, second))) yield return null;
        for (float t = 0; t < 0.4f; t += Time.deltaTime) yield return null;
        Tap(Key.Digit4);
        yield return WaitFor(() => Phase() == "Free", 30f, "'End the ride' closes Bhaskar's questions");
        Check(SaveSystem.Data.evidence.Count >= 2, "Bhaskar's answers were noted (" + SaveSystem.Data.evidence.Count + " notes)");
        SyncLook();
    }

    /// <summary>Vikram's conversation: the first challenge as configured, then "Thank Vikram" so the route stays short.</summary>
    IEnumerator Conversation()
    {
        object lastMenu = null; int menus = 0; double t0 = Now;
        var vikram = Npc<CharacterNPC>("vikram");
        var voice = Get(market, "voice") as AudioSource;
        bool wasTalking = false; double lastSpeechEnd = -1;
        while (Phase() == "Dialogue" && Now - t0 < 240f)
        {
            bool talking = (vikram != null && vikram.IsTalking) || (voice != null && voice.isPlaying);
            if (wasTalking && !talking) lastSpeechEnd = Now;
            wasTalking = talking;
            var opts = Get(market, "dlgOptions") as string[];
            if (opts != null && !ReferenceEquals(opts, lastMenu))
            {
                lastMenu = opts; menus++;
                Stamp("menu" + menus);
                if (lastSpeechEnd > 0) Note("reply-to-decision gap before decision " + menus + ": " + (Now - lastSpeechEnd).ToString("F2") + " s");
                if (vikram != null)
                {
                    var vp = player.cam.GetComponent<Camera>().WorldToViewportPoint(vikram.transform.position + Vector3.up * 1.6f);
                    bool inFrame = vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.1f && vp.y < 0.95f;
                    Check(inFrame, "Vikram's face is in frame at decision " + menus + " (viewport " + vp.ToString("F2") + ", yaw " + player.Yaw.ToString("F0") + ", pitch " + player.Pitch.ToString("F0") + ", player " + player.transform.position.ToString("F1") + ", Vikram " + vikram.transform.position.ToString("F1") + ")");
                }
                for (float t = 0; t < 0.8f; t += Time.deltaTime) yield return null;     // reading time
                if (shots && menus == 1) Shot("04_choice");
                Key k = Key.Digit4;
                if (menus == 1) k = trustPick == "appeal" ? Key.Digit2 : trustPick == "threat" ? Key.Digit3 : Key.Digit1;
                Tap(k);
                yield return null; yield return null; yield return null;
                if (menus == 1)
                {
                    string tr = Get(market, "trust") as string;
                    for (int i = 0; i < 600 && (tr == "COLD"); i++) { yield return null; tr = Get(market, "trust") as string; }
                    Note("trust after the first answer (" + trustPick + "): " + tr);
                    for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
                    Segment("approach", false);
                }
            }
            yield return null;
        }
        Check(menus >= 2, "Vikram's conversation offered " + menus + " decisions");
        Check(SaveSystem.Data.vikramDone, "Vikram's conversation is saved as done");
    }

    IEnumerator CuratorVisit()
    {
        section = "curator";
        CuratorInterlude inter = null;
        for (int i = 0; i < 900 && inter == null; i++) { inter = FindAnyObjectByType<CuratorInterlude>(); yield return null; }
        Check(inter != null, "the Curator's visit follows the market");
        if (inter == null) yield break;
        float skipAfter;
        float.TryParse(Env("PINNACLE_ROUTE_CURATOR_SKIP", "-1"), NumberStyles.Float, CultureInfo.InvariantCulture, out skipAfter);
        double c0 = Now; bool skipped = false;
        while (inter != null && Now - c0 < 300f)
        {
            if (!skipped && skipAfter > 0f && Now - c0 > skipAfter) { Tap(Key.Enter); skipped = true; Stamp("curator_skip"); }
            yield return null;
        }
        Stamp("curator_end");
        Check(inter == null, "the Curator's visit ends" + (skipped ? " after a skip" : ""));
        for (int i = 0; i < 30; i++) yield return null;
        int acts = FindObjectsByType<Act3Story>().Length;
        Check(acts == 1, "it hands over to Act III exactly once (" + acts + " Act III stories)");
        var camT = player.cam;
        Check(camT.parent == player.transform, "the camera is back on the player after the visit");
        Check(Time.timeScale == 1f && !AudioListener.pause && !GameState.Paused, "time, sound and pause are given back after the visit");
    }

    // ================================================================== census and A/B switches
    static long Tris(Mesh m)
    {
        long t = 0;
        for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
        return t;
    }

    static string MeshKey(Renderer r, Mesh m)
    {
        string n = m != null && !string.IsNullOrEmpty(m.name) ? m.name : r.name;
        int k = n.IndexOf(" Instance"); if (k > 0) n = n.Substring(0, k);
        return n.Replace("(Clone)", "").Trim() + (r is SkinnedMeshRenderer ? " [skinned]" : "");
    }

    void Census(Transform root, string file)
    {
        var agg = new Dictionary<string, long[]>();          // instances, triangles, shadow-casting instances, casting triangles
        long all = 0, cast = 0; int rends = 0, casters = 0, skinned = 0, lights = 0, shadowLights = 0;
        foreach (var r in FindObjectsByType<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Mesh m = null;
            if (r is SkinnedMeshRenderer smr) { m = smr.sharedMesh; skinned++; }
            else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            if (m == null) continue;
            long t;
            string k;
            if (r.isPartOfStaticBatch && r is MeshRenderer mr)
            {
                // a statically batched renderer draws only its own range of the combined mesh
                t = 0;
                int n = Mathf.Max(1, r.sharedMaterials.Length);
                for (int i = 0; i < n && mr.subMeshStartIndex + i < m.subMeshCount; i++) t += m.GetIndexCount(mr.subMeshStartIndex + i) / 3;
                k = r.name.Replace("(Clone)", "").Trim() + " [batched]";
            }
            else { t = Tris(m); k = MeshKey(r, m); }
            bool c = r.shadowCastingMode != ShadowCastingMode.Off;
            if (!agg.TryGetValue(k, out var a)) agg[k] = a = new long[4];
            a[0]++; a[1] += t; if (c) { a[2]++; a[3] += t; }
            all += t; rends++; if (c) { cast += t; casters++; }
        }
        foreach (var l in FindObjectsByType<Light>()) if (l.enabled && l.gameObject.activeInHierarchy) { lights++; if (l.shadows != LightShadows.None) shadowLights++; }
        var keys = new List<string>(agg.Keys);
        keys.Sort((x, y) => agg[y][1].CompareTo(agg[x][1]));
        var sb = new StringBuilder();
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} renderers ({1} skinned), {2:N0} triangles; {3} cast shadows ({4:N0} triangles); {5} lights ({6} with shadows)", rends, skinned, all, casters, cast, lights, shadowLights));
        sb.AppendLine("instances  triangles   casting  casting-tris  mesh");
        foreach (var k in keys)
        {
            var a = agg[k];
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,9} {1,11:N0} {2,9} {3,13:N0}  {4}", a[0], a[1], a[2], a[3], k));
        }
        File.WriteAllText(Path.Combine(outDir, file), sb.ToString());
        Log("census: " + rends + " renderers, " + all.ToString("N0") + " triangles, " + casters + " casters");
    }

    void ApplyExperiments(Transform root)
    {
        var asset = UniversalRenderPipeline.asset;
        var camData = player.cam.GetComponent<UniversalAdditionalCameraData>();
        foreach (var raw in experiments.Split(','))
        {
            var kv = raw.Trim().Split('=');
            string k = kv[0]; string v = kv.Length > 1 ? kv[1] : "";
            float f; float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
            switch (k)
            {
                case "cascades": asset.shadowCascadeCount = (int)f; break;
                case "shadowdist": asset.shadowDistance = f; break;
                case "shadowres": asset.mainLightShadowmapResolution = (int)f; break;
                case "msaa": asset.msaaSampleCount = (int)f; break;
                case "renderscale": asset.renderScale = f; break;
                case "smaa": if (camData != null && f == 0f) camData.antialiasing = AntialiasingMode.None; break;
                case "softshadows":
                    var fi = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", Any);
                    if (fi != null) fi.SetValue(asset, f != 0f);
                    break;
                case "noshadows":
                    foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) l.shadows = LightShadows.None;
                    break;
                case "propshadows":
                    int off = 0;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        if (!(r is SkinnedMeshRenderer) && r.bounds.size.magnitude < 1.2f && r.shadowCastingMode != ShadowCastingMode.Off) { r.shadowCastingMode = ShadowCastingMode.Off; off++; }
                    Note("experiment propshadows=0: " + off + " small renderers stop casting");
                    break;
                case "hideprops":
                    int hid = 0;
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null && Tris(mf.sharedMesh) > 2000 && r.bounds.size.magnitude < 1.5f) { r.enabled = false; hid++; }
                    }
                    Note("experiment hideprops: " + hid + " detailed small props hidden");
                    break;
                case "hidecrowd":
                    foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.enabled = false;
                    break;
            }
        }
        Note("experiments applied: " + experiments + " | cascades " + asset.shadowCascadeCount + ", shadow distance " + asset.shadowDistance + ", msaa " + asset.msaaSampleCount + ", render scale " + asset.renderScale);
    }

    // ================================================================== signs
    void Stand(Vector3 world, Vector3 lookAt)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; player.transform.position = world; cc.enabled = true;
        AngleTo(lookAt, out float y, out float x);
        yaw = y; pitch = x; player.SetLook(yaw, pitch);
    }

    IEnumerator Signs()
    {
        Call(story, "OnMenuPlay", 2, false);
        while (market == null) { market = FindAnyObjectByType<MarketStory>(); yield return null; }
        yield return WaitFor(() => Phase() == "Free" && !PriyaRoomPlayer.Locked, 60f, "the market hands over control");
        var world = Get(market, "world") as MarketWorld;
        for (int i = 0; i < 60; i++) yield return null;
        string[] kinds = { "chai", "veg", "spice", "sweets", "news" };
        foreach (var k in kinds)
        {
            var st = FindDeep(world.transform, "Stall_" + k);
            if (st == null) { Note("no stall " + k); continue; }
            Transform board = null;
            foreach (Transform c in st) if (c.name == "SignBoard") { board = c; break; }
            if (board == null) { Note("no board on " + k); continue; }
            Vector3 bc = board.position;
            float half = board.localScale.x * 0.5f;
            var views = new (string, Vector3, Vector3)[]
            {
                ("front", st.TransformPoint(new Vector3(0f, 0f, 3.2f)), bc),
                ("side", st.TransformPoint(new Vector3(2.6f, 0f, 1.9f)), bc),
                ("behind", st.TransformPoint(new Vector3(0.35f, 0f, -0.75f)), bc),
                ("strap", st.TransformPoint(new Vector3(half + 0.7f, 0f, 1.6f)), st.TransformPoint(new Vector3(half - 0.12f, 2.72f, 0.55f))),
            };
            foreach (var v in views)
            {
                Stand(new Vector3(v.Item2.x, 0.05f, v.Item2.z), v.Item3);
                for (int i = 0; i < 12; i++) { player.SetLook(yaw, pitch); yield return null; }
                Shot("sign_" + k + "_" + v.Item1);
                for (int i = 0; i < 4; i++) yield return null;
            }
        }
        // permanent shop signs, one on each side of the lane
        int shops = 0;
        foreach (Transform c in world.transform)
        {
            if (c.name != "SignBoard" || shops >= 4 || c.position.z < 14f || c.position.z > 68f) continue;      // in the open lane
            bool left = c.position.x < 0f;
            if ((shops % 2 == 0) != left) continue;
            Vector3 front = new Vector3(left ? -0.8f : 0.8f, 0.05f, c.position.z - 3.5f);
            Stand(front, c.position);
            for (int i = 0; i < 12; i++) { player.SetLook(yaw, pitch); yield return null; }
            Shot("shop_" + shops + (left ? "_left" : "_right"));
            for (int i = 0; i < 4; i++) yield return null;
            shops++;
        }
        for (int i = 0; i < 10; i++) yield return null;
    }

    // ================================================================== ACT I
    void Place(Vector3 local, float yawDeg, float pitchDeg)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; player.transform.position = builder.Root.TransformPoint(local); cc.enabled = true;
        yaw = yawDeg; pitch = pitchDeg; player.SetLook(yaw, pitch);
    }

    /// <summary>A slow pan across a target (35 degrees each side at 18 degrees a second), a stop, then backing away.</summary>
    IEnumerator Pan(Transform target, string name)
    {
        AngleTo(target.position, out float y, out float x);
        yaw = y - 35f; pitch = x; player.SetLook(yaw, pitch);
        for (int i = 0; i < 20; i++) yield return null;
        section = name + "_pan";
        if (shots) Shot("act1_" + name + "_a");
        for (float t = 0; t < 70f / 18f; t += Time.deltaTime) { yaw += 18f * Time.deltaTime; player.SetLook(yaw, pitch); yield return null; }
        yaw = y + 35f;
        // back to the target and stop on it
        for (float t = 0; t < 35f / 18f; t += Time.deltaTime) { yaw -= 18f * Time.deltaTime; player.SetLook(yaw, pitch); yield return null; }
        section = name + "_still";
        for (float t = 0; t < 2f; t += Time.deltaTime) yield return null;
        if (shots) Shot("act1_" + name + "_b");
        section = name + "_back";
        Hold(Key.S, true);
        for (float t = 0; t < 1.6f; t += Time.deltaTime) yield return null;
        ReleaseAll();
        for (float t = 0; t < 1.0f; t += Time.deltaTime) yield return null;
    }

    IEnumerator ActOne()
    {
        // resume Act I in the room (one clue found), so the route starts in free play without the prologue
        SaveSystem.ResetAct(1);
        SaveSystem.AddClue("1_Laptop");
        SaveSystem.Save();
        tRequest = Now;
        Call(story, "OnMenuPlay", 1, true);
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Explore" && !PriyaRoomPlayer.Locked, 20f, "Act I resumes in free play");
        for (int i = 0; i < 30; i++) yield return null;
        market = null;
        section = "act1";
        StartSampling();
        Segment("act1", true);
        var latch = FindDeep(builder.Root, "CLUE_9_BrokenWindowLatch");
        var stair = FindDeep(builder.Root, "StairWindowGlow");
        Note("latch " + (latch != null ? latch.position.ToString("F2") : "not found") + ", stair window " + (stair != null ? stair.position.ToString("F2") : "not found"));
        // the broken latch, from 1.9 m inside Priya's room
        if (latch != null) { Place(new Vector3(0.7f, 0.1f, 0.7f), 0f, 0f); yield return Pan(latch, "latch"); }
        // the stairwell window at the end of the landing, from just outside Priya's door
        if (stair != null) { Place(new Vector3(-2.0f, 0.1f, -3.7f), -90f, 0f); yield return Pan(stair, "stair"); }
        Segment("act1", false);
        sampling = false;
        WriteTiming();
    }

    /// <summary>A new game: the prologue is skipped, then Act I's opening is timed and its voices checked.</summary>
    IEnumerator ActOneIntro()
    {
        tRequest = Now;
        Segment("prologue", true);
        Call(story, "OnMenuPlay", 1, false);
        CuratorInterlude inter = null;
        for (int i = 0; i < 600 && inter == null; i++) { inter = FindAnyObjectByType<CuratorInterlude>(); yield return null; }
        Check(inter != null, "a new game opens with the Curator's prologue");
        for (float t = 0; t < 4f; t += Time.deltaTime) yield return null;
        Tap(Key.Enter);
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Intro", 30f, "Act I's opening starts after the skipped prologue");
        double t0 = Now;
        Stamp("act1_intro");
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Explore" && !PriyaRoomPlayer.Locked, 30f, "control in Act I");
        Stamp("act1_control");
        Note("Act I: first control " + (Now - t0).ToString("F2") + " s after its opening began");
        Check(GetBool(story, "openingVoices"), "the opening narration carries on over free movement");
        var voice = Get(story, "voice") as AudioSource;
        for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
        Check(voice != null && voice.isPlaying, "Arjun's narration is audible while the player looks around");
        Tap(Key.Enter);
        for (int i = 0; i < 6; i++) yield return null;
        Check(!GetBool(story, "openingVoices") && (voice == null || !voice.isPlaying) && Get(story, "sub") == null, "Enter stops the opening voices and their subtitle at once");
        Segment("prologue", false);
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t == null) return null;
        if (t.name == n) return t;
        for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), n); if (r != null) return r; }
        return null;
    }

    // ================================================================== output
    void Shot(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png")); }

    static float Pct(List<float> s, double p) { return s.Count == 0 ? float.NaN : s[Mathf.Clamp((int)Math.Ceiling(s.Count * p) - 1, 0, s.Count - 1)]; }

    static string Stats(List<float> v)
    {
        var s = new List<float>(); foreach (var x in v) if (x >= 0f) s.Add(x);
        if (s.Count == 0) return "not reported";
        s.Sort();
        int over50 = 0, over100 = 0; double sum = 0;
        foreach (var x in s) { sum += x; if (x > 50f) over50++; if (x > 100f) over100++; }
        return string.Format(CultureInfo.InvariantCulture, "{0,6} frames  median {1,7:F2}  p95 {2,7:F2}  p99 {3,7:F2}  worst {4,8:F2} ms  >50 ms {5,4}  >100 ms {6,3}  mean {7,6:F2}",
            s.Count, Pct(s, 0.5), Pct(s, 0.95), Pct(s, 0.99), s[s.Count - 1], over50, over100, sum / s.Count);
    }

    List<float> Where(List<float> src, Func<string, bool> pick)
    {
        var r = new List<float>();
        for (int i = 0; i < src.Count && i < secOf.Count; i++) if (pick(secOf[i])) r.Add(src[i]);
        return r;
    }

    static readonly HashSet<string> Steady = new HashSet<string> { "bhaskar", "walk", "approach", "talk", "lookback", "reddy", "card" };
    static readonly HashSet<string> Suv = new HashSet<string> { "suv", "choice", "outcome" };

    void WriteTiming()
    {
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("t_s,frame_ms,cpu_ms,main_ms,render_ms,gpu_ms,section");
        foreach (var c in counterLabel) csv.Append(',').Append(c.Replace(' ', '_'));
        csv.Append('\n');
        for (int i = 0; i < frameMs.Count; i++)
        {
            csv.Append(tAt[i].ToString("F4", inv)).Append(',').Append(frameMs[i].ToString("F3", inv)).Append(',').Append(cpuMs[i].ToString("F3", inv)).Append(',')
               .Append(mainMs[i].ToString("F3", inv)).Append(',').Append(renderMs[i].ToString("F3", inv)).Append(',').Append(gpuMs[i].ToString("F3", inv)).Append(',').Append(secOf[i]);
            for (int c = 0; c < counterVal.Count; c++) csv.Append(',').Append(counterVal[c][i]);
            csv.Append('\n');
        }
        File.WriteAllText(Path.Combine(outDir, "frames.csv"), csv.ToString());

        var sb = new StringBuilder();
        sb.AppendLine("PlayRoute " + route + (resumeRun ? " (from a saved game)" : "") + ", real time, " + (Debug.isDebugBuild ? "DEVELOPMENT" : "release") + " player build");
        sb.AppendLine(string.Format("{0} | {1} | {2} | {3}x{4} {5} | quality {6} | look tier {7} | vSync {8} | target fps {9}", SystemInfo.deviceModel, SystemInfo.processorType,
            SystemInfo.graphicsDeviceName, Screen.width, Screen.height, Screen.fullScreenMode, QualitySettings.names[QualitySettings.GetQualityLevel()], LookController.Tier,
            QualitySettings.vSyncCount, Application.targetFrameRate));
        sb.AppendLine("trust pick " + trustPick + " | SUV outcome " + outcome);
        sb.Append("Moments (s after the request):");
        foreach (var kv in stamps) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value.ToString("F2", inv));
        sb.AppendLine();
        if (route == "market")
        {
            sb.AppendLine("COLD ENTRY   (request -> first controllable frame) " + Stats(Where(frameMs, s => s == "entry")));
            sb.AppendLine("STEADY PLAY  (Bhaskar -> gate -> Vikram -> look back -> Reddy -> card) " + Stats(Where(frameMs, s => Steady.Contains(s))));
            sb.AppendLine("SUV          (phone -> SUV arrival -> choice -> outcome) " + Stats(Where(frameMs, s => Suv.Contains(s))));
        }
        sb.AppendLine("ALL          " + Stats(frameMs));
        sb.AppendLine("By section (wall-clock frame time):");
        var order = new List<string>();
        foreach (var s in secOf) if (!order.Contains(s)) order.Add(s);
        foreach (var s in order)
        {
            string name = s;
            sb.AppendLine(string.Format("  {0,-9} {1}", name, Stats(Where(frameMs, x => x == name))));
            sb.AppendLine(string.Format("  {0,-9} CPU main {1}", "", Stats(Where(mainMs, x => x == name))));
            sb.AppendLine(string.Format("  {0,-9} GPU      {1}", "", Stats(Where(gpuMs, x => x == name))));
        }
        sb.AppendLine("Render counters (median per section):");
        foreach (var s in order)
        {
            sb.Append(string.Format("  {0,-9}", s));
            for (int c = 0; c < counterVal.Count; c++)
            {
                var v = new List<long>();
                for (int i = 0; i < secOf.Count; i++) if (secOf[i] == s && counterVal[c][i] >= 0) v.Add(counterVal[c][i]);
                v.Sort();
                sb.Append("  ").Append(counterLabel[c].Replace(" Count", "")).Append(' ').Append(v.Count > 0 ? v[v.Count / 2].ToString() : "n/a");
            }
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), sb.ToString());

        if (markers.Count > 0)
        {
            var mc = new StringBuilder("t_s,frame_ms,section");
            foreach (var m in markerLabel) mc.Append(',').Append(m.Replace(',', ';'));
            mc.Append('\n');
            // LastValue in frame i is frame i-1's sample: shift by one
            for (int i = 0; i + 1 < frameMs.Count; i++)
            {
                mc.Append(tAt[i].ToString("F4", inv)).Append(',').Append(frameMs[i].ToString("F3", inv)).Append(',').Append(secOf[i]);
                for (int m = 0; m < markerVal.Count; m++) mc.Append(',').Append(markerVal[m][i + 1].ToString("F3", inv));
                mc.Append('\n');
            }
            File.WriteAllText(Path.Combine(outDir, "markers.csv"), mc.ToString());
            var sp = new StringBuilder("Frames over 50 ms and their biggest Profiler markers (ms)\n");
            for (int i = 0; i + 1 < frameMs.Count; i++)
            {
                if (frameMs[i] <= 50f) continue;
                var top = new List<KeyValuePair<string, float>>();
                for (int m = 0; m < markerVal.Count; m++) top.Add(new KeyValuePair<string, float>(markerLabel[m], markerVal[m][i + 1]));
                top.Sort((a, b) => b.Value.CompareTo(a.Value));
                sp.Append(string.Format(inv, "t={0,8:F2}s {1,8:F1} ms [{2}]", tAt[i], frameMs[i], secOf[i]));
                for (int k = 0; k < 10 && k < top.Count; k++) sp.Append(string.Format(inv, "  {0} {1:F1}", top[k].Key, top[k].Value));
                sp.Append('\n');
            }
            File.WriteAllText(Path.Combine(outDir, "spikes.txt"), sp.ToString());
        }
        Log(sb.ToString());
    }

    void Finish(int code)
    {
        if (capture) StopCapture();
        if (sampling) { sampling = false; WriteTiming(); }
        File.WriteAllText(Path.Combine(outDir, "checks.txt"), checks.ToString());
        File.WriteAllText(Path.Combine(outDir, "route_log.txt"), log.ToString());
        foreach (var c in counters) c.Dispose();
        foreach (var m in markers) m.Dispose();
        if (vk != null) InputSystem.RemoveDevice(vk);
        if (pad != null) InputSystem.RemoveDevice(pad);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }

    // ================================================================== footage
    string segDir;

    void Segment(string name, bool on)
    {
        if (!segments.Contains(name)) return;
        if (on && wav == null) StartCapture(name);
        else if (!on && wav != null) StopCapture();
    }

    void StartCapture(string name)
    {
        segDir = Path.Combine(outDir, name);
        capDir = Path.Combine(segDir, "frames");
        Directory.CreateDirectory(capDir);
        capFrame = 0; wavSamples = 0; srtLine = null; srtIndex = 0;
        Time.captureFramerate = 30;
        channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        sampleRate = AudioSettings.outputSampleRate;
        wav = new BinaryWriter(File.Create(Path.Combine(segDir, "route.wav")));
        WriteWavHeader(wav, 0, channels, sampleRate);
        bool ok = AudioRenderer.Start();
        Log("capture: audio renderer " + (ok ? "on" : "FAILED") + ", " + channels + " ch @ " + sampleRate + " Hz, " + Screen.width + "x" + Screen.height);
        srt = new StringBuilder();
        StartCoroutine(CaptureLoop());
    }

    IEnumerator CaptureLoop()
    {
        var eof = new WaitForEndOfFrame();
        while (wav != null)
        {
            yield return eof;
            if (wav == null) yield break;
            int n = AudioRenderer.GetSampleCountForCaptureFrame();
            if (n > 0)
            {
                var buf = new NativeArray<float>(n * channels, Allocator.Temp);
                AudioRenderer.Render(buf);
                for (int i = 0; i < buf.Length; i++) wav.Write((short)Mathf.Clamp(Mathf.RoundToInt(buf[i] * 32767f), -32768, 32767));
                wavSamples += n;
                buf.Dispose();
            }
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(capDir, "f_" + capFrame.ToString("00000") + ".jpg"), tex.EncodeToJPG(82));
            Destroy(tex);
            string line = section;
            if (line != srtLine)
            {
                if (srtLine != null) Srt(srtStart, capFrame, srtLine);
                srtLine = line; srtStart = capFrame;
            }
            capFrame++;
        }
    }

    void Srt(int f0, int f1, string text)
    {
        srtIndex++;
        srt.Append(srtIndex).Append('\n').Append(Tc(f0)).Append(" --> ").Append(Tc(f1)).Append('\n').Append(text).Append("\n\n");
    }
    static string Tc(int f) { var t = TimeSpan.FromSeconds(f / 30.0); return string.Format("{0:00}:{1:00}:{2:00},{3:000}", (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds); }

    void StopCapture()
    {
        if (wav == null) return;
        if (srtLine != null) Srt(srtStart, capFrame, srtLine);
        AudioRenderer.Stop();
        wav.Seek(0, SeekOrigin.Begin);
        WriteWavHeader(wav, wavSamples, channels, sampleRate);
        wav.Close(); wav = null;
        File.WriteAllText(Path.Combine(segDir, "sections.srt"), srt.ToString());
        Time.captureFramerate = 0;
        Log("capture: " + capFrame + " frames, " + (wavSamples / (float)sampleRate).ToString("F1") + " s of audio");
    }

    static void WriteWavHeader(BinaryWriter w, long samples, int ch, int rate)
    {
        long dataBytes = samples * ch * 2;
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write((int)(36 + dataBytes)); w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate);
        w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write((int)dataBytes);
    }
}

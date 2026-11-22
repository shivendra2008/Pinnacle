using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
///            window, stop, back away (light and glazing stability); then a sidestep along the sill at eye height, the
///            stairwell window from the far end of the landing, and pans past every flat's nameplate from Priya's room
///            and from the landing (labels must stay on their walls and hide behind them).
///   act1door Act I with every clue found: the steps on the landing, the door that swings shut, a pause and resume in the
///            middle, opening it, the folded note, a skipped line of the finale (door sounds, double triggers, cuts).
///   kamala   Act I resumed with two clues found: walk to a third and examine it, wait for Mrs. Kapoor, then her
///            question, the compass, the needle swung with a virtual mouse, a click, Arjun asking, her answer.
///   act3     Act III from a saved game at the temple: the Pandit's question on the compass (PINNACLE_ROUTE_PANDIT=0|1|2),
///            the letter, the gate, the call, the crossroads (PINNACLE_ROUTE_BRANCH=0|1|2), the Curator's visit skipped,
///            and Act IV loading the chosen place; with branch 0 it walks on into the café (as the cafe route).
///   act3full Act III fresh from chapter select: the guard, the computer password, the basement, the cage, the calendar,
///            the librarian's drawer code, the key, the cage, the temple, the Pandit and the crossroads.
///   cafe     Act IV at Café Irani from chapter select: cold entry, the walk from the entrance to the counter, the
///            biscuit case from the side, the owner at work, the street from the terrace and the counter, timed.
/// Options: PINNACLE_ROUTE_TRUST=note|appeal|threat (Vikram's first challenge), PINNACLE_ROUTE_OUTCOME=hide|walk|plate|
///   timeout (the SUV), PINNACLE_ROUTE_RESUME=1 (enter Act II from a saved game: Bhaskar and Vikram already done),
///   PINNACLE_ROUTE_CURATOR=1 (carry on into the Curator's post-market visit and stop at its callback to Act III;
///   PINNACLE_ROUTE_CURATOR_SKIP=12 skips it with Enter after 12 s),
///   PINNACLE_ROUTE_CAPTURE=1 or a list of segments: approach,suv,curator for the market, act1 for Act I (footage: fixed
///   30 fps while a segment records (approach, suv, curator; act1; prologue), every frame with its UI as JPEG, the mix rendered offline to WAV, one folder per
///   segment; the stretches between run in real time; Tools/route_video.py makes the MP4), PINNACLE_ROUTE_SHOTS=1 (a few PNG stills; not for timing runs),
///   PINNACLE_ROUTE_POINTER=mouse (answer the dials by swinging the needle with a virtual mouse and clicking, instead of
///   the number keys), PINNACLE_ROUTE_INPUTTESTS=1 (Bhaskar's gated questions with keys and a virtual gamepad, a held key, pausing in the
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
    bool resumeRun, curatorAfter, capture, shots, census, inputTests, mousePointer;
    Gamepad pad;
    Mouse vm;                              // a virtual mouse that swings the compass needle (PINNACLE_ROUTE_POINTER=mouse)
    Vector2 mouseDelta; bool mouseClick, mouseDirty;
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
        mousePointer = Env("PINNACLE_ROUTE_POINTER", "") == "mouse" || route == "kamala" || route == "act3" || route == "act3full";
        experiments = Env("PINNACLE_ROUTE_EXP", "");
        UnityEngine.Random.InitState(20261121);       // the crowd's and the traffic's dice roll the same way every run

        // The virtual keyboard must keep working when the window is not in front.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        vk = InputSystem.AddDevice<Keyboard>("RouteKeyboard");
        vk.MakeCurrent();
        if (mousePointer) { vm = InputSystem.AddDevice<Mouse>("RouteMouse"); vm.MakeCurrent(); }

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
    static object Prop(object o, string n) { if (o == null) return null; var p = o.GetType().GetProperty(n, Any); return p != null ? p.GetValue(o) : null; }
    static int MouthModeOf(CharacterNPC c) { return Prop(c, "MouthMode") is int i ? i : -1; }
    static float MouthDegOf(CharacterNPC c) { return Prop(c, "MouthDegrees") is float f ? f : 0f; }
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
        // the virtual mouse: one delta (and click) per update, then back to rest
        if (vm != null)
        {
            if (Mouse.current != vm) vm.MakeCurrent();
            if (mouseDirty || mouseDelta != Vector2.zero || mouseClick)
            {
                var ms = new MouseState { delta = mouseDelta };
                if (mouseClick) ms = ms.WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(vm, ms);
                mouseDirty = mouseDelta != Vector2.zero || mouseClick;
                mouseDelta = Vector2.zero; mouseClick = false;
            }
        }
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

    /// <summary>WalkTo for any act: it walks whenever the player is free to move (WalkTo waits on the market's phase).</summary>
    IEnumerator WalkFree(Vector3 goal, float stopDist, float timeout)
    {
        SyncLook();
        float t = 0, checkAt = 1.2f, dodge = 0f; int dodges = 0;
        Vector3 last = player.transform.position;
        while (t < timeout)
        {
            if (PriyaRoomPlayer.Locked || GameState.Paused) { ReleaseAll(); yield return null; t += Time.deltaTime; continue; }
            Vector3 d = goal - player.transform.position; d.y = 0;
            if (d.magnitude < stopDist) break;
            float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Steer(want, 3f, 140f, 40f);
            float off = Mathf.Abs(Mathf.DeltaAngle(yaw, want));
            Hold(Key.W, off < 55f);
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
        else if (route == "act1door") yield return ActOneDoor();
        else if (route == "kamala") yield return Kamala();
        else if (route == "act3") yield return ActThree(false);
        else if (route == "act3full") yield return ActThree(true);
        else if (route == "cafe") yield return Cafe(true);
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
        Segment("lookback", true);                       // footage of the turn, to look for anything that flickers or pops
        yield return TurnBy(180f, 120f);
        if (shots) Shot("05_lookback");
        for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
        yield return TurnBy(-120f, 120f);
        Segment("lookback", false);

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
        bool fastWatched = false;
        while (Phase() == "Dialogue" && Now - t0 < 240f)
        {
            if (!fastWatched && vikram != null && vikram.IsTalking && Get(vikram, "voice") is AudioSource vs && vs.clip != null && vs.clip.name == "v_t2_intro")
            {
                fastWatched = true;
                yield return MouthWatch(vikram, "lip-sync, Vikram's long fast line (v_t2_intro)", () => !vikram.IsTalking, 30f);
                Check(lastMouth.x > 200 && lastMouth.z >= 5, "lip-sync: a fast 19 s line keeps the visemes going with lip closures (" + lastMouth.z + ")");
            }
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
                if (menus == 1 && mousePointer && CompassOf(market) != null)
                    yield return Dial(market, k == Key.Digit2 ? 1 : k == Key.Digit3 ? 2 : 0, "Vikram's first challenge", vikram);
                else Tap(k);
                yield return null; yield return null; yield return null;
                if (menus == 1)
                {
                    // his answer is the voice actor's recording, with the word timing its subtitles follow
                    string want = trustPick == "appeal" ? "q_v_1b" : trustPick == "threat" ? "q_v_1c" : "q_v_1a";
                    for (int i = 0; i < 240 && !(voice != null && voice.isPlaying && voice.clip != null && voice.clip.name == want); i++) yield return null;
                    var vt = voice != null && voice.clip != null ? VoTiming.Get(voice.clip.name) : null;
                    Check(voice != null && voice.clip != null && voice.clip.name == want && vt != null && vt.times[vt.times.Length - 1] <= voice.clip.length + 0.05f,
                          "Arjun's answer plays his recording (" + want + ", " + (voice != null && voice.clip != null ? voice.clip.length.ToString("F2") : "-") + " s)");
                    string tr = Get(market, "trust") as string;
                    double tw0 = Now;          // his line, the reaction beat and Vikram's reply come first (up to ~15 s)
                    while (tr == "COLD" && Now - tw0 < 25.0) { yield return null; tr = Get(market, "trust") as string; }
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
        // the stairwell window: a real opening with a pane now, a self-lit board before
        var stair = FindDeep(builder.Root, "StairWindowPane") ?? FindDeep(builder.Root, "StairWindowGlow");
        Note("latch " + (latch != null ? latch.position.ToString("F2") : "not found") + ", stair window " + (stair != null ? stair.position.ToString("F2") : "not found"));
        // the broken latch, from 1.9 m inside Priya's room
        if (latch != null) { Place(new Vector3(0.7f, 0.1f, 0.7f), 0f, 0f); yield return Pan(latch, "latch"); }
        // the stairwell window at the end of the landing, from just outside Priya's door
        if (stair != null) { Place(new Vector3(-2.0f, 0.1f, -3.7f), -90f, 0f); yield return Pan(stair, "stair"); }
        Segment("act1", false);
        // at Arjun's eye height, a sidestep along the sill looking out (the neighbour's wall must slide against the skyline)
        Segment("window", true);
        section = "sill";
        Place(new Vector3(0.45f, 0.1f, 1.55f), 0f, 2f);
        for (int i = 0; i < 25; i++) yield return null;
        if (shots) yield return StillShot("act1_sill_left");
        Hold(Key.D, true);
        for (float t = 0; t < 0.85f; t += Time.deltaTime) { player.SetLook(0f, 2f); yield return null; }
        Hold(Key.D, false);
        for (float t = 0; t < 0.8f; t += Time.deltaTime) { player.SetLook(0f, 2f); yield return null; }
        if (shots) yield return StillShot("act1_sill_right");
        // the latch up close, as evidence
        if (latch != null) { Place(new Vector3(1.55f, 0.1f, 1.7f), 0f, 0f); AngleTo(latch.position, out float ly, out float lx); yaw = ly; pitch = lx; player.SetLook(yaw, pitch); for (int i = 0; i < 25; i++) yield return null; if (shots) yield return StillShot("act1_latch_close"); }
        // from the far end of the landing, turning back to the stairwell window and the door
        section = "landing_far";
        Place(new Vector3(5.2f, 0.1f, -4.6f), 90f, 0f);
        for (int i = 0; i < 20; i++) yield return null;
        for (float t = 0; t < 180f / 45f; t += Time.deltaTime) { yaw -= 45f * Time.deltaTime; player.SetLook(yaw, 0f); yield return null; }
        for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
        if (shots) yield return StillShot("act1_landing_far");
        Segment("window", false);
        // the nameplates: from inside Priya's room (the Kapoor plate is on the other side of the wall and must not show),
        // then walking the landing past all four flats
        Segment("labels", true);
        section = "labels_room";
        Place(new Vector3(-0.6f, 0.1f, -0.4f), 150f, 0f);
        for (int i = 0; i < 20; i++) yield return null;
        for (float t = 0; t < 70f / 20f; t += Time.deltaTime) { yaw += 20f * Time.deltaTime; player.SetLook(yaw, 4f); yield return null; }
        if (shots) yield return StillShot("act1_labels_from_room");
        section = "labels_landing";
        Place(new Vector3(-3.4f, 0.1f, -4.4f), 60f, 0f);
        for (int i = 0; i < 20; i++) yield return null;
        // walk east along the landing, looking at the front-wall plates (301 Kapoor, 302 Verma), then turn to the back wall (303 Joshi, 304 Iyer)
        Hold(Key.D, true);
        for (float t = 0; t < 3.2f; t += Time.deltaTime) { player.SetLook(Mathf.Lerp(60f, 0f, t / 3.2f), 4f); yield return null; }
        Hold(Key.D, false);
        if (shots) yield return StillShot("act1_labels_landing_front");
        for (float t = 0; t < 180f / 40f; t += Time.deltaTime) { yaw = Mathf.Lerp(0f, 180f, t / (180f / 40f)); player.SetLook(yaw, 4f); yield return null; }
        Hold(Key.A, true);
        for (float t = 0; t < 3.0f; t += Time.deltaTime) { player.SetLook(180f, 4f); yield return null; }
        Hold(Key.A, false);
        if (shots) yield return StillShot("act1_labels_landing_back");
        // and back through Priya's door, looking at her plate as he passes it
        Place(new Vector3(-1.1f, 0.1f, -4.0f), 20f, 0f);
        for (float t = 0; t < 2.5f; t += Time.deltaTime) { player.SetLook(Mathf.Lerp(20f, -10f, t / 2.5f), 3f); yield return null; }
        Segment("labels", false);
        // a census of the labels: each plate and its lettering sit on their wall, the lettering uses the depth-tested material
        foreach (var tm in builder.Root.GetComponentsInChildren<TextMesh>(true))
        {
            if (!(tm.text == "KAPOOR" || tm.text == "VERMA" || tm.text == "JOSHI" || tm.text == "IYER" || (tm.text.Length == 3 && tm.text.StartsWith("30") && tm.transform.position.y > 1.4f))) continue;
            var mr = tm.GetComponent<MeshRenderer>();
            string sh = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.shader.name : "-";
            Check(sh == "Pinnacle/WorldText", "label " + tm.text + " is depth-tested (" + sh + ")");
        }
        sampling = false;
        WriteTiming();
    }

    /// <summary>
    /// Act I's ending: every clue found, so the steps come up the landing, the door swings shut, the light under it, a pause
    /// and resume in the middle of the steps, the door opened by hand, the folded note, one line of the finale skipped.
    /// Reads the door's sound log (newer builds) to check what played, where, and that nothing played twice.
    /// </summary>
    IEnumerator ActOneDoor()
    {
        SaveSystem.ResetAct(1);
        foreach (var id in new[] { "1_Laptop", "2_StickyNote", "3_NewspaperClippings", "4_HalfPackedSuitcase", "5_TwoChaiCups", "6_TornPhotograph", "7_BurnerPhone", "8_Diary", "9_BrokenWindowLatch" })
            SaveSystem.AddClue(id);
        SaveSystem.Data.dlgDone = true;
        SaveSystem.Save();
        tRequest = Now;
        Call(story, "OnMenuPlay", 1, true);
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Explore", 20f, "Act I resumes with every clue found");
        PlaceAt(builder.Root.TransformPoint(new Vector3(-1.2f, 0.1f, -0.6f)), 200f, 2f);
        Segment("door", true);
        section = "hook";
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Hook", 20f, "the hook begins once every clue is in");
        var hinge = builder.DoorHinge;
        // pause in the middle of the steps, then resume
        for (float t = 0; t < 4.6f; t += Time.deltaTime) yield return null;
        float before = hinge.localEulerAngles.y;
        Tap(Key.Escape);
        for (int i = 0; i < 4; i++) yield return null;
        Check(GameState.Paused, "Escape pauses during the steps");
        double p0 = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - p0 < 2.0) yield return null;
        Check(Mathf.Abs(Mathf.DeltaAngle(before, hinge.localEulerAngles.y)) < 0.5f, "the door does not move while paused");
        Tap(Key.Escape);
        for (int i = 0; i < 4; i++) yield return null;
        Check(!GameState.Paused, "Escape resumes");
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "DoorReady", 40f, "someone stands outside: the door can be opened");
        float shut = hinge.localEulerAngles.y; if (shut > 180f) shut -= 360f;
        Check(Mathf.Abs(shut) < 1f, "the door has swung shut (" + shut.ToString("F1") + " deg)");
        section = "open";
        var doorCentre = builder.Root.TransformPoint(new Vector3(-2f, 1.1f, -2.5f));
        yield return WalkFree(builder.Root.TransformPoint(new Vector3(-1.9f, 0.1f, -1.35f)), 0.3f, 8f);
        yield return LookAtPoint(doorCentre, 70f);
        yield return WaitFor(() => ((string)Get(story, "prompt") ?? "").Contains("Open the door"), 4f, "the open-door prompt appears");
        Tap(Key.E);
        yield return null;
        Tap(Key.E);                                                          // a second press straight after must not open it twice
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "NoteReady", 3f, "E opens the door");
        for (float t = 0; t < 1.8f; t += Time.deltaTime) yield return null;
        float open = hinge.localEulerAngles.y; if (open > 180f) open -= 360f;
        Check(Mathf.Abs(open + 100f) < 1f, "the door swings open to its stop (" + open.ToString("F1") + " deg)");
        section = "note";
        var note = builder.HookNote != null ? builder.HookNote.transform.position : doorCentre;
        yield return WalkFree(new Vector3(note.x, player.transform.position.y, note.z) + (player.transform.position - note).normalized * 1.1f, 0.3f, 8f);
        yield return LookAtPoint(note, 60f);
        yield return WaitFor(() => ((string)Get(story, "prompt") ?? "").Contains("note"), 4f, "the note prompt appears");
        Tap(Key.E);
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Ending", 3f, "E picks up the note");
        section = "finale";
        for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
        Tap(Key.Space);                                                      // skip the first line of the finale
        for (float t = 0; t < 5f; t += Time.deltaTime) yield return null;
        Segment("door", false);
        var logList = Get(story, "doorLog") as System.Collections.IList;
        if (logList != null)
        {
            var counts = new Dictionary<string, int>();
            foreach (var e in logList)
            {
                string k = e.ToString(); Note("door sound: " + k);
                string kind = k.Split(' ')[1];
                counts[kind] = counts.TryGetValue(kind, out int c) ? c + 1 : 1;
            }
            int Count(string k) => counts.TryGetValue(k, out int c) ? c : 0;
            Check(Count("latch") == 2 && Count("hinge_start") == 2 && Count("hinge_stop") == 2 && Count("frame") == 1,
                  "door sounds: two latch sounds (catching on the swing shut, released by hand), the hinge only while it turns (twice), one frame contact (the shut); counts " + string.Join(", ", counts));
        }
        else Note("no door sound log in this build");
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
        // the opening is the voice actor's recording, its subtitle phrases timed on his words (VoTiming)
        yield return WaitFor(() => voice != null && voice.isPlaying && voice.clip != null && voice.clip.name == "arjun_intro2", 15f, "the second opening line plays");
        for (int i = 0; i < 3; i++) yield return null;
        var ends = Get(Get(story, "narration"), "phraseEnds") as List<float>;
        var timing = VoTiming.Get("arjun_intro2");
        float len = voice != null && voice.clip != null ? voice.clip.length : 0f;
        bool timed = timing != null && ends != null && ends.Count > 1 && Array.Exists(timing.times, x => Mathf.Abs(x - ends[0] * len) < 0.02f);
        Check(timed, "the opening plays Arjun's recording, its subtitle changing phrase on his words (first phrase ends at "
              + (ends != null && ends.Count > 0 ? (ends[0] * len).ToString("F2") : "-") + " s of " + len.ToString("F2") + " s)");
        Tap(Key.Enter);
        for (int i = 0; i < 6; i++) yield return null;
        Check(!GetBool(story, "openingVoices") && (voice == null || !voice.isPlaying) && Get(story, "sub") == null, "Enter stops the opening voices and their subtitle at once");
        Segment("prologue", false);

        // Mrs. Kapoor's questions: Arjun asks the chosen one aloud, in his recorded voice, and then she answers
        var kamala = Get(story, "kamala") as CharacterNPC;
        story.StartCoroutine((IEnumerator)Call(story, "KamalaDialogue"));
        yield return WaitFor(() => Get(story, "dlgOptions") != null, 40f, "Mrs. Kapoor's questions open");
        for (float t = 0; t < 0.5f; t += Time.deltaTime) yield return null;
        Tap(Key.Digit1);
        yield return WaitFor(() => (Get(story, "dlgSpeaker") as string) == "ARJUN", 3f, "choosing a question makes Arjun ask it");
        double asked = Now;
        Check(voice != null && voice.isPlaying && voice.clip != null && voice.clip.name == "q_k_a" && (Get(story, "dlgText") as string) == "Did she mention anyone threatening her?",
              "he asks it in his recorded voice, the question as the subtitle (" + (voice != null && voice.clip != null ? voice.clip.name + ", " + voice.clip.length.ToString("F2") + " s" : "no clip") + ")");
        yield return WaitFor(() => (Get(story, "dlgSpeaker") as string) == "KAMALA KAPOOR" && kamala != null && kamala.IsTalking, 8f, "Mrs. Kapoor answers after him");
        Note("Mrs. Kapoor's answer began " + (Now - asked).ToString("F2") + " s after Arjun began asking");
        for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
        Tap(Key.Space);
        yield return WaitFor(() => Get(story, "dlgOptions") != null, 8f, "the questions come back after her answer");
        for (float t = 0; t < 0.5f; t += Time.deltaTime) yield return null;
        Tap(Key.Digit4);
        yield return WaitFor(() => GetBool(story, "dlgDone"), 12f, "\"That's all for now\" ends the conversation");
    }

    // ================================================================== the compass, by hand
    // read by reflection, so the same route runs on builds made before the compass changed
    static object CompassOf(object story) { return Get(story, "compass"); }
    static bool DialOpen(object c) { return c != null && Get(c, "labels") != null; }
    static int DialFocus(object c) { return DialOpen(c) && Get(c, "selected") is int i ? i : -1; }
    static float DialNeedle(object c) { return Get(c, "needle") is float f ? f : float.NaN; }
    static int DialCommitted(object c) { return Get(c, "committed") is int i ? i : -1; }

    static Transform HeadOf(CharacterNPC n) { return n == null ? null : (n.HeadForFace != null ? n.HeadForFace : n.transform); }

    /// <summary>Where a witness's eyes sit in the frame (viewport, y up) and whether they are above the dial's top.</summary>
    string FaceCheck(CharacterNPC witness, out bool inFrame, out bool aboveDial)
    {
        inFrame = aboveDial = false;
        var h = HeadOf(witness);
        if (h == null) return "no witness";
        var cam = player.cam.GetComponent<Camera>();
        Vector3 eyes = h.position + Vector3.up * 0.06f;
        var vp = cam.WorldToViewportPoint(eyes);
        inFrame = vp.z > 0f && vp.x > 0.08f && vp.x < 0.92f && vp.y > 0.1f && vp.y < 0.92f;
        // the compass cluster's top edge (StoryCompass: centre 0.705 of the height, minus the up label) is at 0.605 from
        // the top, i.e. viewport y 0.395; the eyes must sit clearly above it
        aboveDial = vp.y > 0.395f + 0.04f;
        return "eyes at viewport " + vp.ToString("F2");
    }

    /// <summary>
    /// Answers an open compass the way a player would with a mouse or trackpad: with the needle still at rest, swings it to
    /// each other bearing (it must follow and pick that bearing), then to the wanted one, dwells so the full line reads,
    /// and clicks. Without a compass (an older build) it presses the number.
    /// </summary>
    IEnumerator Dial(object story, int want, string what, CharacterNPC witness, bool stateTests = false)
    {
        object c = CompassOf(story);
        double w0 = Now;
        // the decision opens when its question has been spoken: wait for its options (every build has them) or the dial
        while (!(Get(story, "dlgOptions") is string[]) && !(c != null && DialOpen(c)) && Now - w0 < 90f) yield return null;
        while (c != null && !DialOpen(c) && Now - w0 < 92f) yield return null;
        if (c == null || !DialOpen(c))
        {
            Note(what + ": no compass in this build, answered with the number key");
            for (float t = 0; t < 0.8f; t += Time.deltaTime) yield return null;
            Tap(want == 0 ? Key.Digit1 : want == 1 ? Key.Digit2 : want == 2 ? Key.Digit3 : Key.Digit4);
            yield break;
        }
        Stamp(what.Replace(' ', '_') + "_open");
        for (int i = 0; i < 6; i++) yield return null;
        var labels = Get(c, "labels") as string[];
        var replies = Get(c, "replies") as string[];
        float rest = DialNeedle(c);
        Check(DialFocus(c) < 0 && !float.IsNaN(rest) && Mathf.Abs(Mathf.DeltaAngle(rest, 225f)) < 12f,
              what + ": the dial opens with its needle at rest on the empty lower-left diagonal and no answer picked (needle " + rest.ToString("F0") + " deg)");
        if (witness != null)
        {
            string where = FaceCheck(witness, out bool inFrame, out bool above);
            Check(inFrame && above, what + ": the witness's face stays in frame above the dial (" + where + ")");
        }
        Note(what + ": bearings " + (labels != null ? string.Join(" | ", labels) : "?"));
        for (float t = 0; t < 0.6f; t += Time.deltaTime) yield return null;      // the dial at rest, as the player first sees it
        if (stateTests)
        {
            // the gameplay cursor stays captured (the needle follows relative mouse movement)
            Check(Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, what + ": the cursor stays captured while the dial is open (" + Cursor.lockState + ")");
            // the journal key does nothing over a decision
            Tap(Key.J);
            for (int i = 0; i < 4; i++) yield return null;
            Check(!GetBool(story, "journalOpen") && DialOpen(c), what + ": J does not open the journal over the decision");
            // pause: a number is ignored behind the pause menu, and the dial is back, unanswered, after resuming
            Tap(Key.Escape);
            for (int i = 0; i < 4; i++) yield return null;
            Check(GameState.Paused, what + ": Escape pauses over the dial");
            Tap(Key.Digit2);
            for (int i = 0; i < 4; i++) yield return null;
            Check(DialOpen(c) && DialCommitted(c) != 1, what + ": a number pressed while paused does not answer");
            double pz = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - pz < 1.0) yield return null;
            Tap(Key.Escape);
            for (int i = 0; i < 4; i++) yield return null;
            Check(!GameState.Paused && DialOpen(c) && Cursor.lockState == CursorLockMode.Locked, what + ": resuming returns to the open dial with the cursor captured again");
            for (float t = 0; t < 0.4f; t += Time.deltaTime) yield return null;
        }
        var order = new List<int>();
        if (labels != null) { for (int i = 0; i < labels.Length; i++) if (i != want) order.Add(i); }
        order.Add(want);
        Vector2[] dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        foreach (int b in order)
        {
            // move the mouse so the pointer travels to 85% of the radius toward the bearing over ~0.3 s
            for (int f = 0; f < 10; f++)
            {
                var ptr = Get(c, "pointer") is Vector2 pv ? pv : Vector2.zero;
                Vector2 goal = dirs[b] * 0.85f;
                mouseDelta = (goal - ptr) * 36f / Mathf.Max(1, 10 - f);
                yield return null;
            }
            for (float t = 0; t < (b == want ? 1.1f : 0.65f); t += Time.deltaTime) yield return null;      // settle; the line reads
            float needle = DialNeedle(c);
            float off = float.IsNaN(needle) ? 0f : Mathf.Abs(Mathf.DeltaAngle(needle, Mathf.Atan2(dirs[b].y, dirs[b].x) * Mathf.Rad2Deg));
            Check(DialFocus(c) == b && off < 15f, what + ": the needle swings to '" + (labels != null && b < labels.Length ? labels[b] : b.ToString()) + "' and picks it (needle " + needle.ToString("F0") + " deg, " + off.ToString("F0") + " deg off)"
                  + (replies != null && b < replies.Length ? "; preview: " + replies[b] : ""));
        }
        mouseClick = true;
        Stamp(what.Replace(' ', '_') + "_commit");
        double c0 = Now;
        while (DialOpen(c) && Now - c0 < 2f) yield return null;
        Check(!DialOpen(c) && DialCommitted(c) == want, what + ": the click commits '" + (labels != null && want < labels.Length ? labels[want] : want.ToString()) + "' (index " + DialCommitted(c) + ")");
    }

    void PlaceAt(Vector3 world, float yawDeg, float pitchDeg)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; player.transform.position = world; cc.enabled = true;
        yaw = yawDeg; pitch = pitchDeg; player.SetLook(yaw, pitch);
    }

    // ================================================================== lip-sync, measured
    /// <summary>
    /// Samples a character's mouth every frame while <paramref name="until"/> is false: how it was driven (visemes or the
    /// loudness fallback), the jaw's range, how often the lips closed (PP weight peaks) and whether it was speaking.
    /// </summary>
    IEnumerator MouthWatch(CharacterNPC n, string what, Func<bool> until, float maxSeconds)
    {
        if (n == null || !Has(n, "visS")) { Note(what + ": no mouth layer in this build"); yield break; }
        int frames = 0, speaking = 0, visemeFrames = 0, closures = 0; float jawMax = 0f, jawMin = 99f; bool closed = false;
        double t0 = Now;
        while (!until() && Now - t0 < maxSeconds)
        {
            var w = Get(n, "visS") as float[];
            int mode = MouthModeOf(n);
            frames++;
            if (n.IsTalking) speaking++;
            if (mode == 2) visemeFrames++;
            if (n.IsTalking) { jawMax = Mathf.Max(jawMax, MouthDegOf(n)); jawMin = Mathf.Min(jawMin, MouthDegOf(n)); }
            bool pp = w != null && w.Length > 1 && w[1] > 0.45f;
            if (pp && !closed) closures++;
            closed = pp;
            yield return null;
        }
        Note(what + ": " + frames + " frames, speaking " + speaking + ", visemes " + visemeFrames + ", jaw " + (jawMin > 98f ? 0f : jawMin).ToString("F1") + ".." + jawMax.ToString("F1") + " deg, lip closures " + closures);
        lastMouth = new Vector3(visemeFrames, jawMax, closures);
    }
    Vector3 lastMouth;

    // ================================================================== Act I: Mrs. Kapoor after the third clue
    IEnumerator Kamala()
    {
        SaveSystem.ResetAct(1);
        SaveSystem.AddClue("1_Laptop");
        SaveSystem.AddClue("2_StickyNote");
        SaveSystem.Save();
        tRequest = Now;
        Call(story, "OnMenuPlay", 1, true);
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Explore" && !PriyaRoomPlayer.Locked, 20f, "Act I resumes with two clues found");
        for (int i = 0; i < 40; i++) yield return null;
        section = "act1";
        // the nearest clue not yet found: walk up to it, put the crosshair on it, press E
        ClueTag pick = null; float best = 99f;
        foreach (var t in builder.ClueTags)
        {
            if (t == null || t.found || t.id.StartsWith("Hook") || !t.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(t.transform.position, player.transform.position);
            if (d < best) { best = d; pick = t; }
        }
        Check(pick != null, "a third clue to find (" + (pick != null ? pick.id : "none") + ")");
        if (pick == null) yield break;
        var rend = pick.GetComponentInChildren<Renderer>();
        Vector3 cc = rend != null ? rend.bounds.center : pick.transform.position;
        Vector3 toward = player.transform.position - cc; toward.y = 0;
        Vector3 stand = cc + toward.normalized * 1.25f; stand.y = player.transform.position.y;
        yield return WalkFree(stand, 0.3f, 12f);
        yield return LookAtPoint(cc, 70f);
        yield return WaitFor(() => ((string)Get(story, "prompt") ?? "").Contains("Examine"), 4f, "the examine prompt appears on the third clue");
        Tap(Key.E);
        yield return WaitFor(() => pick.found, 3f, "E examines the third clue");
        // Mrs. Kapoor comes to the doorway once the clue card is read
        Segment("kamala", true);
        section = "kamala_walk";
        yield return WaitFor(() => (Get(story, "phase") ?? "").ToString() == "Dialogue", 40f, "Mrs. Kapoor comes to talk after the third clue");
        var kamala = Get(story, "kamala") as CharacterNPC;
        // her question, then the dial
        yield return WaitFor(() => (Get(story, "dlgText") as string) == "Have you found anything?", 20f, "she asks her question");
        section = "kamala_question";
        double qAt = Now;
        // a quiet, short line in close-up: the mouth follows its viseme track and settles when it ends
        yield return MouthWatch(kamala, "lip-sync, her quiet question", () => kamala == null || (!kamala.IsTalking && Now - qAt > 0.3), 10f);
        Check(lastMouth.x > 10 && lastMouth.y > 2f, "lip-sync: her question drives the mouth from its viseme track (" + lastMouth.x + " frames, jaw to " + lastMouth.y.ToString("F1") + " deg)");
        for (int i = 0; i < 6; i++) yield return null;
        Check(kamala != null && MouthModeOf(kamala) == 0 && Mathf.Abs(MouthDegOf(kamala)) < 0.3f, "lip-sync: the mouth is at rest once her question has ended (jaw " + (kamala != null ? MouthDegOf(kamala).ToString("F2") : "-") + " deg)");
        double qEnd = Now;
        yield return WaitFor(() => Get(story, "dlgOptions") != null, 10f, "the compass opens after her question");
        Note("question to compass: " + (Now - qEnd).ToString("F2") + " s after her voice ended (" + (qEnd - qAt).ToString("F2") + " s of question)");
        section = "kamala_dial";
        yield return Dial(story, 0, "Mrs. Kapoor's questions", kamala, true);
        // Arjun asks it aloud, then she reacts
        section = "kamala_answer";
        yield return WaitFor(() => (Get(story, "dlgSpeaker") as string) == "ARJUN", 3f, "Arjun asks the chosen question");
        var voice = Get(story, "voice") as AudioSource;
        Check(voice != null && voice.isPlaying && voice.clip != null && voice.clip.name == "q_k_a", "he asks it in his recorded voice (" + (voice != null && voice.clip != null ? voice.clip.name : "-") + ")");
        // the listener stays silent while he speaks
        float listenerJaw = 0f; int listenerMoving = 0;
        while (voice != null && voice.isPlaying && kamala != null && !kamala.IsTalking) { listenerJaw = Mathf.Max(listenerJaw, Mathf.Abs(MouthDegOf(kamala))); if (MouthModeOf(kamala) != 0) listenerMoving++; yield return null; }
        Check(listenerJaw < 0.3f && listenerMoving == 0, "lip-sync: Mrs. Kapoor's mouth stays still while Arjun asks (jaw at most " + listenerJaw.ToString("F2") + " deg)");
        yield return WaitFor(() => kamala != null && kamala.IsTalking && (Get(story, "dlgSpeaker") as string) == "KAMALA KAPOOR", 8f, "Mrs. Kapoor answers");
        double aAt = Now;
        yield return MouthWatch(kamala, "lip-sync, her answer (first 4.5 s)", () => Now - aAt > 4.5, 5f);
        Check(lastMouth.x > 60 && lastMouth.z >= 1, "lip-sync: her answer moves the mouth from its track and closes the lips on P/B/M (" + lastMouth.z + " closures in 4.5 s)");
        Segment("kamala", false);
        // a held Space that skips her answer must not answer the next dial; the cut line must leave the mouth at rest
        Hold(Key.Space, true);
        double cutAt = Now; float settle = -1f;
        while (Now - cutAt < 1.0)
        {
            if (kamala != null && !kamala.IsTalking && settle < 0f) { }
            if (kamala != null && !kamala.IsTalking && MouthDegOf(kamala) < 0.5f && (Get(kamala, "visS") as float[] ?? new float[1]).Max() < 0.1f) { settle = (float)(Now - cutAt); break; }
            yield return null;
        }
        Check(settle >= 0f && settle < 0.25f, "lip-sync: cut off mid-line, her mouth settles to rest in " + (settle < 0 ? "more than 1" : settle.ToString("F2")) + " s");
        yield return WaitFor(() => Get(story, "dlgOptions") != null, 30f, "the questions come back after her answer");
        for (float t = 0; t < 0.8f; t += Time.deltaTime) yield return null;
        Check(Get(story, "dlgOptions") != null, "Space held through the end of her answer does not answer the next question");
        Hold(Key.Space, false);
        for (int i = 0; i < 4; i++) yield return null;
        Tap(Key.Digit4);
        yield return WaitFor(() => GetBool(story, "dlgDone"), 12f, "'That's all for now' ends the conversation");
    }

    /// <summary>Turns the view onto a point at a mouse-like rate and keeps it there (any act; no phase check).</summary>
    IEnumerator LookHold(Vector3 p, float rate, float hold)
    {
        yield return LookAtPoint(p, rate);
        for (float h = 0; h < hold; h += Time.deltaTime) { AngleTo(p, out float y, out float x); Steer(y, x, rate, rate); yield return null; }
    }

    /// <summary>A steady turn on the spot (any act; no phase check).</summary>
    IEnumerator TurnSteady(float degrees, float rate)
    {
        SyncLook();
        float turned = 0f;
        while (Mathf.Abs(turned) < Mathf.Abs(degrees) - 0.01f)
        {
            float step = Mathf.Sign(degrees) * Mathf.Min(rate * Time.deltaTime, Mathf.Abs(degrees) - Mathf.Abs(turned));
            turned += step; yaw += step;
            pitch = Mathf.MoveTowards(pitch, 2f, 30f * Time.deltaTime);
            player.SetLook(yaw, pitch);
            yield return null;
        }
    }

    IEnumerator LookAtPoint(Vector3 p, float rate)
    {
        SyncLook();
        for (float t = 0; t < 3f; t += Time.deltaTime)
        {
            AngleTo(p, out float y, out float x);
            Steer(y, x, rate, rate * 0.7f);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, y)) < 0.4f && Mathf.Abs(pitch - x) < 0.4f) break;
            yield return null;
        }
        for (int i = 0; i < 8; i++) { AngleTo(p, out float y, out float x); Steer(y, x, rate, rate); yield return null; }
    }

    // ================================================================== Act III: the Pandit, the crossroads, Act IV
    string StoryPhase(object st) { var p = Get(st, "phase"); return p != null ? p.ToString() : ""; }
    bool FreeIn(object st) => StoryPhase(st) == "Free" && !PriyaRoomPlayer.Locked && !GameState.Paused;

    /// <summary>Walks up to an interaction spot (by its label), puts the crosshair on it and presses E.</summary>
    IEnumerator Use(object st, string label, float stop = 1.5f)
    {
        var list = Get(st, "spots") as System.Collections.IList;
        object spot = null;
        if (list != null) foreach (var s in list) { var l = Get(s, "label") as string; if (l != null && l.Contains(label)) { spot = s; break; } }
        Check(spot != null, "the spot '" + label + "' exists");
        if (spot == null) yield break;
        var t = Get(spot, "t") as Transform;
        Vector3 off = Get(spot, "offset") is Vector3 o ? o : Vector3.zero;
        yield return WaitFor(() => FreeIn(st), 60f, "control before '" + label + "'");
        Vector3 c = t.position + off;
        Vector3 from = player.transform.position;
        Vector3 d = c - from; d.y = 0;
        Vector3 goal = c - d.normalized * stop; goal.y = from.y;
        yield return WalkToIn(st, goal, 0.35f, 25f);
        Vector3 pd = c - player.transform.position; pd.y = 0;
        if (pd.magnitude > stop + 1.2f)
        {
            Note("walk to '" + label + "' stopped " + pd.magnitude.ToString("F1") + " m short: placed beside it");
            PlaceAt(new Vector3(goal.x, player.transform.position.y, goal.z), 0f, 0f);
        }
        yield return LookAtPoint(c, 80f);
        double p0 = Now;
        while (!(((string)Get(st, "prompt") ?? "").Contains(label)) && Now - p0 < 2f)
        {
            AngleTo(c, out float y, out float x); Steer(y, x, 60f, 60f);
            yield return null;
        }
        // occluded from where the walk ended (a shelf, a bookcase): stand at eight points round it until it can be seen
        for (int k = 0; k < 8 && !(((string)Get(st, "prompt") ?? "").Contains(label)); k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
            Vector3 at = c + dir * stop; at.y = player.transform.position.y;
            if (Physics.CheckCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            PlaceAt(at, 0f, 0f);
            yield return LookAtPoint(c, 200f);
            for (int i = 0; i < 10 && !(((string)Get(st, "prompt") ?? "").Contains(label)); i++) yield return null;
            if (((string)Get(st, "prompt") ?? "").Contains(label)) Note("'" + label + "' seen from the " + (k * 45) + " degree side");
        }
        Check(((string)Get(st, "prompt") ?? "").Contains(label), "the prompt '" + label + "' appears");
        Tap(Key.E);
        for (int i = 0; i < 4; i++) yield return null;
    }

    IEnumerator WalkToIn(object st, Vector3 goal, float stopDist, float timeout)
    {
        SyncLook();
        float t = 0, checkAt = 1.2f, dodge = 0f; int dodges = 0;
        Vector3 last = player.transform.position;
        while (t < timeout)
        {
            if (!FreeIn(st)) { ReleaseAll(); yield return null; t += Time.deltaTime; continue; }
            Vector3 d = goal - player.transform.position; d.y = 0;
            if (d.magnitude < stopDist) break;
            float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Steer(want, 3f, 140f, 40f);
            float off = Mathf.Abs(Mathf.DeltaAngle(yaw, want));
            Hold(Key.W, off < 55f);
            if (dodge > 0f) { dodge -= Time.deltaTime; Hold(dodges % 2 == 0 ? Key.D : Key.A, dodge > 0f); }
            if (t > checkAt)
            {
                if ((player.transform.position - last).magnitude < 0.3f && off < 55f) { dodge = 0.8f; dodges++; }
                if (dodges > 6) break;
                last = player.transform.position; checkAt = t + 1.2f;
            }
            t += Time.deltaTime;
            yield return null;
        }
        ReleaseAll();
    }

    IEnumerator CloseCard(object st, float read)
    {
        yield return WaitFor(() => GetBool(st, "cardOpen"), 30f, "a document card opens");
        for (float t = 0; t < read; t += Time.deltaTime) yield return null;
        Tap(Key.E);
        yield return WaitFor(() => !GetBool(st, "cardOpen"), 3f, "E closes the card");
    }

    IEnumerator TypeCode(object st, string code)
    {
        yield return WaitFor(() => GetBool(st, "codeOpen"), 40f, "the code entry opens");
        if (!GetBool(st, "codeOpen")) yield break;           // (a result from an earlier code must not count for this one)
        for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
        foreach (char ch in code) { InputSystem.QueueTextEvent(vk, ch); yield return null; yield return null; }
        for (float t = 0; t < 0.4f; t += Time.deltaTime) yield return null;
        Tap(Key.Enter);
        yield return WaitFor(() => !GetBool(st, "codeOpen"), 4f, "the code " + code + " opens it");
        Check(GetBool(st, "codeResult"), "the code " + code + " is accepted");
    }

    IEnumerator ActThree(bool fresh)
    {
        int wantPandit = int.Parse(Env("PINNACLE_ROUTE_PANDIT", "2"), CultureInfo.InvariantCulture);
        int wantBranch = int.Parse(Env("PINNACLE_ROUTE_BRANCH", "0"), CultureInfo.InvariantCulture);
        var d = SaveSystem.Data;
        d.actsCompleted = Mathf.Max(d.actsCompleted, 2);
        SaveSystem.ResetAct(3);
        d.hasUsb = true;
        if (!fresh) d.act3Stage = 2;
        SaveSystem.Save();
        tRequest = Now;
        Call(story, "OnMenuPlay", 3, !fresh);
        Act3Story a3 = null;
        while (a3 == null && Now - tRequest < 30f) { a3 = FindAnyObjectByType<Act3Story>(); yield return null; }
        Check(a3 != null, "Act III starts " + (fresh ? "fresh from chapter select" : "from a saved game at the temple"));
        if (a3 == null) yield break;
        section = "act3";
        if (fresh)
        {
            // the night guard lets Arjun in (lines are skipped with Space like a player in a hurry would not: they play out)
            yield return WaitFor(() => FreeIn(a3) && (((string)Get(a3, "objective")) ?? "").Contains("computer"), 120f, "the guard's lines end and the computer is the objective");
            yield return Use(a3, "Use the computer");
            yield return TypeCode(a3, "NANI1987");
            yield return CloseCard(a3, 2.5f);
            yield return WaitFor(() => GetBool(a3, "usbDone"), 60f, "Priya's files are read");
            yield return Use(a3, "Go down to the Record Room");
            yield return WaitFor(() => (Get(a3, "area") is int ar && ar == 1) && FreeIn(a3), 30f, "down in the Record Room");
            if (shots) yield return StillShot("act3_basement");
            yield return Use(a3, "Inspect the cage", 1.6f);
            yield return WaitFor(() => GetBool(a3, "cageSeen") && FreeIn(a3), 30f, "the cage is locked");
            yield return Use(a3, "Look at the calendar", 1.4f);
            yield return CloseCard(a3, 2.5f);
            yield return WaitFor(() => GetBool(a3, "calendarSeen") && FreeIn(a3), 30f, "the calendar is read");
            yield return Use(a3, "Go back upstairs");
            yield return WaitFor(() => (Get(a3, "area") is int ar2 && ar2 == 0) && FreeIn(a3), 30f, "back in the reading room");
            yield return Use(a3, "Try the desk drawer", 1.3f);
            yield return TypeCode(a3, "7491");
            yield return WaitFor(() => GetBool(a3, "keyFound") && FreeIn(a3), 30f, "the cage key is found");
            yield return Use(a3, "Go down to the Record Room");
            yield return WaitFor(() => (Get(a3, "area") is int ar3 && ar3 == 1) && FreeIn(a3), 30f, "down again");
            yield return Use(a3, "Open the cage with the key", 1.6f);
            yield return CloseCard(a3, 2.5f);
            yield return WaitFor(() => GetBool(a3, "cageOpen"), 30f, "the cage is opened and the sign-out sheet read");
        }
        // ---- the temple
        yield return WaitFor(() => (Get(a3, "area") is int ar4 && ar4 == 2), 60f, "Arjun reaches the Kali Temple");
        yield return WaitFor(() => FreeIn(a3) && (((string)Get(a3, "objective")) ?? "").Contains("priest"), 60f, "the priest is the objective");
        if (shots) yield return StillShot("act3_temple");
        var pandit = Get(a3, "pandit") as CharacterNPC;
        Segment("pandit", true);
        section = "pandit";
        yield return Use(a3, "Talk to Pandit Harishankar", 1.7f);
        yield return WaitFor(() => StoryPhase(a3) == "Busy", 3f, "E starts the Pandit's talk");
        yield return Dial(a3, wantPandit, "the Pandit's question", pandit);
        int got = Get(a3, "menuResult") is int mr ? mr : -9;
        Check(got == wantPandit, "the Pandit's question answered with index " + got);
        yield return WaitFor(() => pandit != null && pandit.IsTalking, 20f, "the Pandit answers back");
        for (float t = 0; t < 3.5f; t += Time.deltaTime) yield return null;
        Segment("pandit", false);
        yield return CloseCard(a3, 3f);
        for (int i = 0; i < 60 && SaveSystem.Data.act3Stage < 3; i++) yield return null;      // saved as the letter closes
        Check(SaveSystem.Data.panditAnswer == wantPandit && SaveSystem.Data.act3Stage == 3, "the answer and the envelope are saved (answer " + SaveSystem.Data.panditAnswer + ", stage " + SaveSystem.Data.act3Stage + ")");
        // ---- the gate, the call, the crossroads
        section = "gate";
        var gate = Get(a3, "gate") as Transform;
        yield return WaitFor(() => FreeIn(a3), 20f, "control after the letter");
        if (gate != null) yield return WalkToIn(a3, gate.position, 2.4f, 40f);
        yield return CloseCard(a3, 2f);                     // Kamala's text message
        Segment("crossroads", true);
        section = "crossroads";
        yield return Dial(a3, wantBranch, "the crossroads", null);
        var prev = CompassOf(a3) != null ? Get(CompassOf(a3), "replies") as string[] : null;
        if (prev != null) Check(prev.Length == 3 && prev[0].Contains("Café Irani") && prev[1].Contains("Kapoor house") && prev[2].Contains("Inspector Reddy"), "the crossroads previews name each destination");
        for (int i = 0; i < 60 && SaveSystem.Data.branch < 0; i++) yield return null;           // saved once the dial has closed
        Check(SaveSystem.Data.branch == wantBranch, "the branch is saved as " + SaveSystem.Data.branch);
        for (float t = 0; t < 2.5f; t += Time.deltaTime) yield return null;
        Segment("crossroads", false);
        // ---- the Curator, then Act IV at the chosen place
        section = "curator2";
        CuratorInterlude inter = null;
        double i0 = Now;
        while (inter == null && Now - i0 < 60f) { inter = FindAnyObjectByType<CuratorInterlude>(); yield return null; }
        Check(inter != null, "the Curator's visit follows Act III");
        for (float t = 0; t < 5f && inter != null; t += Time.deltaTime) yield return null;
        Tap(Key.Enter);
        Act4Story a4 = null;
        double a0 = Now;
        while (a4 == null && Now - a0 < 90f) { a4 = FindAnyObjectByType<Act4Story>(); yield return null; }
        Check(a4 != null, "Act IV loads after the Curator");
        if (a4 == null) yield break;
        int variant = Get(a4, "variant") is int v ? v : -9;
        string sub = Call(a4, "get_ActSubtitle") as string;
        string[] where = { "Café Irani", "Kapoor house", "Police Station" };
        Check(variant == wantBranch && sub != null && sub.Contains(where[wantBranch]), "Act IV opens at the chosen place: variant " + variant + ", \"" + sub + "\"");
        if (wantBranch == 0) yield return Cafe(false);
    }

    // ================================================================== Act IV: Café Irani
    IEnumerator Cafe(bool fromMenu)
    {
        Act4Story a4;
        if (fromMenu)
        {
            var d = SaveSystem.Data;
            d.actsCompleted = Mathf.Max(d.actsCompleted, 3);
            d.branch = 0;
            SaveSystem.Save();
            section = "entry";
            tRequest = Now;
            StartSampling();
            Call(story, "OnMenuPlay", 4, false);
            Stamp("request");
        }
        a4 = null;
        while (a4 == null) { a4 = FindAnyObjectByType<Act4Story>(); yield return null; }
        double t0 = Now;
        while (!FreeIn(a4) && Now - t0 < 60f)
        {
            var f = Get(a4, "fade");
            if (f is float ff && ff < 0.5f) Stamp("first_visible");
            yield return null;
        }
        Stamp("first_control");
        Check(FreeIn(a4), "the café hands over control");
        Log("café entry: control after " + (stamps.ContainsKey("first_control") ? stamps["first_control"].ToString("F2") : "?") + " s");
        if (census) Census(a4.transform, "census_cafe.txt");
        var owner = Get(a4, "owner") as CharacterNPC;
        var raj = Get(a4, "raj") as CharacterNPC;
        Transform counter = FindDeep(a4.transform, "Counter");
        Transform caseT = FindDeep(a4.transform, "DisplayCase");
        Vector3 cpos = counter != null ? counter.position : new Vector3(3.3f, 0.5f, -2.8f);
        Vector3 casePos = caseT != null ? caseT.position : cpos + Vector3.up * 0.7f;
        Note("counter at " + cpos.ToString("F2") + ", case at " + casePos.ToString("F2") + ", owner at " + (owner != null ? owner.transform.position.ToString("F2") : "-"));
        // ---- the approach: from the entrance to the counter (the test: walking up to the counter)
        section = "cafe_approach";
        Segment("cafe", true);
        yield return LookHold(cpos + Vector3.up * 0.9f, 80f, 0.8f);
        if (shots) yield return StillShot("cafe_01_entrance");
        Vector3 front = cpos + new Vector3(-0.2f, 0f, 1.45f);
        yield return WalkToIn(a4, new Vector3(front.x, 0f, front.z), 0.35f, 20f);
        yield return LookHold(casePos + Vector3.up * 0.05f, 60f, 1.5f);
        if (shots) yield return StillShot("cafe_02_counter");
        if (owner != null) yield return LookHold(HeadOf(owner).position, 50f, 2.5f);
        if (shots) yield return StillShot("cafe_03_owner");
        // ---- the street: from the counter, the terrace and the entrance
        section = "cafe_street";
        yield return TurnSteady(-150f, 70f);
        for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
        if (shots) yield return StillShot("cafe_04_street_from_counter");
        yield return WalkToIn(a4, new Vector3(-3.6f, 0f, -0.2f), 0.4f, 20f);
        yield return LookHold(new Vector3(-14f, 2.2f, -1f), 60f, 2f);
        if (shots) yield return StillShot("cafe_05_street_from_terrace");
        yield return TurnSteady(70f, 40f);
        yield return TurnSteady(-120f, 40f);
        Segment("cafe", false);
        // ---- stills from fixed places: the case from the side, the street from behind the owner
        section = "cafe_views";
        if (caseT != null)
        {
            PlaceAt(new Vector3(casePos.x - 1.55f, 0.08f, casePos.z + 0.35f), 0f, 0f);
            yield return LookAtPoint(casePos, 200f);
            for (int i = 0; i < 20; i++) yield return null;
            if (shots) yield return StillShot("cafe_06_case_side");
        }
        if (owner != null)
        {
            var op = owner.transform.position;
            PlaceAt(new Vector3(op.x + 0.35f, 0.08f, op.z - 0.55f), 0f, 0f);
            yield return LookAtPoint(new Vector3(-12f, 1.8f, 2f), 200f);
            for (int i = 0; i < 20; i++) yield return null;
            if (shots) yield return StillShot("cafe_07_outward_behind_owner");
        }
        // ---- the owner's task: hands on the cup, and the cup left standing on the counter
        var task = Get(a4, "taskCup") as Transform;
        if (task != null)
        {
            float worst = 0f; int holding = 0; double w0 = Now;
            while (Now - w0 < 14f)
            {
                if (GetBool(a4, "taskHolding"))
                {
                    holding++;
                    worst = Mathf.Max(worst, Vector3.Distance(owner.HandPosition(1), task.position) - 0.06f);
                }
                yield return null;
            }
            float grip = Get(a4, "taskGripError") is float ge ? ge : 99f;
            Check(holding > 0 && grip < 0.03f, "the owner's hand stays on the cup while carrying it (" + holding + " frames held, wrist at most " + (grip * 100f).ToString("F1") + " cm off its grip; wrist-to-cup " + ((worst + 0.06f) * 100f).ToString("F1") + " cm)");
            float top = counter != null ? counter.position.y : 0.5f;
            // the window can end mid-carry (the cup 6 cm up in his hand): judge the cup once he has set it down
            for (double r0 = Now; GetBool(a4, "taskHolding") && Now - r0 < 6.0;) yield return null;
            Check(!GetBool(a4, "taskHolding") && Mathf.Abs(task.position.y - (Get(a4, "counterTopY") is float cty ? cty : task.position.y)) < 0.01f, "the cup is standing on the counter after release (y " + task.position.y.ToString("F3") + ")");
        }
        else Note("no owner task in this build");
        sampling = false;
        WriteTiming();
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

    /// <summary>A still once the view has stopped moving (camera-only motion blur smeared stills taken while the player
    /// was still settling after a turn or a placement): up to 2 s for 12 frames without movement, then the capture.</summary>
    IEnumerator StillShot(string name)
    {
        Transform c = player != null ? player.cam : null;
        int calm = 0; double t0 = Now;
        Vector3 lastP = c != null ? c.position : Vector3.zero; Quaternion lastR = c != null ? c.rotation : Quaternion.identity;
        while (c != null && calm < 12 && Now - t0 < 2.0)
        {
            yield return null;
            bool still = (c.position - lastP).sqrMagnitude < 1e-6f && Quaternion.Angle(c.rotation, lastR) < 0.02f;
            calm = still ? calm + 1 : 0;
            lastP = c.position; lastR = c.rotation;
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
        if (calm < 12) Note("still " + name + " taken while the view was still moving");
        // the capture is taken at the end of this frame: the route must not move the view again until it has been
        yield return null;
    }

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

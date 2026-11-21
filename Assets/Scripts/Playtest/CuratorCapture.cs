using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Records the Curator scenes as footage with sound, for review and before/after comparison.
/// Does nothing unless PINNACLE_CAPTURE is set (a comma list of segments: prologue, int1, int2, int3, gallery,
/// verdictJ, verdictS, verdictR). Run it like the play-test (Docs/PLAYTEST.md), on a copy of the project:
///   PINNACLE_CAPTURE=prologue,int1 PINNACLE_CAPTURE_OUT=/tmp/cap PINNACLE_SAVE_DIR=/tmp/saves Unity -batchmode ...
///     -executeMethod PlaytestHarness.Run
/// Time runs at a fixed 30 frames per second (Time.captureFramerate) and the audio is rendered offline in step with
/// it (AudioRenderer), so picture and sound stay in sync however slowly the frames are drawn. That also means the
/// footage says nothing about frame rate: performance is measured separately, in a player build, in real time.
/// Per segment it writes: frames (JPEG), audio (WAV), lines.srt (subtitles by line id) and track.csv (per frame:
/// the Curator's feet, hands, head, props and the camera), which Tools/review_capture.py turns into numbers.
/// </summary>
[DefaultExecutionOrder(30000)]          // after everything else has moved the camera and the bones
public class CuratorCapture : MonoBehaviour
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const int Fps = 30;

    string outDir, segDir, seg;
    int width = 1280, height = 720, frame;
    bool recording;
    Camera cam;
    RenderTexture rt; Texture2D readback;
    BinaryWriter wav; long wavSamples; int channels, sampleRate;
    StringBuilder csv, srt; int srtIndex; string curLine; int lineStartFrame;
    readonly StringBuilder log = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PINNACLE_CAPTURE"))) return;
        var go = new GameObject("CuratorCapture");
        DontDestroyOnLoad(go);
        go.AddComponent<CuratorCapture>();
    }

    void Start()
    {
        outDir = Environment.GetEnvironmentVariable("PINNACLE_CAPTURE_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.temporaryCachePath, "capture");
        Directory.CreateDirectory(outDir);
        string res = Environment.GetEnvironmentVariable("PINNACLE_CAPTURE_RES");
        if (!string.IsNullOrEmpty(res) && res.Contains("x"))
        {
            var p = res.Split('x'); int.TryParse(p[0], out width); int.TryParse(p[1], out height);
        }
        StartCoroutine(Run(Environment.GetEnvironmentVariable("PINNACLE_CAPTURE").Split(',')));
    }

    // ------------------------------------------------------------------ reflection helpers
    static FieldInfo FieldOf(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f; }
        return null;
    }
    static object Get(object o, string n) { if (o == null) return null; var f = FieldOf(o.GetType(), n); return f != null ? f.GetValue(o) : null; }
    static void Set(object o, string n, object v) { var f = FieldOf(o.GetType(), n); if (f != null) f.SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
            foreach (var m in t.GetMethods(Any))
                if (m.Name == n && m.GetParameters().Length == a.Length) return m.Invoke(o, a);
        throw new MissingMethodException(o.GetType().Name + "." + n);
    }
    static Transform FindDeep(Transform t, string n)
    {
        if (t == null) return null;
        if (t.name == n) return t;
        for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), n); if (r != null) return r; }
        return null;
    }
    void Log(string s) { log.AppendLine(s); Debug.Log("[CAPTURE] " + s); }

    // ------------------------------------------------------------------ the run
    IEnumerator Run(string[] segments)
    {
        Time.captureFramerate = Fps;
        PriyaRoomBuilder builder = null; PriyaRoomStory story = null;
        for (int i = 0; i < 600 && (builder == null || story == null || builder.PlayerCtrl == null); i++)
        {
            builder = FindAnyObjectByType<PriyaRoomBuilder>(); story = FindAnyObjectByType<PriyaRoomStory>();
            yield return null;
        }
        if (builder == null || story == null) { Log("game did not boot"); Quit(1); yield break; }
        for (int i = 0; i < 30; i++) yield return null;
        var player = builder.PlayerCtrl;
        cam = player.cam.GetComponent<Camera>();
        SaveSystem.Profile.playerName = "Tester";
        Log("Capture " + DateTime.Now + "  Unity " + Application.unityVersion + "  " + SystemInfo.graphicsDeviceName + "  " + width + "x" + height + " @" + Fps);

        foreach (var raw in segments)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;
            if (s.StartsWith("lab_"))
            {
                // a test bench: lab_walk, lab_hands, lab_face, lab_storm (scene 0 for hands, scene 1 for the rest)
                var lab = typeof(CuratorInterlude).GetField("Lab", BindingFlags.Static | BindingFlags.Public);
                if (lab == null) { Log(s + ": this build has no test benches"); continue; }
                lab.SetValue(null, s.Substring(4));
                yield return Interlude(s == "lab_hands" ? 0 : 1, builder, player, story, s);
                lab.SetValue(null, null);
            }
            else if (s == "prologue") yield return Prologue(story);
            else if (s.StartsWith("int")) yield return Interlude(int.Parse(s.Substring(3)), builder, player, story, s);
            else if (s == "gallery") yield return Gallery(builder, player, story, null);
            else if (s.StartsWith("verdict")) yield return Gallery(builder, player, story, s.Substring(7));
        }
        File.WriteAllText(Path.Combine(outDir, "capture_log.txt"), log.ToString());
        Quit(0);
    }

    void Quit(int code)
    {
        Time.captureFramerate = 0;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }

    static void SilenceAllBut(Transform keep)
    {
        foreach (var a in FindObjectsByType<AudioSource>())
            if (keep == null || !a.transform.IsChildOf(keep)) a.Stop();
    }

    IEnumerator Prologue(PriyaRoomStory story)
    {
        Call(story, "OnMenuPlay", 1, false);
        yield return null;
        var inter = FindAnyObjectByType<CuratorInterlude>();
        if (inter == null) { Log("prologue did not start"); yield break; }
        Begin("prologue", inter);
        for (float t = 0; t < 320f && inter != null; t += Time.deltaTime) yield return null;
        End();
        Log("prologue ended " + (inter == null ? "normally" : "by the time limit"));
        story.enabled = false;
        SilenceAllBut(null);
        for (int i = 0; i < 10; i++) yield return null;
    }

    IEnumerator Interlude(int scene, PriyaRoomBuilder builder, PriyaRoomPlayer player, PriyaRoomStory story, string name)
    {
        story.enabled = false;
        SilenceAllBut(null);
        // branch coverage: the swap lines need a priest's answer (scene 2) and an ally (scene 3)
        var d = SaveSystem.Data;
        if (scene == 2) d.panditAnswer = 2;
        if (scene == 3) { d.branch = 2; d.allyReddy = true; }
        bool done = false; int calls = 0;
        var inter = CuratorInterlude.Play(scene, builder, player, () => { done = true; calls++; });
        yield return null;
        Begin(name, inter);
        for (float t = 0; t < 260f && !done; t += Time.deltaTime) yield return null;
        End();
        Log(name + " ended " + (done ? "normally" : "by the time limit") + ", exit callback ran " + calls + " time(s)");
        for (int i = 0; i < 10; i++) yield return null;
    }

    IEnumerator Gallery(PriyaRoomBuilder builder, PriyaRoomPlayer player, PriyaRoomStory story, string verdict)
    {
        story.enabled = false;
        SilenceAllBut(null);
        // (ActLoader moves the player out of the previous root before freeing it; destroying that root here took the
        // player's camera and audio listener with it)
        SaveSystem.Data.act5Stage = 1;
        ActLoader.Load(5, builder, player, true);
        Act5Story act = null;
        for (int i = 0; i < 120 && act == null; i++) { act = FindAnyObjectByType<Act5Story>(); yield return null; }
        if (act == null) { Log("Act V did not load"); yield break; }
        if (verdict == null)
        {
            Begin("gallery", act);
            // until the player is handed control at the caskets
            for (float t = 0; t < 150f && Get(act, "phase").ToString() != "Free"; t += Time.deltaTime) yield return null;
            for (float t = 0; t < 3f; t += Time.deltaTime) yield return null;
            End();
            Log("gallery reached the casket choice: " + (Get(act, "phase").ToString() == "Free"));
        }
        else
        {
            // straight to the Curator's verdict for one ending (the hall as it is after the ending's epilogue)
            for (float t = 0; t < 8f; t += Time.deltaTime) yield return null;
            act.StopAllCoroutines();
            Call(act, "BeginBusy");
            string[] ids; string title; bool dark = false;
            if (verdict == "S") { ids = new[] { "endS_01", "endS_02", "endS_03", "endS_04", "endS_05" }; title = "CHANDNIPUR  -  SACRIFICE"; dark = true; }
            else if (verdict == "R") { ids = new[] { "endR_01", "endR_02", "endR_03", "endR_04", "endR_05" }; title = "CHANDNIPUR  -  REVOLUTION"; }
            else { ids = new[] { "endJ_01", "endJ_02", "endJ_03", "endJ_04", "endJ_05" }; title = "CHANDNIPUR  -  JUSTICE"; }
            var co = (IEnumerator)Call(act, "BackToGallery", title, ids, dark);
            bool fin = false;
            act.StartCoroutine(Wrap(co, () => fin = true));
            Begin("verdict" + verdict, act);
            for (float t = 0; t < 120f && !fin; t += Time.deltaTime) yield return null;
            End();
            Log("verdict" + verdict + " finished: " + fin);
        }
        // leave the act standing: ActLoader frees it (after taking the player out of it) when the next segment loads
        // Act V again. Destroying it here took the player with it (he lives under the act's root), so every segment
        // after the first ending failed
        act.StopAllCoroutines(); act.enabled = false;
        SilenceAllBut(null);
        for (int i = 0; i < 10; i++) yield return null;
    }

    static IEnumerator Wrap(IEnumerator inner, Action after) { yield return inner; after(); }

    // ------------------------------------------------------------------ recording
    MonoBehaviour scene;
    Transform curRoot, footL, footR, toeL, toeR, handL, handR, headB, eyeL, eyeR, jaw, pelvis;

    void Begin(string name, MonoBehaviour sceneScript)
    {
        seg = name; scene = sceneScript; frame = 0;
        segDir = Path.Combine(outDir, name);
        Directory.CreateDirectory(segDir);
        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        readback = new Texture2D(width, height, TextureFormat.RGB24, false);
        channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        sampleRate = AudioSettings.outputSampleRate;
        wav = new BinaryWriter(File.Create(Path.Combine(segDir, name + ".wav")));
        WriteWavHeader(wav, 0, channels, sampleRate);
        wavSamples = 0;
        bool ok = AudioRenderer.Start();
        Log(name + ": recording (audio renderer " + (ok ? "on" : "FAILED") + ", " + channels + " ch @ " + sampleRate + " Hz)");
        csv = new StringBuilder("frame,time,line,rootx,rooty,rootz,yaw,footLx,footLy,footLz,footRx,footRy,footRz,toeLx,toeLy,toeLz,toeRx,toeRy,toeRz,handLx,handLy,handLz,handRx,handRy,handRz,headx,heady,headz,candlex,candley,candlez,bookx,booky,bookz,camx,camy,camz,fov,jaw,pelvisx,pelvisy,pelvisz,hero\n");
        srt = new StringBuilder(); srtIndex = 0; curLine = null;
        curRoot = null;
        // render the bones as they are now: a camera rendered by hand would otherwise skin with last frame's matrices
        foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>()) smr.forceMatrixRecalculationPerRender = true;
        recording = true;
    }

    void BindCurator()
    {
        var npc = Get(scene, "curator") as CharacterNPC;
        if (npc == null || npc.transform == curRoot) return;
        curRoot = npc.transform;
        footL = FindDeep(curRoot, "Bip01 L Foot"); footR = FindDeep(curRoot, "Bip01 R Foot");
        toeL = FindDeep(curRoot, "Bip01 L Toe0"); toeR = FindDeep(curRoot, "Bip01 R Toe0");
        handL = FindDeep(curRoot, "Bip01 L Hand"); handR = FindDeep(curRoot, "Bip01 R Hand");
        headB = FindDeep(curRoot, "Bip01 Head"); jaw = FindDeep(curRoot, "Bip01 MJaw"); pelvis = FindDeep(curRoot, "Bip01 Pelvis");
    }

    void LateUpdate()
    {
        if (!recording) return;
        if (wav == null || csv == null)        // the scripts were reloaded mid-recording (a recompile during Play)
        {
            recording = false;
            Log("recording of " + seg + " stopped at frame " + frame + ": scripts reloaded during Play");
            return;
        }
        // audio for this frame (offline, in step with the fixed frame time)
        int n = AudioRenderer.GetSampleCountForCaptureFrame();
        if (n > 0)
        {
            var buf = new NativeArray<float>(n * channels, Allocator.Temp);
            AudioRenderer.Render(buf);
            for (int i = 0; i < buf.Length; i++) wav.Write((short)Mathf.Clamp(Mathf.RoundToInt(buf[i] * 32767f), -32768, 32767));
            wavSamples += n;
            buf.Dispose();
        }
        // picture
        if (cam != null && cam.isActiveAndEnabled)
        {
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0); readback.Apply(false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(segDir, "f_" + frame.ToString("00000") + ".jpg"), readback.EncodeToJPG(85));
        }
        // subtitles, by line id
        string line = CurrentLine();
        if (line != curLine)
        {
            if (curLine != null) Srt(lineStartFrame, frame, curLine);
            curLine = line; lineStartFrame = frame;
        }
        // tracking data
        if (scene != null) BindCurator();
        if (frame % 30 == 0) foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>()) smr.forceMatrixRecalculationPerRender = true;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        csv.Append(frame).Append(',').Append((frame / (float)Fps).ToString("F4", inv)).Append(',').Append((line ?? "").Replace(',', ';').Replace('\n', ' '));
        Vec(curRoot); csv.Append(',').Append(curRoot != null ? curRoot.eulerAngles.y.ToString("F2", inv) : "");
        Vec(footL); Vec(footR); Vec(toeL); Vec(toeR); Vec(handL); Vec(handR); Vec(headB);
        Vec(Get(scene, "candle") as Transform);
        var book = Get(scene, "book");
        Vec(book != null ? Get(book, "root") as Transform : Get(scene, "book") as Transform);
        Vec(cam != null ? cam.transform : null);
        csv.Append(',').Append(cam != null ? cam.fieldOfView.ToString("F2", inv) : "");
        csv.Append(',').Append(jaw != null ? jaw.localEulerAngles.ToString("F2").Replace(',', ' ') : "");
        Vec(pelvis);
        var npc = Get(scene, "curator") as CharacterNPC;
        var dbg = npc != null ? typeof(CharacterNPC).GetMethod("HeroDebug") : null;        // (absent in older builds of the game)
        csv.Append(',').Append(dbg != null ? dbg.Invoke(npc, null) as string : "");
        csv.Append('\n');
        frame++;
    }

    void Vec(Transform t)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (t == null) { csv.Append(",,,"); return; }
        Vector3 p = t.position;
        csv.Append(',').Append(p.x.ToString("F4", inv)).Append(',').Append(p.y.ToString("F4", inv)).Append(',').Append(p.z.ToString("F4", inv));
    }

    string CurrentLine()
    {
        if (scene == null) return null;
        string text = Get(scene, "sub") as string;
        if (string.IsNullOrEmpty(text)) text = Get(scene, "dlgText") as string;
        if (string.IsNullOrEmpty(text)) return null;
        return text;
    }

    void Srt(int f0, int f1, string text)
    {
        srtIndex++;
        srt.Append(srtIndex).Append('\n').Append(Tc(f0)).Append(" --> ").Append(Tc(f1)).Append('\n').Append(text).Append("\n\n");
    }

    static string Tc(int f)
    {
        double s = f / (double)Fps;
        var ts = TimeSpan.FromSeconds(s);
        return string.Format("{0:00}:{1:00}:{2:00},{3:000}", (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
    }

    void End()
    {
        if (!recording) return;
        recording = false;
        if (curLine != null) Srt(lineStartFrame, frame, curLine);
        AudioRenderer.Stop();
        wav.Seek(0, SeekOrigin.Begin);
        WriteWavHeader(wav, wavSamples, channels, sampleRate);
        wav.Close();
        File.WriteAllText(Path.Combine(segDir, "track.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(segDir, "lines.srt"), srt.ToString());
        rt.Release(); Destroy(rt); Destroy(readback);
        Log(seg + ": " + frame + " frames (" + (frame / (float)Fps).ToString("F1") + " s), " + wavSamples + " audio samples (" + (wavSamples / (float)sampleRate).ToString("F1") + " s)");
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

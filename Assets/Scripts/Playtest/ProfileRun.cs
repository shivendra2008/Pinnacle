using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Real-time performance of the Curator prologue in a player build (not the Editor, not batch mode, not the fixed-step
/// capture). Does nothing unless PINNACLE_PROFILE is set. Launch the built app's binary directly so the environment
/// reaches it, at the resolution to measure:
///   PINNACLE_PROFILE=1 PINNACLE_PROFILE_OUT=/tmp/prof PINNACLE_SAVE_DIR=/tmp/prof/saves \
///   Pinnacle.app/Contents/MacOS/Pinnacle -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
/// (PINNACLE_SAVE_DIR keeps the new game it starts away from the real saves.)
/// It starts a new game (the prologue), records every frame's time (wall clock) and, where the platform reports them,
/// CPU main-thread, render-thread and GPU times (FrameTimingManager), labels each frame with what is on screen
/// (walking, a line of dialogue, the lightning), takes screenshots with the UI at a few beats, then quits.
/// Writes frames.csv and summary.txt (mean, median, 95th and 99th percentile, worst, by section).
/// </summary>
public class ProfileRun : MonoBehaviour
{
    string outDir;
    readonly List<float> frame = new List<float>(), cpu = new List<float>(), gpu = new List<float>(), main = new List<float>(), render = new List<float>();
    readonly List<string> label = new List<string>();
    readonly FrameTiming[] ft = new FrameTiming[1];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PINNACLE_PROFILE"))) return;
        // the player pauses when its window loses focus (Run In Background is off); a measurement must not
        Application.runInBackground = true;
        var go = new GameObject("ProfileRun"); DontDestroyOnLoad(go); go.AddComponent<ProfileRun>();
    }

    IEnumerator Start()
    {
        outDir = Environment.GetEnvironmentVariable("PINNACLE_PROFILE_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.persistentDataPath, "profile");
        Directory.CreateDirectory(outDir);
        PriyaRoomStory story = null;
        for (int i = 0; i < 600 && story == null; i++) { story = FindAnyObjectByType<PriyaRoomStory>(); yield return null; }
        for (int i = 0; i < 60; i++) yield return null;
        if (story == null) { File.WriteAllText(Path.Combine(outDir, "summary.txt"), "the game did not boot"); Application.Quit(1); yield break; }
        SaveSystem.Profile.playerName = "Profiler";
        typeof(PriyaRoomStory).GetMethod("OnMenuPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(story, new object[] { 1, false });
        CuratorInterlude inter = null;
        for (int i = 0; i < 30 && inter == null; i++) { inter = FindAnyObjectByType<CuratorInterlude>(); yield return null; }
        int shots = 0; float t0 = Time.realtimeSinceStartup;
        float[] screenshotAt = { 8f, 30f, 45f, 75f, 110f, 140f };
        while (inter != null && Time.realtimeSinceStartup - t0 < 300f)
        {
            yield return null;
            FrameTimingManager.CaptureFrameTimings();
            uint n = FrameTimingManager.GetLatestTimings(1, ft);
            frame.Add(Time.unscaledDeltaTime * 1000f);
            cpu.Add(n > 0 ? (float)ft[0].cpuFrameTime : -1f);
            main.Add(n > 0 ? (float)ft[0].cpuMainThreadFrameTime : -1f);
            render.Add(n > 0 ? (float)ft[0].cpuRenderThreadFrameTime : -1f);
            gpu.Add(n > 0 ? (float)ft[0].gpuFrameTime : -1f);
            label.Add(Label(inter));
            float el = Time.realtimeSinceStartup - t0;
            if (shots < screenshotAt.Length && el > screenshotAt[shots]) { ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "screen_" + shots + ".png")); shots++; }
        }
        Write();
        Application.Quit(0);
    }

    static string Label(CuratorInterlude inter)
    {
        var storm = GalleryStorm.Active;
        if (storm != null && storm.Flash > 0.05f) return "lightning";
        if (!string.IsNullOrEmpty(inter.LineNow)) return "dialogue";
        var npc = inter.GetType().GetField("curator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inter) as CharacterNPC;
        if (npc != null && npc.Speed > 0.2f) return "walking";
        return "other";
    }

    static string Stats(List<float> v)
    {
        var s = new List<float>(); foreach (var x in v) if (x >= 0f) s.Add(x);
        if (s.Count == 0) return "not reported";
        s.Sort(); float sum = 0; foreach (var x in s) sum += x;
        return string.Format("mean {0,6:F2}  median {1,6:F2}  p95 {2,6:F2}  p99 {3,6:F2}  worst {4,6:F2} ms", sum / s.Count, s[s.Count / 2], s[(int)(s.Count * 0.95f)], s[Mathf.Min(s.Count - 1, (int)(s.Count * 0.99f))], s[s.Count - 1]);
    }

    void Write()
    {
        var csv = new StringBuilder("frame_ms,cpu_ms,main_ms,render_ms,gpu_ms,section\n");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < frame.Count; i++)
            csv.Append(frame[i].ToString("F3", inv)).Append(',').Append(cpu[i].ToString("F3", inv)).Append(',').Append(main[i].ToString("F3", inv)).Append(',')
               .Append(render[i].ToString("F3", inv)).Append(',').Append(gpu[i].ToString("F3", inv)).Append(',').Append(label[i]).Append('\n');
        File.WriteAllText(Path.Combine(outDir, "frames.csv"), csv.ToString());
        var sb = new StringBuilder();
        sb.AppendLine("Curator prologue, real time, player build");
        sb.AppendLine(string.Format("{0} | {1} | {2} | {3}x{4} {5} | quality {6} | graphics tier {7} | vSync {8}", SystemInfo.deviceModel, SystemInfo.processorType, SystemInfo.graphicsDeviceName,
            Screen.width, Screen.height, Screen.fullScreenMode, QualitySettings.names[QualitySettings.GetQualityLevel()], LookController.Tier, QualitySettings.vSyncCount));
        sb.AppendLine(frame.Count + " frames");
        sb.AppendLine("frame (wall clock)  " + Stats(frame));
        sb.AppendLine("CPU frame           " + Stats(cpu));
        sb.AppendLine("CPU main thread     " + Stats(main));
        sb.AppendLine("CPU render thread   " + Stats(render));
        sb.AppendLine("GPU frame           " + Stats(gpu));
        foreach (var sec in new[] { "walking", "dialogue", "lightning", "other" })
        {
            var f = new List<float>(); var g = new List<float>();
            for (int i = 0; i < frame.Count; i++) if (label[i] == sec) { f.Add(frame[i]); g.Add(gpu[i]); }
            sb.AppendLine(string.Format("{0,-10} {1,5} frames  frame {2}", sec, f.Count, Stats(f)));
            sb.AppendLine(string.Format("{0,-10} {1,5}         GPU   {2}", "", "", Stats(g)));
        }
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), sb.ToString());
    }
}

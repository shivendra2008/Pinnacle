using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Opt-in wall-clock frame sampling. F8 starts/stops a section in the market, in either build.</summary>
public sealed class MarketFrameCapture : MonoBehaviour
{
    readonly List<double> frames = new List<double>(16384);
    MarketStory market;
    double previous;
    bool recording;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Environment.GetEnvironmentVariable("PINNACLE_MARKET_PROFILE") != "1") return;
        var go = new GameObject("Market frame capture");
        DontDestroyOnLoad(go);
        go.AddComponent<MarketFrameCapture>();
    }

    void LateUpdate()
    {
        bool toggle = Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
        double now = Time.realtimeSinceStartupAsDouble;
        if (recording)
        {
            if (market == null || GameState.Paused || !Application.isFocused) { Finish(); return; }
            frames.Add((now - previous) * 1000.0);
            previous = now;
            if (toggle) Finish();
        }
        else if (toggle && !GameState.Paused)
        {
            market = FindFirstObjectByType<MarketStory>();
            if (market == null) return;
            frames.Clear();
            previous = now;
            recording = true;
            Debug.Log("Market frame capture started. Press F8 at the end of the route.");
        }
    }

    void OnApplicationQuit() { if (recording) Finish(); }

    void Finish()
    {
        recording = false;
        if (frames.Count == 0) return;
        try
        {
            frames.Sort();
            double Percentile(double fraction) => frames[(int)Math.Ceiling(frames.Count * fraction) - 1];
            int longFrames = frames.Count - frames.FindLastIndex(v => v <= 50.0) - 1;
            string summary = string.Format(CultureInfo.InvariantCulture,
                "{0} | {1} | {2}x{3} | Unity {4} | {5} | tier {6} | vSync {7}\n" +
                "{8} frames | median {9:F2} ms | p95 {10:F2} ms | p99 {11:F2} ms | worst {12:F2} ms | over 50 ms {13}\n",
                SystemInfo.deviceModel, SystemInfo.graphicsDeviceName, Screen.width, Screen.height, Application.unityVersion,
                QualitySettings.names[QualitySettings.GetQualityLevel()], LookController.Tier, QualitySettings.vSyncCount,
                frames.Count, Percentile(0.5), Percentile(0.95), Percentile(0.99), frames[frames.Count - 1], longFrames);
            string directory = Path.Combine(Application.persistentDataPath, "market-profile");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".txt");
            File.WriteAllText(path, summary);
            Debug.Log(summary + path);
        }
        catch (Exception ex) { Debug.LogWarning("Could not save market frame capture: " + ex.Message); }
    }
}

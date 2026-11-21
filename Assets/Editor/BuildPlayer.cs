using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Standalone macOS build, from the command line (on a copy of the project; the open Editor locks this one):
///   PINNACLE_BUILD_OUT=/tmp/build Unity -batchmode -projectPath &lt;copy&gt; -executeMethod BuildPlayer.MacOS -logFile build.log
/// Before building it makes sure the shader variants the game creates at run time are in the build: materials are
/// made in code (URP Lit with normal maps, alpha clipping, smoothness maps, emission; transparent Unlit and particle
/// materials), and a build only keeps variants something in it uses. One material per variant is kept in
/// Assets/Resources/ShaderVariants (made here if missing) so they are always included.
/// Frame timing stats are switched on so the profiling run (Assets/Scripts/Playtest/ProfileRun.cs) can read the GPU time.
/// </summary>
public static class BuildPlayer
{
    const string VariantDir = "Assets/Resources/ShaderVariants";

    public static void MacOS()
    {
        string outDir = Environment.GetEnvironmentVariable("PINNACLE_BUILD_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = "Builds/macOS";
        EnsureVariantMaterials();
        PlayerSettings.enableFrameTimingStats = true;
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = Path.Combine(outDir, "Pinnacle.app"),
            target = BuildTarget.StandaloneOSX,
            // PINNACLE_BUILD_DEV=1: a development player, so the route can read Profiler markers (Docs/PLAYTEST.md)
            options = Environment.GetEnvironmentVariable("PINNACLE_BUILD_DEV") == "1" ? BuildOptions.Development : BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log(string.Format("[BUILD] {0}: {1} errors, {2} warnings, {3:F0} MB, {4:F0} s -> {5}", s.result, s.totalErrors, s.totalWarnings, s.totalSize / 1048576.0, s.totalTime.TotalSeconds, s.outputPath));
        EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    [MenuItem("Pinnacle/Keep run-time shader variants")]
    public static void EnsureVariantMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(VariantDir)) AssetDatabase.CreateFolder("Assets/Resources", "ShaderVariants");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        // URP's material validation sets the map and emission keywords from the material's own state (a texture in the
        // slot, an emissive colour) and clears them otherwise: a material made only with EnableKeyword lost them on
        // import, and the variants were stripped from the build (procedural surfaces without normal or gloss maps, the
        // Curator's skin at full gloss). So each material carries a small texture in every slot its keywords need.
        var flatNormal = Dummy("Dummy_normal", new Color(0.5f, 0.5f, 1f, 1f));
        var mask = Dummy("Dummy_mask", new Color(0f, 0f, 0f, 0.5f));
        string[][] litSets =
        {
            new string[0], new[] { "_NORMALMAP" }, new[] { "_ALPHATEST_ON" }, new[] { "_NORMALMAP", "_ALPHATEST_ON" },
            new[] { "_METALLICSPECGLOSSMAP" }, new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP" }, new[] { "_EMISSION" },
            new[] { "_NORMALMAP", "_EMISSION" }, new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_ALPHATEST_ON" },
        };
        int made = 0;
        if (lit != null)
            foreach (var set in litSets) made += Make(lit, "Lit_" + (set.Length == 0 ? "plain" : string.Join("_", set)), set, false, Array.IndexOf(set, "_ALPHATEST_ON") >= 0, flatNormal, mask);
        if (lit != null) made += Make(lit, "Lit_transparent", new string[0], true, false, flatNormal, mask);
        if (unlit != null) { made += Make(unlit, "Unlit_plain", new string[0], false, false, flatNormal, mask); made += Make(unlit, "Unlit_transparent", new string[0], true, false, flatNormal, mask); }
        if (particles != null) made += Make(particles, "Particles_transparent", new string[0], true, false, flatNormal, mask);
        AssetDatabase.SaveAssets();
        // read back what was kept: a keyword that did not survive would silently drop its variants again
        int lost = 0;
        foreach (var set in litSets)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(VariantDir + "/Lit_" + (set.Length == 0 ? "plain" : string.Join("_", set)) + ".mat");
            foreach (var k in set) if (m == null || !m.IsKeywordEnabled(k)) { lost++; Debug.LogWarning("[BUILD] variant material lost keyword " + k + " (" + (m != null ? m.name : "missing") + ")"); }
        }
        Debug.Log("[BUILD] run-time shader variant materials: " + made + " made or updated, " + lost + " keywords lost, kept in " + VariantDir);
    }

    static Texture2D Dummy(string name, Color c)
    {
        string path = VariantDir + "/" + name + ".asset";
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t != null) return t;
        t = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = name };
        var px = new Color[16]; for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels(px); t.Apply(false, false);
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    static int Make(Shader sh, string name, string[] keywords, bool transparent, bool clip, Texture2D flatNormal, Texture2D mask)
    {
        string path = VariantDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(sh);
        foreach (var k in keywords)
        {
            if (k == "_NORMALMAP" && m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", flatNormal);
            if (k == "_METALLICSPECGLOSSMAP" && m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", mask);
            if (k == "_EMISSION" && m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.white); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            m.EnableKeyword(k);
        }
        if (clip) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.4f); }
        if (transparent)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return 1;
    }
}

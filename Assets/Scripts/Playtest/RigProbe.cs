using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Measures a character rig instead of assuming it works. Does nothing unless PINNACLE_PROBE is set (to a Rocketbox
/// avatar name, e.g. Business_Male_04). Run like the play-test (Docs/PLAYTEST.md) with -executeMethod PlaytestHarness.Run.
///  1. Skinning: for every face bone, how many vertices it actually moves when it is turned or shifted, by how much,
///     and where on the head (BakeMesh before/after). A bone that moves no skin cannot animate the face.
///  2. Walk calibration on the scaled character: the ground speed of the stance foot at playback 1.0, the cycle and
///     step length, and when each foot is planted (the clip has no foot-contact curves).
///  3. Pictures of the face for each test pose, from the front, under flat light.
/// Writes rig_report.txt, walk_contacts.csv and probe_*.jpg to PINNACLE_PROBE_OUT.
/// </summary>
public class RigProbe : MonoBehaviour
{
    string outDir, avatar;
    readonly StringBuilder rep = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PINNACLE_PROBE"))) return;
        var go = new GameObject("RigProbe"); DontDestroyOnLoad(go); go.AddComponent<RigProbe>();
    }

    void Log(string s) { rep.AppendLine(s); Debug.Log("[PROBE] " + s); }

    IEnumerator Start()
    {
        avatar = Environment.GetEnvironmentVariable("PINNACLE_PROBE");
        outDir = Environment.GetEnvironmentVariable("PINNACLE_PROBE_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.temporaryCachePath, "probe");
        Directory.CreateDirectory(outDir);
        for (int i = 0; i < 60; i++) yield return null;
        foreach (var c in FindObjectsByType<Camera>()) c.enabled = false;
        foreach (var l in FindObjectsByType<Light>()) l.enabled = false;
        foreach (var b in FindObjectsByType<MonoBehaviour>()) if (b != this && !(b is PlaytestDriver) && b.GetType().Name.EndsWith("Story")) b.enabled = false;
        foreach (var a in FindObjectsByType<AudioSource>()) a.Stop();
        try { Skinning(); } catch (Exception e) { Log("skinning probe failed: " + e); }
        yield return Walk();
        yield return Pictures();
        File.WriteAllText(Path.Combine(outDir, "rig_report.txt"), rep.ToString());
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#else
        Application.Quit(0);
#endif
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), n); if (r != null) return r; }
        return null;
    }

    // ------------------------------------------------------------------ 1. skinning
    void Skinning()
    {
        var prefab = Resources.Load<GameObject>("Characters/RB/" + avatar);
        if (prefab == null) { Log("no prefab " + avatar); return; }
        var inst = Instantiate(prefab, new Vector3(0, 0, -600f), Quaternion.identity);
        foreach (var an in inst.GetComponentsInChildren<Animator>()) an.enabled = false;
        Log("avatar " + avatar + "  lossy scale " + inst.transform.lossyScale);
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = smr.sharedMesh;
            var mats = smr.sharedMaterials;
            var sb = new StringBuilder();
            for (int i = 0; i < mats.Length; i++) sb.Append(mats[i] != null ? mats[i].name : "null").Append(i < mats.Length - 1 ? ", " : "");
            Log(string.Format("\nSMR {0}: {1} verts, {2} submeshes [{3}], readable {4}, blendshapes {5}, bones {6}, bounds {7}",
                smr.name, mesh.vertexCount, mesh.subMeshCount, sb, mesh.isReadable, mesh.blendShapeCount, smr.bones.Length, smr.bounds.size));
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var d = mesh.GetSubMesh(s);
                Log(string.Format("  submesh {0}: {1} tris, {2} verts ({3})", s, d.indexCount / 3, d.vertexCount, s < mats.Length && mats[s] != null ? mats[s].name : "?"));
            }
            if (!mesh.isReadable) { Log("  (not readable: skin weights and deformation cannot be measured)"); continue; }
            var bones = smr.bones;
            var bw = mesh.boneWeights;
            var count = new int[bones.Length]; var maxW = new float[bones.Length];
            foreach (var w in bw)
            {
                void Add(int b, float x) { if (x > 0.02f) { count[b]++; if (x > maxW[b]) maxW[b] = x; } }
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            }
            var faceBones = new List<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                string n = bones[i].name;
                bool face = n.Contains("Eye") || n.Contains("brow") || n.Contains("Lip") || n.Contains("lip") || n.Contains("Jaw") || n.Contains("Mouth") ||
                            n.Contains("Cheek") || n.Contains("Caninus") || n.Contains("Masseter") || n.Contains("Nose") || n.Contains("Tongue");
                if (face) faceBones.Add(i);
                if (face || n.Contains("Head") || n.Contains("Finger") || n.Contains("Hand")) Log(string.Format("  weights  {0,-28} verts {1,5}  max {2:F2}", n, count[i], maxW[i]));
            }
            // deformation tests: rotate 12 degrees about each local axis, shift 4 mm along each local axis
            var baseMesh = new Mesh(); smr.BakeMesh(baseMesh, true);
            var v0 = baseMesh.vertices;
            var head = FindDeep(inst.transform, "Bip01 Head");
            foreach (int bi in faceBones)
            {
                var b = bones[bi];
                Quaternion r0 = b.localRotation; Vector3 p0 = b.localPosition;
                var line = new StringBuilder();
                line.Append(string.Format("  move {0,-26}", b.name.Replace("Bip01 ", "")));
                for (int test = 0; test < 6; test++)
                {
                    Vector3 axis = test % 3 == 0 ? Vector3.right : test % 3 == 1 ? Vector3.up : Vector3.forward;
                    if (test < 3) b.localRotation = r0 * Quaternion.AngleAxis(12f, axis);
                    else b.position = b.parent.TransformPoint(p0) + b.TransformDirection(axis) * 0.004f;
                    var m = new Mesh(); smr.BakeMesh(m, true);
                    var v1 = m.vertices; Destroy(m);
                    int moved = 0; float mx = 0; Vector3 c = Vector3.zero;
                    for (int k = 0; k < v0.Length; k++) { float dd = (v1[k] - v0[k]).magnitude; if (dd > 0.0005f) { moved++; c += v0[k]; } if (dd > mx) mx = dd; }
                    line.Append(string.Format(" | {0}{1}: {2,4}v {3,5:F1}mm", test < 3 ? "rot" : "pos", "xyz"[test % 3], moved, mx * 1000f));
                    b.localRotation = r0; b.localPosition = p0;
                }
                Log(line.ToString());
            }
            Destroy(baseMesh);
        }
        // the hand's own axes (for wrist-orientation IK): palm normal and finger direction in hand-bone space
        foreach (var side in new[] { "L", "R" })
        {
            var hand = FindDeep(inst.transform, "Bip01 " + side + " Hand");
            var mid = FindDeep(inst.transform, "Bip01 " + side + " Finger2"); var thumb = FindDeep(inst.transform, "Bip01 " + side + " Finger0");
            var pinky = FindDeep(inst.transform, "Bip01 " + side + " Finger4");
            if (hand == null || mid == null) continue;
            Vector3 fwd = hand.InverseTransformDirection(mid.position - hand.position).normalized;
            Vector3 across = hand.InverseTransformDirection(thumb.position - pinky.position).normalized;
            Log(string.Format("hand {0}: fingers {1} thumb-side {2} (hand space), finger segment lengths {3:F3}/{4:F3}/{5:F3}", side, fwd.ToString("F2"), across.ToString("F2"),
                Vector3.Distance(mid.position, FindDeep(mid, "Bip01 " + side + " Finger21").position),
                Vector3.Distance(FindDeep(mid, "Bip01 " + side + " Finger21").position, FindDeep(mid, "Bip01 " + side + " Finger22").position),
                Vector3.Distance(hand.position, mid.position)));
        }
        Destroy(inst);
    }

    // ------------------------------------------------------------------ 2. walk calibration on the cast character
    IEnumerator Walk()
    {
        var npc = CharacterNPC.Create(null, new Vector3(0, 0, -700f), Cast.Curator(), "ProbeCurator");
        npc.freezeFacing = true;
        yield return null; yield return null;
        var an = npc.GetComponentInChildren<Animator>();
        Transform root = npc.transform;
        Transform[] foot = { FindDeep(root, "Bip01 L Foot"), FindDeep(root, "Bip01 R Foot") }, toe = { FindDeep(root, "Bip01 L Toe0"), FindDeep(root, "Bip01 R Toe0") };
        Transform head = FindDeep(root, "Bip01 Head");
        Log("\nCurator height on screen: head bone at " + (head.position.y - root.position.y).ToString("F3") + " m, scale " + npc.GetComponentInChildren<Animator>().transform.localScale.x.ToString("F3") + ", humanScale " + (an != null ? an.humanScale.ToString("F3") : "?"));
        foreach (var clipName in new[] { "m_WalkForwards", "m_Idle", "m_RunForwards" })
        {
            AnimationClip clip = null;
            foreach (var k in Resources.LoadAll<AnimationClip>("Anim/" + clipName)) if (!k.name.StartsWith("__preview")) { clip = k; break; }
            if (clip == null || an == null) { Log("no clip " + clipName); continue; }
            Log(string.Format("{0}: length {1:F3}s  loop {2}  averageSpeed {3}  apparentSpeed {4:F3}  humanMotion {5}", clipName, clip.length, clip.isLooping, clip.averageSpeed.ToString("F3"), clip.apparentSpeed, clip.humanMotion));
            // sample the clip on this skeleton, in place (no root motion), and record the feet in the character's space
            var g = PlayableGraph.Create("probe"); g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var o = AnimationPlayableOutput.Create(g, "a", an);
            var cp = AnimationClipPlayable.Create(g, clip); cp.SetApplyFootIK(false);
            o.SetSourcePlayable(cp);
            an.applyRootMotion = false;
            var csv = new StringBuilder("t,lx,ly,lz,rx,ry,rz,ltx,lty,ltz,rtx,rty,rtz\n");
            int N = 120; var pos = new Vector3[2, N + 1];
            for (int s = 0; s <= N; s++)
            {
                float t = clip.length * s / N;
                cp.SetTime(t); g.Evaluate(0f);
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                csv.Append(t.ToString("F4", inv));
                for (int i = 0; i < 2; i++) { var p = root.InverseTransformPoint(foot[i].position); pos[i, s] = p; csv.Append(',').Append(p.x.ToString("F4", inv)).Append(',').Append(p.y.ToString("F4", inv)).Append(',').Append(p.z.ToString("F4", inv)); }
                for (int i = 0; i < 2; i++) { var p = root.InverseTransformPoint(toe[i].position); csv.Append(',').Append(p.x.ToString("F4", inv)).Append(',').Append(p.y.ToString("F4", inv)).Append(',').Append(p.z.ToString("F4", inv)); }
                csv.Append('\n');
            }
            g.Destroy();
            File.WriteAllText(Path.Combine(outDir, "contacts_" + clipName + ".csv"), csv.ToString());
            if (clipName == "m_Idle") continue;
            // stance: the lower half of the foot's height range; its backward speed is the ground speed at rate 1.0
            for (int i = 0; i < 2; i++)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int s = 0; s <= N; s++) { minY = Mathf.Min(minY, pos[i, s].y); maxY = Mathf.Max(maxY, pos[i, s].y); }
                float thr = minY + (maxY - minY) * 0.18f, dz = 0; int frames = 0; float firstT = -1, lastT = -1;
                for (int s = 0; s < N; s++)
                    if (pos[i, s].y < thr && pos[i, s + 1].y < thr)
                    {
                        dz += pos[i, s + 1].z - pos[i, s].z; frames++;
                        if (firstT < 0) firstT = s / (float)N; lastT = (s + 1) / (float)N;
                    }
                float dt = clip.length / N;
                Log(string.Format("  {0} foot: height {1:F3}..{2:F3} m, planted below {3:F3} for {4:P0} of the cycle (phase {5:F2}-{6:F2}), stance speed {7:F3} m/s", i == 0 ? "left" : "right", minY, maxY, thr, frames / (float)N, firstT, lastT, frames > 0 ? -dz / (frames * dt) : 0f));
            }
        }
        Destroy(npc.gameObject);
    }

    // ------------------------------------------------------------------ 3. pictures
    IEnumerator Pictures()
    {
        var stage = new GameObject("ProbeStage").transform; stage.position = new Vector3(0, 0, -800f);
        var key = new GameObject("Key").AddComponent<Light>(); key.transform.SetParent(stage, false);
        key.type = LightType.Directional; key.intensity = 1.6f; key.transform.rotation = Quaternion.Euler(20f, 160f, 0);
        CinematicLook.Ambient(new Color(0.5f, 0.5f, 0.52f), new Color(0.42f, 0.4f, 0.4f), new Color(0.2f, 0.2f, 0.2f));
        RenderSettings.fog = false;
        CharacterNPC.FaceLayer = false;          // only the test offsets on the face bones
        var npc = CharacterNPC.Create(stage, Vector3.zero, Cast.Curator(), "ProbeCurator2");
        npc.transform.rotation = Quaternion.Euler(0, 180f, 0); npc.freezeFacing = true;
        for (int i = 0; i < 20; i++) yield return null;
        var camGo = new GameObject("ProbeCam"); var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.25f, 0.27f); cam.nearClipPlane = 0.02f; cam.fieldOfView = 14f;
        var head = FindDeep(npc.transform, "Bip01 Head");
        var tests = new[] { "neutral", "Bip01 MJaw|rz|-14", "Bip01 MJaw|rz|14", "Bip01 LEyeBlinkTop|rz|-25", "Bip01 LEyeBlinkTop|rz|25", "Bip01 LEyeBlinkTop|py|-0.006",
                            "Bip01 MUpperLip|py|0.004", "Bip01 MBottomLip|py|-0.004", "Bip01 LMouthCorner|px|0.006", "Bip01 LMouthCorner|py|0.005",
                            "Bip01 LEye|ry|20", "Bip01 LInnerEyebrow|py|0.005", "Bip01 LCheek|py|0.005", "Bip01 LCaninus|py|0.004", "Bip01 MTongue|rz|-15" };
        foreach (var t in tests)
        {
            // the face layer keeps writing the bones: override after it, in a LateUpdate-like step by posing then rendering
            yield return null;
            var parts = t.Split('|');
            Transform b = parts.Length == 3 ? FindDeep(npc.transform, parts[0]) : null;
            Quaternion r0 = b != null ? b.localRotation : Quaternion.identity; Vector3 p0 = b != null ? b.localPosition : Vector3.zero;
            if (b != null)
            {
                float v = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                Vector3 axis = parts[1][1] == 'x' ? Vector3.right : parts[1][1] == 'y' ? Vector3.up : Vector3.forward;
                if (parts[1][0] == 'r') b.localRotation = r0 * Quaternion.AngleAxis(v, axis); else b.localPosition = p0 + axis * v;
            }
            Vector3 hp = head.position + npc.transform.forward * 0.02f + Vector3.up * 0.06f;
            cam.transform.position = hp + npc.transform.forward * 1.3f;
            cam.transform.LookAt(hp);
            Render(cam, "probe_" + t.Replace("Bip01 ", "").Replace('|', '_') + ".jpg", 480, 480);
            if (b != null) { b.localRotation = r0; b.localPosition = p0; }
        }
    }

    void Render(Camera cam, string file, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToJPG(90)); Destroy(tex);
    }
}

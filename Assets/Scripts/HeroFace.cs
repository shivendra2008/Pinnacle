using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Timed mouth shapes for one line of dialogue (Resources/VO/Visemes/&lt;clip&gt;.txt, made by Tools/lipsync from the
/// actual audio). 15 visemes: sil PP FF TH DD kk CH SS nn RR aa E I O U. Weights at a time come from a dominance
/// (coarticulation) model: every sound pulls the mouth towards its shape, strongest inside its own span and fading
/// before (anticipation) and after (carry-over); lip closures and lip-to-teeth contacts pull hardest.
/// </summary>
public class VisemeTrack
{
    public const int Count = 15;
    public static readonly string[] Names = { "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U" };
    public struct Seg { public float t0, t1, w; public int v; }
    public Seg[] segs;
    public float[] accentT, accentS;
    public Vector2[] pauses;
    public float duration;

    static readonly Dictionary<string, VisemeTrack> cache = new Dictionary<string, VisemeTrack>();

    /// <summary>The track for a clip, or null if this line has none (other characters, missing data).</summary>
    public static VisemeTrack For(AudioClip clip)
    {
        if (clip == null) return null;
        VisemeTrack t;
        if (cache.TryGetValue(clip.name, out t)) return t;
        var ta = Resources.Load<TextAsset>("VO/Visemes/" + clip.name);
        t = ta != null ? Parse(ta.text) : null;
        cache[clip.name] = t;
        return t;
    }

    static int Index(string n) { for (int i = 0; i < Names.Length; i++) if (Names[i] == n) return i; return 0; }

    public static VisemeTrack Parse(string text)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var segs = new List<Seg>(); var at = new List<float>(); var asg = new List<float>(); var ps = new List<Vector2>();
        float dur = 0f;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var p = line.Split(' ');
            if (p[0] == "#" && p.Length > 2 && p[2] == "") continue;
            if (p[0] == "#") { if (p.Length > 2 && float.TryParse(p[2], System.Globalization.NumberStyles.Float, inv, out float d)) dur = d; continue; }
            if (p[0] == "V" && p.Length >= 5)
                segs.Add(new Seg { t0 = float.Parse(p[1], inv), t1 = float.Parse(p[2], inv), v = Index(p[3]), w = float.Parse(p[4], inv) });
            else if (p[0] == "A" && p.Length >= 3) { at.Add(float.Parse(p[1], inv)); asg.Add(float.Parse(p[2], inv)); }
            else if (p[0] == "P" && p.Length >= 3) ps.Add(new Vector2(float.Parse(p[1], inv), float.Parse(p[2], inv)));
        }
        return new VisemeTrack { segs = segs.ToArray(), accentT = at.ToArray(), accentS = asg.ToArray(), pauses = ps.ToArray(), duration = dur };
    }

    static float Strength(int v)
    {
        switch (v)
        {
            case 1: return 1.8f;          // PP: the lips must meet
            case 2: return 1.6f;          // FF: lower lip to the upper teeth
            case 14: return 1.25f;        // U: rounding is anticipated early
            case 13: return 1.15f;
            case 6: return 1.05f;
            default: return 0.85f;
        }
    }

    static float Before(int v) { return v == 14 || v == 13 ? 0.12f : v == 1 || v == 2 ? 0.06f : 0.075f; }
    static float After(int v) { return v == 14 || v == 13 ? 0.08f : 0.05f; }

    /// <summary>Weights of the 15 visemes at time t (seconds into the audio). Sums to at most 1; the rest is "rest".</summary>
    public void Weights(float t, float[] w)
    {
        for (int i = 0; i < Count; i++) w[i] = 0f;
        float total = 0f;
        // segments are sorted by time; look at the neighbourhood only
        int lo = 0, hi = segs.Length - 1;
        while (lo < hi) { int m = (lo + hi) / 2; if (segs[m].t1 < t - 0.35f) lo = m + 1; else hi = m; }
        for (int k = lo; k < segs.Length; k++)
        {
            var s = segs[k];
            if (s.t0 > t + 0.35f) break;
            float d = t < s.t0 ? (s.t0 - t) / Before(s.v) : t > s.t1 ? (t - s.t1) / After(s.v) : 0f;
            float dom = Strength(s.v) * s.w * Mathf.Exp(-d * d * 1.6f);
            if (dom < 0.002f) continue;
            w[s.v] += dom; total += dom;
        }
        float norm = Mathf.Max(total, 1f);
        for (int i = 0; i < Count; i++) w[i] /= norm;
    }
}

/// <summary>
/// The Curator's face on the Rocketbox facial bones (no blendshapes on these models): visemes from VisemeTrack timed to
/// the voice's real playback position (so pause, resume and skipping stay in sync), blinks, gaze with saccades,
/// brows on the stressed words, a resting expression. It is the only thing that writes the face bones: every frame
/// it starts from their rest pose and adds its layers, after the body (head turn) is final.
/// </summary>
public class HeroFace
{
    // bones
    public Transform head, jaw, tongue, ulM, llM, nose;
    public Transform[] ul = new Transform[2], ll = new Transform[2], corner = new Transform[2], cheek = new Transform[2], caninus = new Transform[2];
    public Transform eyeL, eyeR;
    public Transform[] lidTop = new Transform[2], lidBot = new Transform[2], browIn = new Transform[2], browOut = new Transform[2];
    public Transform browMid;
    readonly List<Transform> bones = new List<Transform>();
    readonly List<Vector3> restPos = new List<Vector3>();
    readonly List<Quaternion> restRot = new List<Quaternion>();
    public Vector3 headUpLocal, headFwdLocal;
    float mm = 0.001f;                       // one millimetre on this face (scaled by the eye spacing)
    SkinnedMeshRenderer skin; int blinkShapeL = -1, blinkShapeR = -1;      // generated eyelid-closing shapes
    float lidGap;                            // eyelid opening at rest, per eye average
    Vector3[] lidTopRestLocal = new Vector3[2];

    // speech
    AudioSource voice; AudioClip clipNow; VisemeTrack track; int nextAccent, nextPause;
    readonly float[] vis = new float[VisemeTrack.Count], visSmooth = new float[VisemeTrack.Count];
    float speechAmt;
    public bool Speaking { get { return voice != null && voice.isPlaying && track != null; } }
    /// <summary>For tests: blink shapes present, current lid closure 0..1, current viseme weights.</summary>
    public bool HasBlinkShapes { get { return blinkShapeL >= 0; } }
    public float LidClosure { get; private set; }
    public float[] VisemeWeights { get { return visSmooth; } }

    // eyes and lids
    float nextBlink, blinkStart = -10f, blinkDur = 0.15f, nextSaccade; Vector3 saccade;
    Vector3 glanceAt; float glanceUntil = -1f;
    // expression
    string exprName = "neutral"; float[] exprFrom = new float[E], exprNow = new float[E], exprTo = new float[E]; float exprT = 1f, exprDur = 0.6f;
    float browAccent, browAccentV;

    // expression channels (millimetres or degrees)
    const int E = 10;
    // 0 corner up L, 1 corner up R, 2 corner out, 3 brow inner (up +), 4 brow outer, 5 lower lid up (squint), 6 upper lid down, 7 jaw, 8 lips forward (purse), 9 cheek puff
    static readonly Dictionary<string, float[]> Exprs = new Dictionary<string, float[]>
    {
        { "neutral", new float[E] },
        { "knowing", new[] { 0.9f, 0.3f, 0.3f, -0.4f, 0.2f, 0.5f, 0.6f, 0f, 0f, 0f } },          // the Curator at rest: amused, one corner up
        { "smile",   new[] { 1.8f, 1.4f, 1.0f, 0.2f, 0.3f, 0.9f, 0.4f, 0.5f, 0f, 0f } },
        { "grave",   new[] { -0.5f, -0.5f, -0.2f, -0.8f, -0.2f, 0.2f, 0.9f, 0f, 0f, 0f } },
        { "intent",  new[] { 0.3f, 0.1f, 0f, -0.9f, -0.4f, 0.9f, 1.1f, 0f, 0f, 0f } },           // leaning in: a narrowed, steady look
        { "raised",  new[] { 0.4f, 0.4f, 0.1f, 1.4f, 1.0f, 0f, -0.4f, 0f, 0f, 0f } },            // eyebrows up: the lightning
        { "blow",    new[] { -0.3f, -0.3f, -2.6f, 0.2f, 0f, 0.3f, 0.6f, 1.5f, 3.2f, 1.6f } },     // lips rounded and pushed out to blow
    };

    // ---------------------------------------------------------------- viseme poses (millimetres / degrees on this face)
    // jaw, upper lip up, upper lip fwd, lower lip up, lower lip fwd, corner out, corner up, corner fwd, tongue
    static readonly float[,] Pose =
    {
        /* sil */ { 0f,   0f,   0f,    0f,   0f,    0f,   0f,   0f,   0f },
        /* PP  */ { 0.3f, -0.8f, 0.7f,  2.0f, 0.8f, -1.2f,  0f,  0.6f,  0f },
        /* FF  */ { 2.0f, 1.0f, 0.1f,   1.0f, -3.2f, 0.4f, 0.2f,  0f,   0f },
        /* TH  */ { 4.0f, 0.6f, 0f,    -0.3f, 0f,   0.3f,  0f,   0f,  12f },
        /* DD  */ { 4.5f, 0.4f, 0f,    -0.3f, 0f,   0.8f,  0f,   0f,   9f },
        /* kk  */ { 6.0f, 0.3f, 0f,    -0.5f, 0f,   0.6f,  0f,   0f,  -6f },
        /* CH  */ { 3.0f, 0.9f, 3.4f,  -0.2f, 3.2f, -3.6f,  0f,  1.8f,  5f },
        /* SS  */ { 2.0f, 0.7f, 0f,    -0.5f, 0f,   1.8f, 0.4f,  0f,   5f },
        /* nn  */ { 4.5f, 0.3f, 0f,    -0.3f, 0f,   0.7f,  0f,   0f,   9f },
        /* RR  */ { 4.0f, 0.5f, 2.0f,  -0.2f, 1.8f, -3.0f,  0f,  1.2f,  3f },
        /* aa  */ { 12f,  0.8f, 0f,    -0.8f, 0f,   0.4f, -0.2f,  0f,  -4f },
        /* E   */ { 7.5f, 0.8f, 0f,    -0.5f, 0f,   2.8f, 0.4f,  0f,  -2f },
        /* I   */ { 4.5f, 0.7f, 0f,    -0.4f, 0f,   4.2f, 0.8f, -0.4f,  0f },
        /* O   */ { 9f,   0.6f, 3.0f,  -0.5f, 2.8f, -4.6f,  0f,  2.2f, -3f },
        /* U   */ { 4.5f, 0.3f, 4.6f,   0.3f, 4.2f, -6.4f,  0f,  3.2f, -2f },
    };
    /// <summary>Global scale on the mouth shapes (tuned against close-up renders).</summary>
    public static float Articulation = 0.8f;
    /// <summary>Test bench only: hold one viseme at full weight (-1 = off).</summary>
    public static int DebugViseme = -1;

    Transform Find(Transform root, string n)
    {
        if (root == null) return null;
        if (root.name == n) return root;
        for (int i = 0; i < root.childCount; i++) { var r = Find(root.GetChild(i), n); if (r != null) return r; }
        return null;
    }

    Transform B(Transform root, string n)
    {
        var t = Find(root, "Bip01 " + n);
        if (t != null && !bones.Contains(t)) { bones.Add(t); restPos.Add(t.localPosition); restRot.Add(t.localRotation); }
        return t;
    }

    public void Setup(CharacterNPC npc)
    {
        var root = npc.RootForFace;
        head = npc.HeadForFace; jaw = npc.JawForFace; voice = npc.VoiceSource;
        if (jaw != null && !bones.Contains(jaw)) { bones.Add(jaw); restPos.Add(jaw.localPosition); restRot.Add(jaw.localRotation); }
        tongue = B(root, "MTongue"); ulM = B(root, "MUpperLip"); llM = B(root, "MBottomLip"); nose = B(root, "MNose");
        string[] sd = { "L", "R" };
        for (int i = 0; i < 2; i++)
        {
            ul[i] = B(root, sd[i] + "Upperlip"); ll[i] = B(root, sd[i] + "MouthBottom"); corner[i] = B(root, sd[i] + "MouthCorner");
            cheek[i] = B(root, sd[i] + "Cheek"); caninus[i] = B(root, sd[i] + "Caninus");
            lidTop[i] = B(root, sd[i] + "EyeBlinkTop"); lidBot[i] = B(root, sd[i] + "EyeBlinkBottom");
            browIn[i] = B(root, sd[i] + "InnerEyebrow"); browOut[i] = B(root, sd[i] + "OuterEyebrow");
        }
        eyeL = B(root, "LEye"); eyeR = B(root, "REye");
        browMid = B(root, "MMiddleEyebrow");
        Vector3 up = npc.transform.up;
        headUpLocal = Quaternion.Inverse(head.rotation) * up;
        Vector3 fwd = npc.transform.forward;
        if (eyeL != null && eyeR != null)
        {
            Vector3 m = Vector3.ProjectOnPlane((eyeL.position + eyeR.position) * 0.5f - head.position, up);
            if (m.sqrMagnitude > 1e-6f) fwd = m.normalized;
            mm = 0.001f * Vector3.Distance(eyeL.position, eyeR.position) / 0.064f;
        }
        headFwdLocal = Quaternion.Inverse(head.rotation) * fwd;
        float gaps = 0f; int n = 0;
        for (int i = 0; i < 2; i++)
            if (lidTop[i] != null && lidBot[i] != null) { gaps += Vector3.Distance(lidTop[i].position, lidBot[i].position); n++; lidTopRestLocal[i] = lidTop[i].localPosition; }
        lidGap = n > 0 ? gaps / n : 0.01f;
        nextBlink = Time.time + Random.Range(1f, 3f);
        nextSaccade = Time.time + 0.5f;
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (r.sharedMesh != null && r.sharedMesh.isReadable) { skin = r; break; }
        if (skin != null) BuildBlinkShapes(skin);
        SetExpression("knowing", 0.01f);
    }

    // ---------------------------------------------------------------- eyelids that close
    // The upper-lid bone of these models moves only ~25 vertices, at most 68% weighted (measured by RigProbe), so a
    // blink on the bone leaves the eye visibly open. Instead, a closing shape is built from the mesh once: every
    // vertex the lid bone influences turns about the eyeball's centre (so the lid slides over the eye, never into
    // it) by an angle that takes the lid margin down to the lower lid, scaled by how strongly the bone owns it. The
    // lower lid comes up a little. One shape per eye; it is added to this character's own copy of the mesh.
    static readonly Dictionary<Mesh, Mesh> blinkMeshes = new Dictionary<Mesh, Mesh>();

    void BuildBlinkShapes(SkinnedMeshRenderer smr)
    {
        var src = smr.sharedMesh;
        Mesh mesh;
        if (!blinkMeshes.TryGetValue(src, out mesh) || mesh == null)
        {
            mesh = Object.Instantiate(src);
            mesh.name = src.name + "_blink";
            var bones = smr.bones; var bind = mesh.bindposes; var bw = mesh.boneWeights;
            var v = mesh.vertices; var n = mesh.normals;
            int Idx(string bn) { for (int i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == "Bip01 " + bn) return i; return -1; }
            int eyeL = Idx("LEye"), eyeR = Idx("REye");
            bool built = false;
            foreach (var side in new[] { "L", "R" })
            {
                int top = Idx(side + "EyeBlinkTop"), bot = Idx(side + "EyeBlinkBottom"), eye = Idx(side + "Eye");
                if (top < 0 || bot < 0 || eye < 0 || eyeL < 0 || eyeR < 0) continue;
                Vector3 c = bind[eye].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 axis = (bind[eyeL].inverse.MultiplyPoint3x4(Vector3.zero) - bind[eyeR].inverse.MultiplyPoint3x4(Vector3.zero)).normalized;
                float W(BoneWeight b, int bone) { float w = 0; if (b.boneIndex0 == bone) w += b.weight0; if (b.boneIndex1 == bone) w += b.weight1; if (b.boneIndex2 == bone) w += b.weight2; if (b.boneIndex3 == bone) w += b.weight3; return w; }
                float maxT = 0, maxB = 0;
                for (int i = 0; i < v.Length; i++) { maxT = Mathf.Max(maxT, W(bw[i], top)); maxB = Mathf.Max(maxB, W(bw[i], bot)); }
                if (maxT < 0.05f || maxB < 0.05f) continue;
                Vector3 mt = Vector3.zero, mb = Vector3.zero; float nt = 0, nb = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    float wt = W(bw[i], top), wb = W(bw[i], bot);
                    if (wt >= maxT * 0.75f) { mt += v[i] * wt; nt += wt; }
                    if (wb >= maxB * 0.75f) { mb += v[i] * wb; nb += wb; }
                }
                mt /= nt; mb /= nb;
                Vector3 a = Vector3.ProjectOnPlane(mt - c, axis), b = Vector3.ProjectOnPlane(mb - c, axis);
                float close = Vector3.SignedAngle(a, b, axis);           // turning the top margin onto the bottom one
                var dv = new Vector3[v.Length]; var dn = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    float wt = W(bw[i], top), wb = W(bw[i], bot);
                    float ang = 0f;
                    if (wt > 0.01f) ang += close * 0.96f * Mathf.Pow(Mathf.Clamp01(wt / maxT), 0.8f);
                    if (wb > 0.01f) ang -= close * 0.1f * Mathf.Clamp01(wb / maxB);
                    if (Mathf.Abs(ang) < 0.01f) continue;
                    var q = Quaternion.AngleAxis(ang, axis);
                    dv[i] = c + q * (v[i] - c) - v[i];
                    if (n != null && n.Length == v.Length) dn[i] = q * n[i] - n[i];
                }
                mesh.AddBlendShapeFrame("blink_" + side, 100f, dv, dn, null);
                built = true;
            }
            if (!built) { Object.Destroy(mesh); blinkMeshes[src] = null; return; }
            blinkMeshes[src] = mesh;
        }
        smr.sharedMesh = mesh;
        blinkShapeL = mesh.GetBlendShapeIndex("blink_L"); blinkShapeR = mesh.GetBlendShapeIndex("blink_R");
    }

    public void SetExpression(string name, float blend)
    {
        float[] to;
        if (!Exprs.TryGetValue(name, out to)) to = Exprs["neutral"];
        exprName = name;
        System.Array.Copy(exprNow, exprFrom, E);
        System.Array.Copy(to, exprTo, E);
        exprT = 0f; exprDur = Mathf.Max(0.01f, blend);
    }

    public void BlinkNow() { blinkStart = Time.time; nextBlink = Time.time + Random.Range(2.5f, 5f); }
    public void Glance(Vector3 world, float seconds) { glanceAt = world; glanceUntil = Time.time + seconds; }

    public void Apply(CharacterNPC npc, float dt)
    {
        if (head == null || dt <= 0f) return;
        // ---- start from the rest pose: nothing accumulates, nothing else writes these bones
        for (int i = 0; i < bones.Count; i++) if (bones[i] != null) { bones[i].localPosition = restPos[i]; bones[i].localRotation = restRot[i]; }
        Quaternion hr = head.rotation;
        Vector3 hUp = hr * headUpLocal, hFwd = hr * headFwdLocal, hRight = Vector3.Cross(hUp, hFwd).normalized;    // the character's right
        float t = Time.time;

        // ---- speech: the audio actually playing decides the time
        if (voice != null && voice.clip != clipNow) { clipNow = voice.clip; track = VisemeTrack.For(clipNow); nextAccent = 0; nextPause = 0; }
        bool live = voice != null && voice.clip != null && voice.isPlaying && track != null;
        float at = live ? voice.time + 0.03f : -1f;          // the mouth leads the sound by a frame
        if (live) track.Weights(at, vis);
        else for (int i = 0; i < VisemeTrack.Count; i++) vis[i] = 0f;
        if (DebugViseme >= 0) for (int i = 0; i < VisemeTrack.Count; i++) vis[i] = i == DebugViseme ? 1f : 0f;
        // a light smoothing: no frame-to-frame jitter. Paused, the hero is re-posed with a 1e-6 s step: nothing advances
        // (the mouth crept 0.1% over 40 paused frames after a line, which the pause test caught at the lightning)
        float k = dt > 1e-4f ? 1f - Mathf.Exp(-dt / 0.022f) : 0f;
        for (int i = 0; i < VisemeTrack.Count; i++) visSmooth[i] += (vis[i] - visSmooth[i]) * k;
        if (dt > 1e-4f) speechAmt = Mathf.MoveTowards(speechAmt, live ? 1f : 0f, dt * 6f);
        if (live)
        {
            while (nextAccent < track.accentT.Length && track.accentT[nextAccent] <= at)
            {
                float s = track.accentS[nextAccent];
                if (s > 0.15f) npc.AccentFromFace(0.35f + 0.65f * s, Mathf.Abs(Mathf.Sin(nextAccent * 12.9898f + clipNow.name.Length * 4.1414f)) % 1f);
                nextAccent++;
            }
            while (nextPause < track.pauses.Length && track.pauses[nextPause].x <= at)
            {
                // people blink at the ends of phrases, not mid-word
                if (track.pauses[nextPause].y - track.pauses[nextPause].x > 0.2f && t - blinkStart > 1.2f && Random.value < 0.6f) BlinkNow();
                nextPause++;
            }
        }
        // the mouth shape
        float[] m = new float[9];
        for (int v = 0; v < VisemeTrack.Count; v++)
        {
            float w = visSmooth[v];
            if (w < 0.0005f) continue;
            for (int c = 0; c < 9; c++) m[c] += Pose[v, c] * w;
        }
        for (int c = 0; c < 9; c++) m[c] *= Articulation;

        // ---- expression
        exprT = Mathf.Min(1f, exprT + dt / exprDur);
        float ee = exprT * exprT * (3f - 2f * exprT);
        for (int c = 0; c < E; c++) exprNow[c] = Mathf.Lerp(exprFrom[c], exprTo[c], ee);
        float[] x = exprNow;
        // while talking, the resting smile gives way to the speech shapes a little
        float exprMouth = 1f - 0.5f * speechAmt;

        // ---- jaw and tongue
        Vector3 jawAxis = hRight;
        float jawDeg = m[0] + x[7];
        if (jaw != null && Mathf.Abs(jawDeg) > 0.01f) jaw.rotation = Quaternion.AngleAxis(jawDeg, jawAxis) * jaw.rotation;
        if (tongue != null && Mathf.Abs(m[8]) > 0.01f) tongue.rotation = Quaternion.AngleAxis(-m[8], jawAxis) * tongue.rotation;

        // ---- lips (positions after the jaw has turned, in head directions)
        float purse = x[8];
        Move(ulM, hUp * m[1] + hFwd * (m[2] + purse));
        for (int i = 0; i < 2; i++) Move(ul[i], hUp * m[1] * 0.8f + hFwd * (m[2] + purse) * 0.8f);
        Move(llM, hUp * m[3] + hFwd * (m[4] + purse));
        for (int i = 0; i < 2; i++) Move(ll[i], hUp * m[3] * 0.8f + hFwd * (m[4] + purse) * 0.8f);
        for (int i = 0; i < 2; i++)
        {
            Vector3 outward = i == 0 ? -hRight : hRight;                 // L is the character's left
            float up = m[6] + x[i] * exprMouth;
            Move(corner[i], outward * (m[5] + x[2] * exprMouth) + hUp * up + hFwd * (m[7] + purse * 0.6f));
            Move(cheek[i], hUp * (Mathf.Max(0f, x[i]) * 0.45f) + hFwd * x[9]);
        }

        // ---- eyes: on the eyeline (or a glance), with small saccades
        if (t > nextSaccade) { nextSaccade = t + Random.Range(0.4f, 1.9f); saccade = Random.insideUnitSphere * 0.025f; }
        Vector3 target = t < glanceUntil ? glanceAt : npcGaze(npc);
        float dist = Vector3.Distance(head.position, target);
        Vector3 eyeTarget = target + saccade * dist;
        foreach (var e in new[] { eyeL, eyeR })
        {
            if (e == null) continue;
            Vector3 dir = (eyeTarget - e.position).normalized;
            dir = Vector3.RotateTowards(hFwd, dir, 26f * Mathf.Deg2Rad, 0f);
            e.rotation = Quaternion.FromToRotation(hFwd, dir) * e.rotation;
        }
        float lookDown = Mathf.Clamp01(-Vector3.Dot(hUp, (eyeTarget - head.position).normalized) * 2.5f);

        // ---- lids: blinks every few seconds (more at the ends of phrases), lids follow a downward look
        if (t > nextBlink) BlinkNow();
        float bt = (t - blinkStart) / blinkDur;
        float blink = bt < 0f || bt > 1f ? 0f : (bt < 0.4f ? Mathf.Sin(bt / 0.4f * Mathf.PI * 0.5f) : Mathf.Cos((bt - 0.4f) / 0.6f * Mathf.PI * 0.5f));
        float close = Mathf.Clamp01(Mathf.Max(blink, 0.18f * lookDown + x[6] * 0.07f));
        float squint = x[5];
        LidClosure = close;
        if (blinkShapeL >= 0 && skin != null)
        {
            // the generated shapes close the lids; the bones only add the squint of the lower lid
            skin.SetBlendShapeWeight(blinkShapeL, close * 100f);
            if (blinkShapeR >= 0) skin.SetBlendShapeWeight(blinkShapeR, close * 100f);
            for (int i = 0; i < 2; i++) Move(lidBot[i], hUp * squint * 0.6f);
        }
        else
            for (int i = 0; i < 2; i++)
            {
                if (lidTop[i] == null || lidBot[i] == null) continue;
                Vector3 pt = lidTop[i].position, pb = lidBot[i].position;
                lidTop[i].position = pt + (pb - pt) * 0.85f * close;
                lidBot[i].position = pb + (pt - pb) * (0.12f * close) + hUp * squint * mm * 0.6f;
            }

        // ---- brows: the expression, plus a lift on stressed words that decays
        browAccent = Mathf.SmoothDamp(browAccent, 0f, ref browAccentV, 0.45f, 50f, dt);
        float bi = x[3] + browAccent * 1.6f, bo = x[4] + browAccent * 1.0f;
        for (int i = 0; i < 2; i++)
        {
            Move(browIn[i], hUp * bi);
            Move(browOut[i], hUp * bo);
        }
        Move(browMid, hUp * bi * 0.7f);
    }

    /// <summary>Called from the head layer when a stressed word lands.</summary>
    public void BrowAccent(float s) { browAccent = Mathf.Max(browAccent, s); }

    Vector3 npcGaze(CharacterNPC npc) { return npc.GazeForFace(); }

    void Move(Transform b, Vector3 mmOffset) { if (b != null && mmOffset.sqrMagnitude > 1e-10f) b.position += mmOffset * mm; }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

/// <summary>
/// Appearance preset for a character: which model to load and how to recolour its materials.
/// Material names are matched by lower-case substring, first match wins (so list specific names first).
/// </summary>
public class NpcStyle
{
    public string model = "Characters/Kamala";
    public float height = 1.62f;
    public readonly List<KeyValuePair<string, Color>> map = new List<KeyValuePair<string, Color>>();
    public NpcStyle Set(string key, Color c) { map.Add(new KeyValuePair<string, Color>(key, c)); return this; }

    static Color C(float r, float g, float b) { return new Color(r, g, b); }

    public static NpcStyle Kamala()
    {
        return new NpcStyle { model = "Characters/Kamala", height = 1.58f }
            .Set("skin", C(0.60f, 0.40f, 0.27f)).Set("red", C(0.20f, 0.18f, 0.18f)).Set("green", C(0.50f, 0.12f, 0.16f))
            .Set("gold", C(0.92f, 0.74f, 0.28f)).Set("brown", C(0.08f, 0.05f, 0.04f));
    }

    public static NpcStyle Reddy()   // Inspector Sonal Reddy, plainclothes: simple salwar kameez
    {
        return new NpcStyle { model = "Characters/Kamala", height = 1.66f }
            .Set("skin", C(0.62f, 0.43f, 0.30f)).Set("red", C(0.06f, 0.05f, 0.05f)).Set("green", C(0.20f, 0.36f, 0.48f))
            .Set("gold", C(0.75f, 0.75f, 0.78f)).Set("brown", C(0.05f, 0.04f, 0.04f));
    }

    public static NpcStyle Bhaskar()  // auto driver: khaki uniform
    {
        return new NpcStyle { model = "Characters/Casual", height = 1.70f }
            .Set("skin_darker", C(0.45f, 0.29f, 0.19f)).Set("skin", C(0.52f, 0.34f, 0.23f)).Set("hair", C(0.05f, 0.04f, 0.04f))
            .Set("eyebrows", C(0.05f, 0.04f, 0.04f)).Set("eye", C(0.03f, 0.02f, 0.02f))
            .Set("lightbrown", C(0.62f, 0.56f, 0.38f)).Set("lightblue", C(0.42f, 0.38f, 0.27f))
            .Set("red_dark", C(0.20f, 0.14f, 0.10f)).Set("white", C(0.35f, 0.3f, 0.25f));
    }

    public static NpcStyle Vikram()   // chai seller: white kurta, grey hair
    {
        return new NpcStyle { model = "Characters/Casual", height = 1.66f }
            .Set("skin_darker", C(0.47f, 0.30f, 0.20f)).Set("skin", C(0.55f, 0.36f, 0.24f)).Set("hair", C(0.55f, 0.55f, 0.55f))
            .Set("eyebrows", C(0.35f, 0.35f, 0.35f)).Set("eye", C(0.03f, 0.02f, 0.02f))
            .Set("lightbrown", C(0.88f, 0.85f, 0.76f)).Set("lightblue", C(0.22f, 0.20f, 0.22f))
            .Set("red_dark", C(0.25f, 0.18f, 0.12f)).Set("white", C(0.5f, 0.45f, 0.4f));
    }

    public static NpcStyle Thug()     // dark suit
    {
        return new NpcStyle { model = "Characters/Suit", height = 1.82f }
            .Set("skin", C(0.58f, 0.40f, 0.28f)).Set("hair", C(0.04f, 0.04f, 0.04f)).Set("eyebrows", C(0.04f, 0.04f, 0.04f))
            .Set("eye", C(0.02f, 0.02f, 0.02f)).Set("suit", C(0.04f, 0.04f, 0.05f)).Set("white", C(0.08f, 0.08f, 0.09f))
            .Set("tie", C(0.03f, 0.03f, 0.03f)).Set("black", C(0.02f, 0.02f, 0.02f));
    }

    /// <summary>Women in bright sari / salwar colours (the dress model, recoloured).</summary>
    public static NpcStyle CrowdWoman(int seed)
    {
        var r = new System.Random(seed);
        Color[] saris =
        {
            C(0.92f, 0.55f, 0.1f), C(0.85f, 0.15f, 0.35f), C(0.1f, 0.5f, 0.45f), C(0.2f, 0.3f, 0.7f),
            C(0.9f, 0.78f, 0.15f), C(0.55f, 0.15f, 0.6f), C(0.75f, 0.1f, 0.12f), C(0.15f, 0.55f, 0.25f),
        };
        float t = 0.46f + (float)r.NextDouble() * 0.2f;
        return new NpcStyle { model = "Characters/Kamala", height = 1.5f + (float)r.NextDouble() * 0.14f }
            .Set("skin", C(t, t * 0.68f, t * 0.48f)).Set("red", C(0.05f, 0.04f, 0.04f))
            .Set("green", saris[r.Next(saris.Length)]).Set("gold", C(0.92f, 0.74f, 0.28f)).Set("brown", C(0.05f, 0.04f, 0.04f));
    }

    /// <summary>A child (same casual model, much smaller).</summary>
    public static NpcStyle Child(int seed)
    {
        var st = Crowd(seed);
        st.height = 1.05f + (float)new System.Random(seed).NextDouble() * 0.2f;
        return st;
    }

    /// <summary>Random market-goers built from the casual / hoodie models.</summary>
    public static NpcStyle Crowd(int seed)
    {
        var r = new System.Random(seed);
        Color shirt = Color.HSVToRGB((float)r.NextDouble(), 0.45f + (float)r.NextDouble() * 0.3f, 0.45f + (float)r.NextDouble() * 0.35f);
        Color pants = Color.HSVToRGB((float)r.NextDouble(), 0.2f, 0.25f + (float)r.NextDouble() * 0.2f);
        float t = 0.42f + (float)r.NextDouble() * 0.22f;
        Color skin = C(t, t * 0.66f, t * 0.46f);
        Color hair = r.NextDouble() < 0.2 ? C(0.5f, 0.5f, 0.5f) : C(0.05f, 0.04f, 0.04f);
        bool hood = r.NextDouble() < 0.4;
        var st = new NpcStyle { model = hood ? "Characters/Hoodie" : "Characters/Casual", height = 1.55f + (float)r.NextDouble() * 0.28f };
        st.Set("skin_darker", skin * 0.85f).Set("skin", skin).Set("hair", hair).Set("eyebrows", hair).Set("eye", C(0.03f, 0.02f, 0.02f))
          .Set("lightbrown", shirt).Set("purple", shirt).Set("lightblue", pants).Set("red_dark", C(0.2f, 0.12f, 0.1f)).Set("white", C(0.45f, 0.4f, 0.36f));
        return st;
    }
}

/// <summary>
/// A stylised NPC (Mrs. Kapoor, Bhaskar, Vikram, ...). A stylised character built from primitives
/// (sari, blouse, face, hair bun, bangles) with idle breathing, head tracking,
/// hand gestures, walking, and simple audio-driven lip sync.
/// </summary>
public class CharacterNPC : MonoBehaviour
{
    public Transform lookTarget;

    AudioSource voice;
    Transform visual, head, mouth, armL, armR, torso;
    readonly float[] samples = new float[256];
    float talk, walkPhase, gesture;
    bool walking;

    // model (Quaternius "Animated Woman", CC0) - loaded from Resources/Characters/Kamala
    const float ModelYawOffset = 0f;
    bool usingModel;
    Animation anim;
    Transform headBone;
    string clipIdle, clipWalk, clipNow;
    bool moving;                       // actually changing position right now (drives the walk animation)
    float curSpeed;
    // procedural fallback walk (used only if the model has no walk clip)
    Transform bUpL, bUpR, bLoL, bLoR, bArmL, bArmR, bHips;
    Quaternion qUpL, qUpR, qLoL, qLoR, qArmL, qArmR;
    float walkCycle;
    // Mecanim-imported clips (non-legacy) are played through the Playables API
    bool usingPlayable;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable pIdle, pWalk;
    AnimationClip cIdle, cWalk;
    float walkBlend;

    public bool IsTalking => voice != null && voice.isPlaying;
    public bool IsWalking => walking;
    public bool IsPhysical => cc != null;

    // ---- collision: physical NPCs move with a CharacterController, so they cannot walk through cars, carts, cows or people
    CharacterController cc;
    float stuckT;

    public void MakePhysical()
    {
        if (cc != null) return;
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = Mathf.Max(1.0f, style != null ? style.height : 1.7f);
        cc.radius = 0.24f;
        cc.center = new Vector3(0, cc.height / 2f, 0);
        cc.stepOffset = 0.2f;
        cc.skinWidth = 0.02f;
        cc.minMoveDistance = 0f;
    }

    /// <summary>Step sideways (colliding properly) towards a world X, e.g. to get out of a vehicle's way.</summary>
    public void NudgeX(float targetX, float speed)
    {
        if (cc == null) return;
        float dx = targetX - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return;
        cc.Move(new Vector3(Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), speed * Time.deltaTime), -2f * Time.deltaTime, 0));
    }

    IEnumerator Sidestep(Vector3 dirW)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dirW).normalized * (Random.value < 0.5f ? 1f : -1f);
        moving = true;
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            cc.Move(side * 1.1f * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ build
    NpcStyle style;

    public static CharacterNPC Create(Transform parent, Vector3 localPos, NpcStyle style = null, string npcName = "NPC")
    {
        var go = new GameObject(npcName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var k = go.AddComponent<CharacterNPC>();
        k.style = style ?? NpcStyle.Kamala();
        if (!k.TryBuildModel()) k.BuildProcedural();
        return k;
    }

    static Material Mat(Color c, float smooth = 0.25f)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, float smooth = 0.25f)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = Mat(c, smooth);
        return g.transform;
    }

    void BuildProcedural()
    {
        Color skin = new Color(0.70f, 0.48f, 0.34f);
        Color skinDark = new Color(0.60f, 0.40f, 0.28f);
        Color sari = new Color(0.86f, 0.80f, 0.66f);
        Color maroon = new Color(0.55f, 0.13f, 0.17f);
        Color blouse = new Color(0.14f, 0.38f, 0.40f);
        Color hair = new Color(0.27f, 0.25f, 0.25f);
        Color gold = new Color(0.9f, 0.72f, 0.25f);

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localScale = Vector3.one * 0.93f;

        // sari skirt + border
        Part(PrimitiveType.Cylinder, visual, new Vector3(0, 0.52f, 0), new Vector3(0.46f, 0.52f, 0.40f), sari, 0.1f);
        Part(PrimitiveType.Cylinder, visual, new Vector3(0, 0.05f, 0), new Vector3(0.475f, 0.05f, 0.415f), maroon, 0.1f);
        Part(PrimitiveType.Cylinder, visual, new Vector3(0, 0.98f, 0), new Vector3(0.40f, 0.06f, 0.34f), maroon, 0.1f);

        // torso (blouse) with breathing
        torso = Part(PrimitiveType.Capsule, visual, new Vector3(0, 1.2f, 0), new Vector3(0.34f, 0.26f, 0.22f), blouse, 0.15f);
        // pallu across the chest and over the shoulder
        var sash = Part(PrimitiveType.Cube, visual, new Vector3(0.0f, 1.2f, 0.1f), new Vector3(0.10f, 0.72f, 0.26f), sari, 0.1f);
        sash.localRotation = Quaternion.Euler(0, 0, -32f);
        var edge = Part(PrimitiveType.Cube, visual, new Vector3(0.0f, 1.2f, 0.105f), new Vector3(0.02f, 0.72f, 0.265f), maroon, 0.1f);
        edge.localRotation = Quaternion.Euler(0, 0, -32f);
        var pallu = Part(PrimitiveType.Cube, visual, new Vector3(-0.18f, 1.05f, -0.03f), new Vector3(0.09f, 0.70f, 0.30f), sari, 0.1f);
        pallu.localRotation = Quaternion.Euler(0, 0, 6f);

        // neck
        Part(PrimitiveType.Cylinder, visual, new Vector3(0, 1.5f, 0), new Vector3(0.09f, 0.06f, 0.09f), skin);

        // head
        head = new GameObject("Head").transform;
        head.SetParent(visual, false);
        head.localPosition = new Vector3(0, 1.66f, 0);
        Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.20f, 0.24f, 0.22f), skin);
        Part(PrimitiveType.Sphere, head, new Vector3(0, 0.02f, -0.03f), new Vector3(0.215f, 0.25f, 0.21f), hair, 0.35f);
        Part(PrimitiveType.Sphere, head, new Vector3(0, 0.07f, -0.16f), Vector3.one * 0.13f, hair, 0.35f); // bun
        // face
        Part(PrimitiveType.Sphere, head, new Vector3(-0.058f, 0.028f, 0.095f), Vector3.one * 0.028f, new Color(0.05f, 0.03f, 0.03f), 0.7f);
        Part(PrimitiveType.Sphere, head, new Vector3(0.058f, 0.028f, 0.095f), Vector3.one * 0.028f, new Color(0.05f, 0.03f, 0.03f), 0.7f);
        var bl = Part(PrimitiveType.Cube, head, new Vector3(-0.058f, 0.07f, 0.098f), new Vector3(0.06f, 0.009f, 0.01f), hair);
        var br = Part(PrimitiveType.Cube, head, new Vector3(0.058f, 0.07f, 0.098f), new Vector3(0.06f, 0.009f, 0.01f), hair);
        bl.localRotation = Quaternion.Euler(0, 0, -8f);
        br.localRotation = Quaternion.Euler(0, 0, 8f);
        Part(PrimitiveType.Sphere, head, new Vector3(0, -0.012f, 0.112f), new Vector3(0.032f, 0.04f, 0.03f), skinDark);
        mouth = Part(PrimitiveType.Cube, head, new Vector3(0, -0.07f, 0.098f), new Vector3(0.05f, 0.008f, 0.012f), new Color(0.45f, 0.1f, 0.12f));
        Part(PrimitiveType.Sphere, head, new Vector3(0, 0.052f, 0.108f), Vector3.one * 0.016f, new Color(0.8f, 0.05f, 0.08f)); // bindi
        Part(PrimitiveType.Sphere, head, new Vector3(-0.104f, -0.035f, 0.0f), Vector3.one * 0.022f, gold, 0.8f);
        Part(PrimitiveType.Sphere, head, new Vector3(0.104f, -0.035f, 0.0f), Vector3.one * 0.022f, gold, 0.8f);

        // arms (pivot at shoulder)
        armL = MakeArm(-0.2f, blouse, skin, gold);
        armR = MakeArm(0.2f, blouse, skin, gold);
    }

    Transform MakeArm(float x, Color sleeve, Color skin, Color gold)
    {
        var pivot = new GameObject(x < 0 ? "ArmL" : "ArmR").transform;
        pivot.SetParent(visual, false);
        pivot.localPosition = new Vector3(x, 1.4f, 0);
        Part(PrimitiveType.Capsule, pivot, new Vector3(0, -0.17f, 0), new Vector3(0.085f, 0.16f, 0.085f), sleeve, 0.15f);
        Part(PrimitiveType.Capsule, pivot, new Vector3(0, -0.42f, 0.02f), new Vector3(0.07f, 0.14f, 0.07f), skin);
        Part(PrimitiveType.Sphere, pivot, new Vector3(0, -0.60f, 0.03f), Vector3.one * 0.07f, skin);
        for (int i = 0; i < 3; i++)
            Part(PrimitiveType.Cylinder, pivot, new Vector3(0, -0.53f - i * 0.012f, 0.025f), new Vector3(0.085f, 0.006f, 0.085f), gold, 0.8f);
        return pivot;
    }

    void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0.55f;
        voice.minDistance = 3f;
        voice.maxDistance = 20f;
        voice.volume = 1f;
    }


    // ------------------------------------------------------------ real model
    Material Recolor(Material m)
    {
        string n = m != null ? m.name.ToLowerInvariant() : "";
        foreach (var kv in style.map)
            if (n.Contains(kv.Key)) return Surface(kv.Key, kv.Value);
        return m;
    }

    /// <summary>
    /// Each part of a person reflects light differently. Giving skin, hair, eyes, cloth and metal their own response
    /// (instead of one flat plastic value) is what stops the models reading as toys under the film lighting.
    /// </summary>
    static Material Surface(string key, Color c)
    {
        float smooth = 0.14f, metal = 0f;                     // default: matte cloth
        if (key.Contains("skin")) smooth = 0.46f;              // soft sheen
        else if (key.Contains("eyebrow")) smooth = 0.2f;
        else if (key.Contains("eye")) smooth = 0.92f;          // wet, catches a highlight
        else if (key.Contains("hair") || key == "brown" || key == "red") smooth = 0.58f;   // "red"/"brown" are the hair on the woman model
        else if (key.Contains("gold")) { smooth = 0.82f; metal = 0.95f; }
        else if (key.Contains("black") || key.Contains("red_dark")) smooth = 0.5f;   // leather shoes, belts
        else if (key.Contains("suit") || key.Contains("tie")) smooth = 0.24f;
        var m = Mat(c, smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (key.Contains("skin") && m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", new Color(0.25f, 0.2f, 0.18f));
        return m;
    }

    // The source models are flat-shaded (every polygon has its own hard-edged normals), which is exactly what makes
    // them look like low-poly cartoons. Re-averaging the normals across shared positions gives rounded, continuous
    // shading on faces, arms and cloth while keeping genuinely sharp edges (collars, hems, shoe soles).
    static readonly Dictionary<Mesh, Mesh> smoothCache = new Dictionary<Mesh, Mesh>();

    static Mesh Smoothed(Mesh src)
    {
        if (src == null) return null;
        Mesh done;
        if (smoothCache.TryGetValue(src, out done) && done != null) return done;
        if (!src.isReadable) { smoothCache[src] = src; return src; }
        var mesh = Instantiate(src);
        mesh.name = src.name + "_smooth";
        var v = mesh.vertices; var n = mesh.normals;
        if (n == null || n.Length != v.Length) { smoothCache[src] = src; return src; }
        var faces = new Dictionary<Vector3Int, List<Vector3>>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tri = mesh.GetTriangles(s);
            for (int i = 0; i + 2 < tri.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);   // length = 2 x area, so big faces count more
                if (fn.sqrMagnitude < 1e-14f) continue;
                for (int k = 0; k < 3; k++)
                {
                    var key = Key(v[tri[i + k]]);
                    List<Vector3> list;
                    if (!faces.TryGetValue(key, out list)) { list = new List<Vector3>(6); faces[key] = list; }
                    list.Add(fn);
                }
            }
        }
        const float cosLimit = 0.3f;        // about 72 degrees: sharper creases than this stay sharp
        for (int i = 0; i < v.Length; i++)
        {
            List<Vector3> list;
            if (!faces.TryGetValue(Key(v[i]), out list)) continue;
            Vector3 acc = Vector3.zero;
            foreach (var fn in list)
                if (Vector3.Dot(fn.normalized, n[i]) > cosLimit) acc += fn;
            if (acc.sqrMagnitude > 1e-12f) n[i] = acc.normalized;
        }
        mesh.normals = n;
        smoothCache[src] = mesh;
        return mesh;
    }

    static Vector3Int Key(Vector3 p)
    {
        return new Vector3Int(Mathf.RoundToInt(p.x * 20000f), Mathf.RoundToInt(p.y * 20000f), Mathf.RoundToInt(p.z * 20000f));
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    bool TryBuildModel()
    {
        var prefab = Resources.Load<GameObject>(style.model);
        if (prefab == null) return false;

        var inst = Instantiate(prefab, transform);
        inst.name = "Model";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var renderers = inst.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Destroy(inst); return false; }

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = Recolor(mats[i]);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) { smr.sharedMesh = Smoothed(smr.sharedMesh); smr.quality = SkinQuality.Bone4; smr.updateWhenOffscreen = false; }
            else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = Smoothed(mf.sharedMesh); }
        }

        // glTFast keeps the glTF front facing +Z, which is Unity's forward, so no rotation is needed.
        // If a different model ever comes out backwards, set this to 180.
        inst.transform.localRotation = Quaternion.Euler(0, ModelYawOffset, 0);

        // scale to a realistic height for a woman (~1.62 m)
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        if (b.size.y > 0.2f) inst.transform.localScale = Vector3.one * (style.height / b.size.y);

        // animation (glTFast imports legacy Animation clips)
        anim = inst.GetComponentInChildren<Animation>();
        if (anim != null)
        {
            foreach (AnimationState st in anim)
            {
                st.wrapMode = WrapMode.Loop;
                string n = st.name;
                if (n.Contains("Idle_Neutral")) clipIdle = n;
                else if (clipIdle == null && n.EndsWith("Idle")) clipIdle = n;
                if (n.EndsWith("|Walk") || n == "Walk") clipWalk = n;
            }
            if (clipIdle != null)
            {
                anim.Play(clipIdle); clipNow = clipIdle;
                anim[clipIdle].time = Random.value * anim[clipIdle].length;   // so a crowd doesn't breathe in unison
            }
        }
        if (anim == null || clipIdle == null) SetupPlayables(inst);
        bUpL = FindDeep(inst.transform, "UpperLeg.L"); bUpR = FindDeep(inst.transform, "UpperLeg.R");
        bLoL = FindDeep(inst.transform, "LowerLeg.L"); bLoR = FindDeep(inst.transform, "LowerLeg.R");
        bArmL = FindDeep(inst.transform, "UpperArm.L"); bArmR = FindDeep(inst.transform, "UpperArm.R");
        bHips = FindDeep(inst.transform, "Hips");
        if (bUpL) qUpL = bUpL.localRotation; if (bUpR) qUpR = bUpR.localRotation;
        if (bLoL) qLoL = bLoL.localRotation; if (bLoR) qLoR = bLoR.localRotation;
        if (bArmL) qArmL = bArmL.localRotation; if (bArmR) qArmR = bArmR.localRotation;

        headBone = FindDeep(inst.transform, "Head");
        usingModel = true;
        return true;
    }

    /// <summary>Plays the model's real idle + walk clips even if the .glb was imported with Mecanim (non-legacy) clips.</summary>
    void SetupPlayables(GameObject inst)
    {
        var clips = Resources.LoadAll<AnimationClip>(style.model);
        foreach (var c in clips)
        {
            if (c.legacy) continue;
            if (c.name.EndsWith("Idle_Neutral")) cIdle = c;
            else if (cIdle == null && c.name.EndsWith("|Idle")) cIdle = c;
            if (c.name.EndsWith("|Walk") || c.name == "Walk") cWalk = c;
        }
        if (cIdle == null) return;
        var animator = inst.GetComponentInChildren<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        graph = PlayableGraph.Create("Npc_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        pIdle = AnimationClipPlayable.Create(graph, cIdle);
        pWalk = AnimationClipPlayable.Create(graph, cWalk != null ? cWalk : cIdle);
        graph.Connect(pIdle, 0, mixer, 0);
        graph.Connect(pWalk, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        output.SetSourcePlayable(mixer);
        graph.Play();
        pIdle.SetTime(Random.value * cIdle.length);                 // so a crowd doesn't breathe in unison
        usingPlayable = true;
    }

    void OnDestroy() { if (usingPlayable && graph.IsValid()) graph.Destroy(); }

    void PlayableUpdate()
    {
        walkBlend = Mathf.MoveTowards(walkBlend, moving && cWalk != null ? 1f : 0f, Time.deltaTime * 4f);
        mixer.SetInputWeight(0, 1f - walkBlend);
        mixer.SetInputWeight(1, walkBlend);
        // clips imported without "loop time": wrap them by hand
        if (pIdle.GetTime() >= cIdle.length) pIdle.SetTime(pIdle.GetTime() - cIdle.length);
        if (cWalk != null && pWalk.GetTime() >= cWalk.length) pWalk.SetTime(pWalk.GetTime() - cWalk.length);
    }

    void ModelUpdate()
    {
        if (usingPlayable) PlayableUpdate();
        // face the player when idle
        if (!walking && !freezeFacing && lookTarget != null)
        {
            Vector3 d = lookTarget.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 2.5f);
        }
        if (anim != null)
        {
            string want = moving ? (clipWalk ?? clipIdle) : clipIdle;
            if (want != null && want != clipNow) { anim.CrossFade(want, 0.25f); clipNow = want; }
        }
    }

    void LateUpdate()
    {
        ProceduralWalk();
        // small nod in time with the speech
        if (usingModel && headBone != null && anim != null && anim.isPlaying && talk > 0.02f)
            headBone.localRotation *= Quaternion.Euler(talk * Mathf.Sin(Time.time * 7f) * 5f, 0, 0);
    }

    // ------------------------------------------------------------------- API
    public void Say(AudioClip clip)
    {
        voice.Stop();
        voice.clip = clip;
        voice.Play();
    }

    public void Silence() { voice.Stop(); }

    /// <summary>Fallback: swing legs/arms by hand if the model came in without a walk clip.</summary>
    void ProceduralWalk()
    {
        if (!usingModel || usingPlayable || (anim != null && clipWalk != null)) return;
        if (bUpL == null || bUpR == null) return;
        float amt = Mathf.Clamp01(curSpeed / 1.2f);
        walkCycle += Time.deltaTime * Mathf.Max(0.0001f, curSpeed) * 4.4f;
        float sw = Mathf.Sin(walkCycle) * 32f * amt;
        bUpL.localRotation = qUpL * Quaternion.Euler(sw, 0, 0);
        bUpR.localRotation = qUpR * Quaternion.Euler(-sw, 0, 0);
        if (bLoL) bLoL.localRotation = qLoL * Quaternion.Euler(Mathf.Max(0f, -Mathf.Sin(walkCycle)) * 38f * amt, 0, 0);
        if (bLoR) bLoR.localRotation = qLoR * Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(walkCycle)) * 38f * amt, 0, 0);
        if (bArmL) bArmL.localRotation = qArmL * Quaternion.Euler(-sw * 0.7f, 0, 0);
        if (bArmR) bArmR.localRotation = qArmR * Quaternion.Euler(sw * 0.7f, 0, 0);
    }

    /// <summary>Plays a one-off animation clip (e.g. "Wave", "Interact") then returns to idle.</summary>
    public void PlayOnce(string key)
    {
        if (anim == null) return;
        foreach (AnimationState st in anim)
        {
            if (st.name.EndsWith("|" + key) || st.name == key)
            {
                StartCoroutine(OneShot(st.name, st.length));
                return;
            }
        }
    }

    IEnumerator OneShot(string clip, float length)
    {
        anim.CrossFade(clip, 0.2f);
        clipNow = clip;
        yield return new WaitForSeconds(Mathf.Min(length, 3f));
        if (clipIdle != null) { anim.CrossFade(clipIdle, 0.25f); clipNow = clipIdle; }
    }

    /// <summary>Stop turning to face lookTarget (used for NPCs that look elsewhere).</summary>
    public bool freezeFacing;

    /// <summary>
    /// Walks to a point like a person: turns on the spot first, eases into the stride, matches the walk clip's speed
    /// to the real ground speed (no foot-sliding), and eases out when arriving.
    /// </summary>
    public IEnumerator WalkTo(Vector3 localTarget, float speed)
    {
        walking = true;
        curSpeed = 0f;
        while (true)
        {
            Vector3 p = transform.localPosition;
            Vector3 d = localTarget - p;
            d.y = 0;
            float dist = d.magnitude;
            if (dist < 0.06f) break;

            var world = transform.parent != null ? transform.parent.TransformDirection(d) : d;
            var targetRot = Quaternion.LookRotation(world, Vector3.up);
            float ang = Quaternion.Angle(transform.rotation, targetRot);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 300f * Time.deltaTime);

            // only walk once roughly facing the way we're going; slow down on arrival
            float want = ang < 35f ? Mathf.Min(speed, Mathf.Max(0.35f, dist * 1.6f)) : 0f;
            curSpeed = Mathf.MoveTowards(curSpeed, want, 3.2f * Time.deltaTime);
            if (curSpeed > 0f)
            {
                float stepLen = Mathf.Min(dist, curSpeed * Time.deltaTime);
                if (cc != null)
                {
                    Vector3 before = transform.position;
                    Vector3 dirW = world.normalized; dirW.y = 0;
                    cc.Move(dirW * stepLen + Vector3.down * 2f * Time.deltaTime);
                    Vector3 moved = transform.position - before; moved.y = 0;
                    // blocked by something (a person, a cart)? after a moment, step around it
                    stuckT = (curSpeed > 0.3f && moved.magnitude < stepLen * 0.3f) ? stuckT + Time.deltaTime : 0f;
                    if (stuckT > 0.45f) { stuckT = 0f; yield return Sidestep(dirW); }
                }
                else transform.localPosition = p + d.normalized * stepLen;
            }
            moving = curSpeed > 0.08f;
            SetStrideSpeed(curSpeed);
            yield return null;
        }
        walking = false;
        moving = false;
        curSpeed = 0f;
        SetStrideSpeed(1f);
    }

    /// <summary>Quaternius' walk cycle is about 1.25 m/s; scale playback to the real speed.</summary>
    void SetStrideSpeed(float groundSpeed)
    {
        float sp = Mathf.Clamp(groundSpeed / 1.25f, 0.5f, 2.2f);
        if (anim != null && clipWalk != null && anim[clipWalk] != null) anim[clipWalk].speed = sp;
        if (usingPlayable && cWalk != null) pWalk.SetSpeed(sp);
    }

    // ---------------------------------------------------------------- update
    void Update()
    {
        // lip sync from the actual audio
        float level = 0f;
        if (voice.isPlaying)
        {
            voice.GetOutputData(samples, 0);
            float sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            level = Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * 9f);
        }
        talk = Mathf.Lerp(talk, level, Time.deltaTime * 18f);
        if (usingModel) { ModelUpdate(); return; }
        mouth.localScale = new Vector3(0.05f - talk * 0.012f, 0.008f + talk * 0.05f, 0.012f);

        // face the player when idle
        if (!walking && lookTarget != null)
        {
            Vector3 d = lookTarget.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 2.5f);
        }

        // breathing
        float breath = Mathf.Sin(Time.time * 1.6f) * 0.012f;
        torso.localScale = new Vector3(0.34f + breath, 0.26f + breath * 0.5f, 0.22f + breath);

        // walking bob / arm swing
        if (walking)
        {
            walkPhase += Time.deltaTime * 7f;
            visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.025f, 0);
            armL.localRotation = Quaternion.Euler(Mathf.Sin(walkPhase) * 18f, 0, 3f);
            armR.localRotation = Quaternion.Euler(-Mathf.Sin(walkPhase) * 18f, 0, -3f);
        }
        else
        {
            visual.localPosition = Vector3.zero;
            gesture = Mathf.Lerp(gesture, talk, Time.deltaTime * 5f);
            armL.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.1f) * 2f, 0, 3f);
            // right hand gestures while speaking
            armR.localRotation = Quaternion.Euler(-40f * gesture + Mathf.Sin(Time.time * 4f) * 10f * gesture, 0, -4f);
        }

        // head tracking (limited) + a little nod while talking
        if (lookTarget != null)
        {
            Vector3 local = transform.InverseTransformPoint(lookTarget.position) - head.parent.localPosition;
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -50f, 50f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y - 1.66f, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -20f, 20f);
            var target = Quaternion.Euler(pitch + talk * 4f * Mathf.Sin(Time.time * 6f), yaw, 0);
            head.localRotation = Quaternion.Slerp(head.localRotation, target, Time.deltaTime * 6f);
        }
    }
}

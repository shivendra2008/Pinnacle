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
    /// <summary>Realistic avatar (Microsoft Rocketbox, MIT) in Resources/Characters/RB; null = use the low-poly model.</summary>
    public string rb;
    public Color bodyTint = Color.white;      // multiplies the clothing texture (white kurta x saffron = saffron kurta)
    public NpcStyle Rb(string avatar) { rb = avatar; return this; }
    public NpcStyle Rb(string avatar, Color tint) { rb = avatar; bodyTint = tint; return this; }
    public NpcStyle Set(string key, Color c) { map.Add(new KeyValuePair<string, Color>(key, c)); return this; }

    static Color C(float r, float g, float b) { return new Color(r, g, b); }

    public static NpcStyle Kamala()
    {
        return new NpcStyle { model = "Characters/Kamala", height = 1.58f }
            .Set("skin", C(0.60f, 0.40f, 0.27f)).Set("red", C(0.20f, 0.18f, 0.18f)).Set("green", C(0.50f, 0.12f, 0.16f))
            .Set("gold", C(0.92f, 0.74f, 0.28f)).Set("brown", C(0.08f, 0.05f, 0.04f)).Rb("Female_Adult_06");
    }

    public static NpcStyle Reddy()   // Inspector Sonal Reddy, plainclothes: simple salwar kameez
    {
        return new NpcStyle { model = "Characters/Kamala", height = 1.66f }
            .Set("skin", C(0.62f, 0.43f, 0.30f)).Set("red", C(0.06f, 0.05f, 0.05f)).Set("green", C(0.20f, 0.36f, 0.48f))
            .Set("gold", C(0.75f, 0.75f, 0.78f)).Set("brown", C(0.05f, 0.04f, 0.04f)).Rb("Business_Female_01");
    }

    public static NpcStyle Bhaskar()  // auto driver: khaki uniform
    {
        return new NpcStyle { model = "Characters/Casual", height = 1.70f }
            .Set("skin_darker", C(0.45f, 0.29f, 0.19f)).Set("skin", C(0.52f, 0.34f, 0.23f)).Set("hair", C(0.05f, 0.04f, 0.04f))
            .Set("eyebrows", C(0.05f, 0.04f, 0.04f)).Set("eye", C(0.03f, 0.02f, 0.02f))
            .Set("lightbrown", C(0.62f, 0.56f, 0.38f)).Set("lightblue", C(0.42f, 0.38f, 0.27f))
            .Set("red_dark", C(0.20f, 0.14f, 0.10f)).Set("white", C(0.35f, 0.3f, 0.25f)).Rb("Male_Adult_05", C(0.92f, 0.86f, 0.7f));
    }

    public static NpcStyle Vikram()   // chai seller: white kurta, grey hair
    {
        return new NpcStyle { model = "Characters/Casual", height = 1.66f }
            .Set("skin_darker", C(0.47f, 0.30f, 0.20f)).Set("skin", C(0.55f, 0.36f, 0.24f)).Set("hair", C(0.55f, 0.55f, 0.55f))
            .Set("eyebrows", C(0.35f, 0.35f, 0.35f)).Set("eye", C(0.03f, 0.02f, 0.02f))
            .Set("lightbrown", C(0.88f, 0.85f, 0.76f)).Set("lightblue", C(0.22f, 0.20f, 0.22f))
            .Set("red_dark", C(0.25f, 0.18f, 0.12f)).Set("white", C(0.5f, 0.45f, 0.4f)).Rb("Male_Adult_15");
    }

    public static NpcStyle Thug()     // dark suit
    {
        return new NpcStyle { model = "Characters/Suit", height = 1.82f }
            .Set("skin", C(0.58f, 0.40f, 0.28f)).Set("hair", C(0.04f, 0.04f, 0.04f)).Set("eyebrows", C(0.04f, 0.04f, 0.04f))
            .Set("eye", C(0.02f, 0.02f, 0.02f)).Set("suit", C(0.04f, 0.04f, 0.05f)).Set("white", C(0.08f, 0.08f, 0.09f))
            .Set("tie", C(0.03f, 0.03f, 0.03f)).Set("black", C(0.02f, 0.02f, 0.02f)).Rb("Business_Male_03", C(0.7f, 0.7f, 0.72f));
    }

    static readonly string[] WomenRb = { "Female_Adult_06", "Female_Adult_06", "Female_Adult_09", "Female_Adult_03", "Business_Female_03" };
    static readonly string[] MenRb = { "Male_Adult_15", "Male_Adult_15", "Wood_Male_01", "Male_Adult_17", "Male_Adult_05", "Male_Adult_03", "Business_Male_05" };

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
            .Set("green", saris[r.Next(saris.Length)]).Set("gold", C(0.92f, 0.74f, 0.28f)).Set("brown", C(0.05f, 0.04f, 0.04f))
            .Rb(WomenRb[r.Next(WomenRb.Length)], Color.Lerp(Color.white, saris[r.Next(saris.Length)], 0.45f));
    }

    /// <summary>A child (same casual model, much smaller).</summary>
    public static NpcStyle Child(int seed)
    {
        var st = Crowd(seed);
        st.height = 1.05f + (float)new System.Random(seed).NextDouble() * 0.2f;
        st.rb = null;        // the realistic set used here is adults only; a shrunken adult looks wrong
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
        st.Rb(MenRb[r.Next(MenRb.Length)], Color.Lerp(Color.white, shirt, 0.35f));
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
        if (!(Realistic && k.TryBuildRocketbox()) && !k.TryBuildModel()) k.BuildProcedural();
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

    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

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
        if (rideVehicle != null && !rbClips)      // no skeleton to pose: at least sit at the right height, facing forward
        {
            transform.rotation = rideVehicle.rotation;
            transform.position = rideVehicle.TransformPoint(rideSeat) - rideVehicle.up * (style != null ? style.height : 1.65f) * 0.5f;
        }
        if (rbMode) { RbAnimate(); return; }
        ProceduralWalk();
        // small nod in time with the speech
        if (usingModel && headBone != null && anim != null && anim.isPlaying && talk > 0.02f)
            headBone.localRotation *= Quaternion.Euler(talk * Mathf.Sin(Time.time * 7f) * 5f, 0, 0);
    }


    // ============================================================ realistic avatars
    /// <summary>Set to false to go back to the low-poly models everywhere (they are also the automatic fallback).</summary>
    public static bool Realistic = true;

    bool rbMode, waveIsWave;
    float waveUntil, walkAmt, gestureAmt, lookYaw, lookPitch, sideL = -1f, rbBaseY;
    Transform rbRoot, rSpine, rNeck, rHead, rJaw, rPelvis;
    Transform[] rArm = new Transform[2], rFore = new Transform[2], rHand = new Transform[2], rThigh = new Transform[2], rCalf = new Transform[2], rFoot = new Transform[2];
    readonly List<Transform> rBones = new List<Transform>();
    readonly List<Quaternion> rRest = new List<Quaternion>();
    static readonly Dictionary<string, Material> rbMats = new Dictionary<string, Material>();

    static Material RbMaterial(string matName, Color tint, Material fallback)
    {
        string baseName = matName.Replace(" (Instance)", "").Trim();
        if (baseName.EndsWith("_color")) baseName = baseName.Substring(0, baseName.Length - 6);   // importer may name materials after the texture
        string key = baseName + "#" + ColorUtility.ToHtmlStringRGB(tint);
        Material m;
        if (rbMats.TryGetValue(key, out m) && m != null) return m;
        var col = Resources.Load<Texture2D>("Characters/RB/" + baseName + "_color");
        if (col == null) return fallback;
        var nrm = Resources.Load<Texture2D>("Characters/RB/" + baseName + "_normal");
        bool head = baseName.EndsWith("_head"), cut = baseName.EndsWith("_opacity"), body = baseName.EndsWith("_body");
        m = Mat(body ? tint : Color.white, head ? 0.4f : cut ? 0.35f : 0.2f);
        m.name = key;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", col);
        m.mainTexture = col;
        if (nrm != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
        if (cut)        // hair cards, lashes: alpha-tested and two-sided
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.38f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); m.renderQueue = 2450;
        }
        rbMats[key] = m;
        return m;
    }

    Transform Bone(Dictionary<string, Transform> all, string n)
    {
        Transform t;
        if (!all.TryGetValue(n, out t)) return null;
        if (!rBones.Contains(t)) { rBones.Add(t); rRest.Add(t.localRotation); }
        return t;
    }

    bool TryBuildRocketbox()
    {
        if (style == null || string.IsNullOrEmpty(style.rb)) return false;
        var prefab = Resources.Load<GameObject>("Characters/RB/" + style.rb);
        if (prefab == null) return false;
        var inst = Instantiate(prefab, transform);
        inst.name = "Model";
        inst.transform.localPosition = Vector3.zero;        // keep the importer's own rotation / scale (axis + unit conversion)
        var renderers = inst.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Destroy(inst); return false; }
        foreach (var a in inst.GetComponentsInChildren<Animator>(true)) a.enabled = false;

        var all = new Dictionary<string, Transform>();
        foreach (var t in inst.GetComponentsInChildren<Transform>(true)) if (!all.ContainsKey(t.name)) all[t.name] = t;
        if (!all.ContainsKey("Bip01 Head") || !all.ContainsKey("Bip01 L UpperArm")) { Destroy(inst); return false; }

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null) mats[i] = RbMaterial(mats[i].name, style.bodyTint, mats[i]);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) { smr.quality = SkinQuality.Bone4; smr.updateWhenOffscreen = true; }
        }

        // height
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        if (b.size.y > 0.2f) inst.transform.localScale *= style.height / b.size.y;

        // face the same way as the NPC object: the feet tell us which way the model was authored
        var humanAnimator = inst.GetComponentInChildren<Animator>(true);
        bool human = humanAnimator != null && humanAnimator.avatar != null && humanAnimator.avatar.isValid && humanAnimator.avatar.isHuman;
        if (!human)
        {
            Vector3 f = Vector3.zero;          // both feet, so a splayed stance does not skew the result
            foreach (var sd in new[] { "L", "R" })
                if (all.ContainsKey("Bip01 " + sd + " Foot") && all.ContainsKey("Bip01 " + sd + " Toe0"))
                    f += all["Bip01 " + sd + " Toe0"].position - all["Bip01 " + sd + " Foot"].position;
            f.y = 0;
            if (f.sqrMagnitude > 1e-6f)
            {
                float ang = Vector3.SignedAngle(f, transform.forward, Vector3.up);
                inst.transform.RotateAround(transform.position, Vector3.up, ang);
            }
        }
        // stand on the ground, centred on the NPC origin
        Transform pel = all.ContainsKey("Bip01 Pelvis") ? all["Bip01 Pelvis"] : null;
        if (pel != null)
        {
            Vector3 off = transform.position - pel.position; off.y = 0;
            inst.transform.position += off;
        }
        float minY = float.MaxValue;
        foreach (var n in new[] { "Bip01 L Toe0", "Bip01 R Toe0", "Bip01 L Foot", "Bip01 R Foot" })
            if (all.ContainsKey(n)) minY = Mathf.Min(minY, all[n].position.y);
        if (minY < float.MaxValue) inst.transform.position += Vector3.up * (transform.position.y - minY + 0.03f * style.height / 1.7f);
        rbBaseY = inst.transform.localPosition.y;

        rbRoot = inst.transform;
        rPelvis = pel;
        rSpine = Bone(all, "Bip01 Spine1"); rNeck = Bone(all, "Bip01 Neck"); rHead = Bone(all, "Bip01 Head"); rJaw = Bone(all, "Bip01 MJaw");
        string[] side = { "L", "R" };
        for (int i = 0; i < 2; i++)
        {
            rArm[i] = Bone(all, "Bip01 " + side[i] + " UpperArm"); rFore[i] = Bone(all, "Bip01 " + side[i] + " Forearm"); rHand[i] = Bone(all, "Bip01 " + side[i] + " Hand");
            rThigh[i] = Bone(all, "Bip01 " + side[i] + " Thigh"); rCalf[i] = Bone(all, "Bip01 " + side[i] + " Calf"); rFoot[i] = Bone(all, "Bip01 " + side[i] + " Foot");
        }
        if (rArm[0] != null) sideL = transform.InverseTransformPoint(rArm[0].position).x <= 0f ? -1f : 1f;
        headBone = rHead;
        walkCycle = Random.value * 6.28f;
        usingModel = true; rbMode = true;
        rToe[0] = all.ContainsKey("Bip01 L Toe0") ? all["Bip01 L Toe0"] : null; rToe[1] = all.ContainsKey("Bip01 R Toe0") ? all["Bip01 R Toe0"] : null;
        if (rJaw != null) jawRest = rJaw.localRotation;
        lastPos = transform.position; bornAt = Time.time;
        if (human && SetupRbClips(humanAnimator)) return true;
        RbAnimate();                 // leave the T-pose before the first frame is drawn
        return true;
    }

    /// <summary>Point a bone at its child along a direction given in the NPC's own space (x right, y up, z forward).</summary>
    void Aim(Transform bone, Transform child, Vector3 localDir)
    {
        if (bone == null || child == null) return;
        Vector3 cur = child.position - bone.position;
        if (cur.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(cur, transform.TransformDirection(localDir)) * bone.rotation;
    }

    static Vector3 Swing(float side, float outward, float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return new Vector3(side * outward, -Mathf.Cos(a), Mathf.Sin(a));
    }

    /// <summary>
    /// The avatars come without animation clips, so the body is posed in code every frame: arms down from the T-pose,
    /// breathing, weight shift, head following the player, jaw and hand moving with the voice, and a walk cycle whose
    /// stride follows the real ground speed.
    /// </summary>
    void RbAnimate()
    {
        if (rbClips) { RbClips(); return; }
        for (int i = 0; i < rBones.Count; i++) if (rBones[i] != null) rBones[i].localRotation = rRest[i];
        float dt = Time.deltaTime, time = Time.time + walkCycle;      // per-character offset so a crowd is not in sync

        walkAmt = Mathf.MoveTowards(walkAmt, moving ? Mathf.Clamp01(curSpeed / 0.9f) : 0f, dt * 3.5f);
        if (walkAmt > 0.001f) walkCycle += dt * Mathf.Max(0.6f, curSpeed) * 4.6f;
        float ph = walkCycle, sn = Mathf.Sin(ph), cs = Mathf.Cos(ph);
        bool waving = Time.time < waveUntil;
        gestureAmt = Mathf.MoveTowards(gestureAmt, waving ? 1f : Mathf.Clamp01(talk * 2.2f) * 0.55f, dt * (waving ? 5f : 2.5f));

        // torso: breathing + slow weight shift, a little lean into the walk
        if (rSpine != null)
        {
            float breath = Mathf.Sin(time * 1.7f) * 1.1f;
            float sway = Mathf.Sin(time * 0.45f) * 1.2f * (1f - walkAmt) + sn * 2.2f * walkAmt;
            rSpine.rotation = Quaternion.AngleAxis(-breath + 3f * walkAmt, transform.right) * Quaternion.AngleAxis(sway, transform.forward) * rSpine.rotation;
        }
        if (rbRoot != null)
            rbRoot.localPosition = new Vector3(rbRoot.localPosition.x, rbBaseY + Mathf.Cos(ph * 2f) * 0.014f * walkAmt, rbRoot.localPosition.z);

        // legs
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? sideL : -sideL, dir = i == 0 ? 1f : -1f;
            float a = 27f * walkAmt * sn * dir;
            float bend = 46f * walkAmt * Mathf.Max(0f, cs * dir);
            Aim(rThigh[i], rCalf[i], Swing(s, 0.035f, a + bend * 0.35f));
            Aim(rCalf[i], rFoot[i], Swing(s, 0.035f, a - bend));
        }

        // arms: down from the T-pose, swinging against the legs; the right hand talks and waves
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? sideL : -sideL, dir = i == 0 ? -1f : 1f;
            float sw = 22f * walkAmt * sn * dir + Mathf.Sin(time * 0.9f + i) * 1.5f;
            Vector3 up = Swing(s, 0.17f, sw), fo = Swing(s, 0.10f, sw + 12f + 14f * walkAmt);
            if (i == 1 && gestureAmt > 0.01f)
            {
                float g = gestureAmt, beat = Mathf.Sin(Time.time * (waving && waveIsWave ? 9f : 3.3f));
                if (waving && waveIsWave)
                {
                    up = Vector3.Slerp(up, new Vector3(s * 0.75f, 0.35f, 0.3f), g);
                    fo = Vector3.Slerp(fo, new Vector3(s * (0.15f + 0.3f * beat), 1f, 0.2f), g);
                }
                else
                {
                    up = Vector3.Slerp(up, new Vector3(s * 0.22f, -1f, 0.38f), g);
                    fo = Vector3.Slerp(fo, new Vector3(-s * 0.15f, 0.05f + 0.18f * beat, 1f), g);
                }
            }
            Aim(rArm[i], rFore[i], up.normalized);
            Aim(rFore[i], rHand[i], fo.normalized);
        }

        // head: follow the player (limited), nod with the voice
        float wantYaw = 0f, wantPitch = 0f;
        if (lookTarget != null && rHead != null && !walking)
        {
            Vector3 l = transform.InverseTransformPoint(lookTarget.position) - transform.InverseTransformPoint(rHead.position);
            wantYaw = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -55f, 55f);
            wantPitch = Mathf.Clamp(-Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg, -22f, 25f);
        }
        lookYaw = Mathf.Lerp(lookYaw, wantYaw, dt * 4f); lookPitch = Mathf.Lerp(lookPitch, wantPitch, dt * 4f);
        float nod = talk * Mathf.Sin(Time.time * 6.5f) * 3.5f + Mathf.Sin(time * 0.7f) * 0.8f;
        if (rNeck != null) rNeck.rotation = Quaternion.AngleAxis(lookYaw * 0.35f, transform.up) * rNeck.rotation;
        if (rHead != null)
            rHead.rotation = Quaternion.AngleAxis(lookYaw * 0.65f, transform.up) * Quaternion.AngleAxis(lookPitch + nod, rHead.parent != null ? Vector3.Cross(transform.up, HeadForward()).normalized : transform.right) * rHead.rotation;
        // jaw follows the loudness of the voice
        if (rJaw != null && talk > 0.01f)
            rJaw.rotation = Quaternion.AngleAxis(Mathf.Clamp01(talk * 1.6f) * 11f, Vector3.Cross(transform.up, HeadForward()).normalized) * rJaw.rotation;
    }

    // ------------------------------------------------ motion-capture clips (the normal path)
    bool rbClips, fitted;
    AnimationClip cRun; AnimationClipPlayable pRun;

    // ---- riding a two-wheeler: seated on the saddle, hands on the grips, feet on the pegs (two-bone IK over the idle clip)
    Transform rideVehicle; Vector3 rideSeat, ridePeg, rideGrip; float rideBlend;
    public bool IsRiding => rideVehicle != null;

    /// <summary>Sit this character on a vehicle. Points are in the vehicle's local space; peg and grip are for the right side (x > 0).</summary>
    public void Ride(Transform vehicle, Vector3 seat, Vector3 peg, Vector3 grip)
    {
        rideVehicle = vehicle; rideSeat = seat; ridePeg = peg; rideGrip = grip; rideBlend = 0f;
        walking = false; moving = false; freezeFacing = true; lookTarget = null;
        if (cc != null) cc.enabled = false;
        transform.SetParent(vehicle, true);
    }

    static void AimWorld(Transform bone, Transform child, Vector3 worldDir)
    {
        if (bone == null || child == null) return;
        Vector3 cur = child.position - bone.position;
        if (cur.sqrMagnitude < 1e-8f || worldDir.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(cur, worldDir) * bone.rotation;
    }

    /// <summary>Two-bone IK: puts the end of a limb on target, bending the middle joint towards hint.</summary>
    static void Reach(Transform upper, Transform mid, Transform end, Vector3 target, Vector3 hint)
    {
        if (upper == null || mid == null || end == null) return;
        float a = Vector3.Distance(upper.position, mid.position), b = Vector3.Distance(mid.position, end.position);
        Vector3 to = target - upper.position;
        float d = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.005f);
        Vector3 dir = to.normalized;
        Vector3 perp = hint - Vector3.Dot(hint, dir) * dir;
        perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.Cross(dir, Vector3.right).normalized;
        float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
        AimWorld(upper, mid, dir * cosA + perp * sinA);
        AimWorld(mid, end, target - mid.position);
    }

    void RidePose(float dt)
    {
        var v = rideVehicle;
        rideBlend = Mathf.MoveTowards(rideBlend, 1f, dt * 2.6f);
        float k = Mathf.SmoothStep(0f, 1f, rideBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, v.rotation, k);
        if (rPelvis != null) transform.position += (v.TransformPoint(rideSeat) + v.up * 0.07f - rPelvis.position) * k;
        if (k < 0.5f) return;                               // still swinging a leg over
        if (rSpine != null) rSpine.rotation = Quaternion.AngleAxis(16f, v.right) * rSpine.rotation;
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? sideL : -sideL;
            Reach(rThigh[i], rCalf[i], rFoot[i], v.TransformPoint(new Vector3(s * ridePeg.x, ridePeg.y + 0.09f, ridePeg.z)), v.forward + v.up * 0.5f + v.right * s * 0.35f);
            Reach(rArm[i], rFore[i], rHand[i], v.TransformPoint(new Vector3(s * rideGrip.x, rideGrip.y + 0.03f, rideGrip.z - 0.05f)), -v.up + v.right * s * 0.7f - v.forward * 0.25f);
        }
        if (rHead != null) rHead.rotation = Quaternion.AngleAxis(-12f, v.right) * rHead.rotation;      // eyes on the road
        if (rJaw != null) rJaw.localRotation = jawRest;
    }

    float measSpeed, bornAt, runBlend;
    Vector3 lastPos;
    Quaternion jawRest;
    readonly Transform[] rToe = new Transform[2];
    static readonly Dictionary<string, AnimationClip> clipCache = new Dictionary<string, AnimationClip>();

    static AnimationClip Clip(string res)
    {
        AnimationClip c;
        if (clipCache.TryGetValue(res, out c) && c != null) return c;
        foreach (var k in Resources.LoadAll<AnimationClip>("Anim/" + res))
            if (!k.name.StartsWith("__preview")) { c = k; break; }
        clipCache[res] = c;
        return c;
    }

    /// <summary>Plays real motion-capture idle + walk on the avatar (Humanoid retargeting), blended by how fast the NPC is actually moving.</summary>
    bool SetupRbClips(Animator an)
    {
        bool fem = style.rb.Contains("Female");
        cIdle = Clip(fem ? "f_Idle" : "m_Idle"); cWalk = Clip(fem ? "f_WalkForwards" : "m_WalkForwards");
        if (cIdle == null || cWalk == null) return false;
        an.enabled = true;
        an.runtimeAnimatorController = null;
        an.applyRootMotion = false;
        an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("Npc_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Anim", an);
        cRun = Clip(fem ? "f_RunForwards" : "m_RunForwards");
        mixer = AnimationMixerPlayable.Create(graph, 3);
        pIdle = AnimationClipPlayable.Create(graph, cIdle);
        pWalk = AnimationClipPlayable.Create(graph, cWalk);
        pRun = AnimationClipPlayable.Create(graph, cRun != null ? cRun : cWalk);
        pIdle.SetApplyFootIK(false); pWalk.SetApplyFootIK(false); pRun.SetApplyFootIK(false);
        graph.Connect(pIdle, 0, mixer, 0);
        graph.Connect(pWalk, 0, mixer, 1);
        graph.Connect(pRun, 0, mixer, 2);
        mixer.SetInputWeight(0, 1f); mixer.SetInputWeight(1, 0f); mixer.SetInputWeight(2, 0f);
        output.SetSourcePlayable(mixer);
        graph.Play();
        pIdle.SetTime(Random.value * cIdle.length);          // so a crowd does not breathe in unison
        pWalk.SetTime(Random.value * cWalk.length);
        graph.Evaluate(0f);                                   // out of the T-pose before the first frame is drawn
        rbClips = true;
        return true;
    }

    void AimW(Transform bone, Transform child, Vector3 localDir, float w)
    {
        if (bone == null || child == null || w <= 0.001f) return;
        Vector3 cur = child.position - bone.position;
        if (cur.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, transform.TransformDirection(localDir)), w) * bone.rotation;
    }

    void RbClips()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;                     // paused: the Animator has not rewritten the pose, so add nothing on top

        // walk blend from the real movement, whoever is moving this NPC (WalkTo, a cutscene, a shove)
        Vector3 p = transform.position, d = p - lastPos; d.y = 0; lastPos = p;
        if (rideVehicle != null)
        {
            walkBlend = Mathf.MoveTowards(walkBlend, 0f, dt * 5f);
            mixer.SetInputWeight(0, 1f - walkBlend); mixer.SetInputWeight(1, walkBlend); mixer.SetInputWeight(2, 0f);
            RidePose(dt);
            return;
        }
        measSpeed = Mathf.Lerp(measSpeed, d.magnitude / dt, Mathf.Clamp01(dt * 10f));
        walkBlend = Mathf.MoveTowards(walkBlend, Mathf.Clamp01((measSpeed - 0.06f) / 0.45f), dt * 4f);
        runBlend = Mathf.MoveTowards(runBlend, cRun != null ? Mathf.Clamp01((measSpeed - 2.1f) / 0.9f) : 0f, dt * 3f);
        mixer.SetInputWeight(0, 1f - walkBlend);
        mixer.SetInputWeight(1, walkBlend * (1f - runBlend));
        mixer.SetInputWeight(2, walkBlend * runBlend);
        pWalk.SetSpeed(Mathf.Clamp(measSpeed / 1.3f, 0.7f, 1.8f));      // stride follows ground speed: no foot sliding
        pRun.SetSpeed(Mathf.Clamp(measSpeed / 3.4f, 0.8f, 1.35f));

        // once, when standing still: make sure the animated body faces forward and the feet touch the ground
        if (!fitted && Time.time - bornAt > 0.25f && walkBlend < 0.02f && rbRoot != null)
        {
            fitted = true;
            Vector3 f = Vector3.zero; float minY = float.MaxValue;
            for (int i = 0; i < 2; i++)
                if (rToe[i] != null && rFoot[i] != null) { f += rToe[i].position - rFoot[i].position; minY = Mathf.Min(minY, rToe[i].position.y); }
            f.y = 0;
            if (f.sqrMagnitude > 1e-6f && Mathf.Abs(Vector3.SignedAngle(f, transform.forward, Vector3.up)) > 100f)
                rbRoot.RotateAround(transform.position, Vector3.up, 180f);
            if (minY < float.MaxValue)
            {
                float lift = transform.position.y + 0.025f * style.height / 1.7f - minY;
                if (Mathf.Abs(lift) > 0.02f && Mathf.Abs(lift) < 0.6f) rbRoot.position += Vector3.up * lift;
            }
        }

        // layered on top of the clip: the right hand talks and waves, the head follows the player, the jaw follows the voice
        bool waving = Time.time < waveUntil;
        gestureAmt = Mathf.MoveTowards(gestureAmt, waving ? 1f : Mathf.Clamp01(talk * 2.2f) * 0.6f * (1f - walkBlend), dt * (waving ? 5f : 2.5f));
        if (gestureAmt > 0.01f)
        {
            float s = -sideL, beat = Mathf.Sin(Time.time * (waving && waveIsWave ? 9f : 3.3f));
            Vector3 up, fo;
            if (waving && waveIsWave) { up = new Vector3(s * 0.75f, 0.35f, 0.3f); fo = new Vector3(s * (0.15f + 0.3f * beat), 1f, 0.2f); }
            else { up = new Vector3(s * 0.22f, -1f, 0.38f); fo = new Vector3(-s * 0.15f, 0.05f + 0.18f * beat, 1f); }
            AimW(rArm[1], rFore[1], up.normalized, gestureAmt);
            AimW(rFore[1], rHand[1], fo.normalized, gestureAmt);
        }
        float wantYaw = 0f, wantPitch = 0f;
        if (lookTarget != null && rHead != null && walkBlend < 0.3f)
        {
            Vector3 l = transform.InverseTransformPoint(lookTarget.position) - transform.InverseTransformPoint(rHead.position);
            wantYaw = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -55f, 55f);
            wantPitch = Mathf.Clamp(-Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg, -22f, 25f);
        }
        lookYaw = Mathf.Lerp(lookYaw, wantYaw, dt * 4f); lookPitch = Mathf.Lerp(lookPitch, wantPitch, dt * 4f);
        float nod = talk * Mathf.Sin(Time.time * 6.5f) * 3.5f;
        Vector3 rightAxis = Vector3.Cross(transform.up, HeadForward()).normalized;
        if (rNeck != null) rNeck.rotation = Quaternion.AngleAxis(lookYaw * 0.35f, transform.up) * rNeck.rotation;
        if (rHead != null) rHead.rotation = Quaternion.AngleAxis(lookYaw * 0.65f, transform.up) * Quaternion.AngleAxis(lookPitch + nod, rightAxis) * rHead.rotation;
        if (rJaw != null)
        {
            rJaw.localRotation = jawRest;
            if (talk > 0.01f) rJaw.rotation = Quaternion.AngleAxis(Mathf.Clamp01(talk * 1.6f) * 11f, rightAxis) * rJaw.rotation;
        }
    }

    Vector3 HeadForward()
    {
        return Quaternion.AngleAxis(lookYaw, transform.up) * transform.forward;
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
        if (rbMode) { waveUntil = Time.time + (key == "Wave" ? 1.8f : 1.2f); waveIsWave = key == "Wave"; return; }
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

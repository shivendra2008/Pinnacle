using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The Kali shrine's murti. Resources/Idols/Kali.fbx (the project's own asset) is not a carved statue: it is a single
/// "ImagePlane" mesh with a "TrueDepthMaterial", a painted picture of Dakshina Kali (2048 x 3072, background, halo and
/// trident included) pushed out into relief. Seen straight on it reads as the goddess; from the side its rectangle and
/// its stretched edges show. So it is set as what it is, a painted relief icon in a carved stone frame (a prabhavali)
/// whose borders hide the picture's edges, deep enough in the sanctum that its sides cannot be seen from the courtyard.
/// The garland hangs from the frame, above the image, clear of her face and hands; a low warm lamp gives the relief its
/// form, with a soft shadow on the pedestal, and a little cool light from the doorway.
/// If the asset does not load, the old fallback (Uma-Maheshvara, a different deity) is not used: the shrine shows a
/// veiled murti instead, and a development build says so on screen and in the log.
/// </summary>
public partial class Act3Story
{
    // read by the play-test route and shown in development builds
    public string idolReport = "";
    string devWarning = "";

    void PlaceKali(Vector3 ped)
    {
        var prefab = Resources.Load<GameObject>("Idols/Kali");
        if (prefab == null)
        {
            devWarning = "DEV: Resources/Idols/Kali did not load - the shrine shows a veiled stand-in";
            Debug.LogError("[Temple] Resources/Idols/Kali did not load; showing a veiled murti (not the Uma-Maheshvara scan)");
            idolReport = "kali=missing";
            VeiledMurti(ped + new Vector3(0f, 0.5f, 0.05f));
            return;
        }
        var go = Instantiate(prefab, transform);
        go.name = "Idol_Kali";
        PropKit.StripSceneParts(go);                    // the file carries a Cinema 4D camera
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { idolReport = "kali=no renderers"; return; }
        go.transform.rotation = Quaternion.Euler(0f, IdolYaw, 0f);
        Bounds b = Bounds(rs);
        // a plane that came in lying flat is stood up (its thin side must face the door)
        if (b.size.y < 0.5f * Mathf.Max(b.size.x, b.size.z)) { go.transform.rotation = Quaternion.Euler(-90f, IdolYaw, 0f); b = Bounds(rs); }
        if (b.size.z > b.size.x * 1.2f) { go.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.rotation; b = Bounds(rs); }
        float height = 1.72f;
        if (b.size.y > 0.001f) go.transform.localScale *= height / b.size.y;
        b = Bounds(rs);
        // seated on the pedestal, set a little back so the frame stands in front of its edges
        Vector3 basePos = ped + new Vector3(0f, 0.5f, 0.18f);
        go.transform.position += new Vector3(basePos.x - b.center.x, basePos.y - b.min.y, basePos.z - b.center.z);
        b = Bounds(rs);
        // a painted relief: matt paint, no mirror gloss
        int verts = 0; string tex = "";
        foreach (var r in rs)
        {
            r.shadowCastingMode = ShadowCastingMode.On;
            var mf = r.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) verts += mf.sharedMesh.vertexCount;
            var ms = r.materials;
            foreach (var m in ms)
            {
                if (m == null) continue;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.28f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                if (t != null) tex = t.name + " " + t.width + "x" + t.height;
            }
            r.materials = ms;
        }
        idolReport = "kali=loaded verts=" + verts + " size=" + b.size.ToString("F2") + " tex=" + tex + " shader=" + (rs[0].sharedMaterial != null ? rs[0].sharedMaterial.shader.name : "-");
        Prabhavali(b);
        Garland(b);
        // the lamp at her feet, low and to the left, gives the relief its form; a soft shadow on the pedestal
        var key = new GameObject("MurtiKeyLight").AddComponent<Light>(); key.transform.SetParent(transform, false);
        key.transform.position = new Vector3(b.center.x - 0.75f, b.min.y + 0.35f, b.min.z - 1.15f);
        key.transform.LookAt(new Vector3(b.center.x, b.min.y + b.size.y * 0.62f, b.center.z));
        key.type = LightType.Spot; key.spotAngle = 62f; key.innerSpotAngle = 28f; key.range = 5f; key.intensity = 7f; key.color = new Color(1f, 0.66f, 0.36f);
        CinematicLook.Shadows(key, 0.85f, 0.02f, 0.3f);
        // and a little cool moonlight from the doorway, so the dark side is not black
        var fill = new GameObject("MurtiFill").AddComponent<Light>(); fill.transform.SetParent(transform, false);
        fill.transform.position = new Vector3(b.center.x + 0.6f, b.min.y + 1.6f, b.min.z - 2.6f);
        fill.transform.LookAt(b.center);
        fill.type = LightType.Spot; fill.spotAngle = 50f; fill.range = 5f; fill.intensity = 0.9f; fill.color = new Color(0.62f, 0.7f, 1f); fill.shadows = LightShadows.None;
    }

    static Bounds Bounds(Renderer[] rs) { Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }

    /// <summary>A carved sandstone frame round the icon: two pilasters, a lintel with a little crest, a moulded base;
    /// its inner edges overlap the picture's borders so the rectangle never shows, and its depth hides the relief's sides.</summary>
    void Prabhavali(Bounds b)
    {
        var stone = ProcTex.Mat(ProcTex.Get("sandstone_floor"), new Color(0.82f, 0.66f, 0.5f), new Vector2(1f, 2f), 0.9f, 1.2f);
        var dark = GameKit.Lit(new Color(0.2f, 0.14f, 0.1f), 0.35f);
        float w = b.size.x, h = b.size.y, d = b.size.z + 0.08f, zc = b.center.z - 0.02f;
        float overlap = 0.06f * w, pil = 0.15f;
        var root = new GameObject("Prabhavali").transform; root.SetParent(transform, false);
        foreach (float s in new[] { -1f, 1f })
        {
            float x = b.center.x + s * (w * 0.5f - overlap + pil * 0.5f);
            GameKit.Box(root, "Pilaster", new Vector3(x, b.min.y + h * 0.5f + 0.06f, zc), new Vector3(pil, h + 0.12f, d), stone);
            GameKit.Box(root, "PilasterFlute", new Vector3(x, b.min.y + h * 0.5f, b.min.z - 0.045f), new Vector3(pil * 0.35f, h * 0.8f, 0.01f), dark);
        }
        float topY = b.max.y - 0.06f * h;
        GameKit.Box(root, "Lintel", new Vector3(b.center.x, topY + 0.1f, zc), new Vector3(w - 2f * overlap + 2f * pil + 0.08f, 0.2f, d + 0.02f), stone);
        GameKit.Box(root, "LintelBand", new Vector3(b.center.x, topY + 0.1f, b.min.z - 0.05f), new Vector3(w * 0.8f, 0.04f, 0.01f), dark);
        var crest = Turned("FrameCrest", new Vector3(b.center.x, topY + 0.2f, zc), new[] { new Vector2(0f, 0.16f), new Vector2(0.06f, 0.12f), new Vector2(0.16f, 0.07f), new Vector2(0.26f, 0.02f) }, stone, 12);
        crest.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        GameKit.Box(root, "FrameBase", new Vector3(b.center.x, b.min.y + 0.04f, zc - 0.02f), new Vector3(w - 2f * overlap + 2f * pil + 0.1f, 0.08f, d + 0.06f), stone);
        // the picture's bottom edge sits behind the base's lip
        GameKit.Box(root, "FrameBaseLip", new Vector3(b.center.x, b.min.y + 0.1f, b.min.z - 0.04f), new Vector3(w - 2f * overlap, 0.06f, 0.03f), stone);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
    }

    /// <summary>Marigolds hung as a toran across the top of the frame, sagging under their weight, and a strand down each
    /// pilaster: above and beside the image, never across her face or her hands.</summary>
    void Garland(Bounds b)
    {
        var red = new Color(0.75f, 0.1f, 0.06f); var saffron = new Color(1f, 0.55f, 0.06f);
        float w = b.size.x, overlap = 0.06f * w, pil = 0.15f;
        float xL = b.center.x - (w * 0.5f - overlap + pil * 0.5f), xR = -xL + 2f * b.center.x;
        float topY = b.max.y - 0.06f * b.size.y + 0.2f;
        float z = b.min.z - 0.12f;
        const int N = 26;
        for (int i = 0; i <= N; i++)
        {
            float u = i / (float)N, x = Mathf.Lerp(xL, xR, u);
            float sag = 0.1f * (1f - Mathf.Pow(2f * u - 1f, 2f));          // a hanging chain is near enough a parabola over this span
            Sp("Marigold", new Vector3(x, topY - sag, z), 0.07f, i % 4 == 0 ? red : saffron, 0.15f);
        }
        foreach (float x in new[] { xL, xR })
            for (int i = 0; i < 12; i++)
                Sp("Marigold", new Vector3(x + Mathf.Sin(i * 0.9f) * 0.008f, topY - 0.04f - i * 0.062f, z + 0.01f), 0.066f, i % 3 == 0 ? red : saffron, 0.15f);
    }

    /// <summary>The stand-in if the murti's file is missing: a figure veiled in red cloth with a silver crown above,
    /// as a murti is covered at certain hours. It keeps the shrine a Kali shrine; it is not another deity.</summary>
    void VeiledMurti(Vector3 at)
    {
        var cloth = ProcTex.Mat(ProcTex.Get("fabric"), new Color(0.5f, 0.04f, 0.05f), new Vector2(1f, 2f), 0.2f, 0.8f);
        var body = Turned("VeiledMurti", at, new[] { new Vector2(0f, 0.42f), new Vector2(0.5f, 0.36f), new Vector2(1.0f, 0.3f), new Vector2(1.25f, 0.22f), new Vector2(1.45f, 0.17f), new Vector2(1.6f, 0.06f) }, cloth, 16);
        body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        Turned("Mukut", at + Vector3.up * 1.58f, new[] { new Vector2(0f, 0.11f), new Vector2(0.12f, 0.13f), new Vector2(0.22f, 0.06f), new Vector2(0.27f, 0.01f) }, GameKit.Lit(new Color(0.78f, 0.78f, 0.8f), 0.8f, 1f), 16);
    }

    protected override void OnGUI()
    {
        base.OnGUI();
        if (string.IsNullOrEmpty(devWarning) || !(Debug.isDebugBuild || Application.isEditor)) return;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        st.normal.textColor = new Color(1f, 0.4f, 0.3f);
        GUI.Label(new Rect(Screen.width - 620f, 8f, 610f, 24f), devWarning, st);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The Gallery's three caskets, each made as what it is: cast bronze worn bright at the edges where hands have
/// touched it, with green-brown patina in its recesses and heavy hinge knuckles; carved bone plaques on a dark wood
/// carcass, yellowed at the joins, with small brass hinges; forged iron banded with riveted straps, rust in the corners
/// and a heavy hasp. Each opens with its own weight and sound. A brass plate in front of each carries its inscription,
/// readable from where the player stands at the table. Examining one previews what it leads to and, if it is sealed,
/// says why in the terms of the case; the confirmation is a separate decision on the compass.
/// The rules are those of Sealed(): Justice needs 18 pieces of evidence, Sacrifice is always open, Revolution needs Raj's
/// recordings or the municipal emails.
/// </summary>
public partial class Act5Story
{
    readonly bool[] casketSeen = new bool[3];
    Transform routeExhibit;
    // read by the play-test route
    int examined, steppedBack;
    string lastCasketCard = "";

    static readonly string[] Inscriptions =
    {
        "\"Who chooseth me offers a single throat to the wolf, yet frees the entire flock from the shearer.\"",
        "\"Who chooseth me shall build a sanctuary upon a foundation of dust and quiet graves.\"",
        "\"Who chooseth me shall grant sight to the blind by setting the entire city on fire.\"",
    };

    static string[] Wrap(string s, int width)
    {
        var words = s.Split(' '); var lines = new System.Collections.Generic.List<string>(); string cur = "";
        foreach (var w in words) { if (cur.Length + w.Length + 1 > width && cur.Length > 0) { lines.Add(cur); cur = w; } else cur = cur.Length == 0 ? w : cur + " " + w; }
        if (cur.Length > 0) lines.Add(cur);
        return lines.ToArray();
    }

    GameObject Bit(Transform p, string n, Vector3 at, Vector3 size, Material m, bool shadow = true)
    {
        var g = GameKit.Box(p, n, at, size, m);
        g.GetComponent<Renderer>().shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return g;
    }

    void BuildCaskets()
    {
        string[] names = { "BRONZE", "BONE", "IRON" };
        Color[] glow = { new Color(1f, 0.75f, 0.35f), new Color(0.85f, 0.9f, 1f), new Color(1f, 0.35f, 0.08f) };
        for (int i = 0; i < 3; i++)
        {
            var root = new GameObject("Casket_" + names[i]).transform; root.SetParent(transform, false);
            root.position = G + new Vector3(-1.9f + i * 1.9f, 0.945f, 2f);
            var lidPivot = new GameObject("LidPivot").transform; lidPivot.SetParent(root, false); lidPivot.localPosition = new Vector3(0, 0.4f, 0.25f);
            if (i == 0) BronzeCasket(root, lidPivot);
            else if (i == 1) BoneCasket(root, lidPivot);
            else IronCasket(root, lidPivot);
            // the glow inside, seen when the lid lifts
            Bit(root, "InnerGlow", new Vector3(0f, 0.385f, 0f), new Vector3(0.7f, 0.01f, 0.4f), GameKit.Glow(glow[i] * 0.4f, glow[i], 1.6f), false);
            caskets[i] = root; lids[i] = lidPivot;
            casketLights[i] = GameKit.PointLight(root, new Vector3(0, 0.6f, -0.4f), glow[i], 1.2f, 2.5f);
            InscriptionPlate(root.position + new Vector3(0f, 0f, -0.62f), names[i], Inscriptions[i]);
        }
    }

    /// <summary>A brass plate lying on the velvet in front of a casket, tipped up towards the player, its words engraved dark.</summary>
    void InscriptionPlate(Vector3 at, string name, string text)
    {
        var plate = new GameObject("Inscription_" + name).transform; plate.SetParent(transform, false);
        plate.position = at + Vector3.up * 0.03f; plate.rotation = Quaternion.Euler(-28f, 0f, 0f);
        var brass = GameKit.Lit(new Color(0.72f, 0.54f, 0.22f), 0.7f, 1f);
        Bit(plate, "Plate", Vector3.zero, new Vector3(0.62f, 0.006f, 0.17f), brass);
        Bit(plate, "PlateStand", new Vector3(0f, -0.02f, 0.06f), new Vector3(0.5f, 0.04f, 0.02f), GameKit.Lit(new Color(0.2f, 0.12f, 0.06f), 0.4f));
        var ink = new Color(0.12f, 0.07f, 0.03f);
        GameKit.Solid(GameKit.Text(plate, name, new Vector3(0f, 0.0035f, 0.062f), Quaternion.Euler(90f, 0f, 0f), 0.018f, ink));
        var lines = Wrap(text, 36);
        for (int l = 0; l < lines.Length && l < 4; l++)
            GameKit.Solid(GameKit.Text(plate, lines[l], new Vector3(0f, 0.0035f, 0.03f - l * 0.03f), Quaternion.Euler(90f, 0f, 0f), 0.0165f, ink));
    }

    void BronzeCasket(Transform root, Transform lid)
    {
        var bronze = GameKit.Lit(new Color(0.55f, 0.36f, 0.17f), 0.55f, 1f);
        var worn = GameKit.Lit(new Color(0.88f, 0.64f, 0.34f), 0.82f, 1f);       // rubbed bright where hands go
        var patina = GameKit.Lit(new Color(0.22f, 0.27f, 0.2f), 0.25f, 0.4f);    // green-brown in the recesses
        Bit(root, "Body", new Vector3(0, 0.2f, 0), new Vector3(0.78f, 0.36f, 0.48f), bronze);
        // a moulded plinth and a top band, worn bright on their edges
        Bit(root, "Plinth", new Vector3(0, 0.025f, 0), new Vector3(0.84f, 0.05f, 0.54f), bronze);
        Bit(root, "PlinthEdge", new Vector3(0, 0.051f, -0.268f), new Vector3(0.84f, 0.006f, 0.006f), worn);
        Bit(root, "Band", new Vector3(0, 0.375f, 0), new Vector3(0.8f, 0.03f, 0.5f), bronze);
        Bit(root, "BandEdge", new Vector3(0, 0.39f, -0.25f), new Vector3(0.8f, 0.006f, 0.006f), worn);
        foreach (float x in new[] { -0.39f, 0.39f }) Bit(root, "CornerEdge", new Vector3(x, 0.2f, -0.24f), new Vector3(0.008f, 0.36f, 0.008f), worn);
        // recessed panels on the front and ends, dark with patina, framed
        Bit(root, "FrontPanel", new Vector3(0, 0.2f, -0.242f), new Vector3(0.6f, 0.22f, 0.004f), patina);
        foreach (float x in new[] { -0.392f, 0.392f }) Bit(root, "EndPanel", new Vector3(x, 0.2f, 0f), new Vector3(0.004f, 0.22f, 0.34f), patina);
        // a ring pull on the front, polished by use, and four ball feet
        var ring = GameKit.Cyl(root, "RingPull", new Vector3(0f, 0.2f, -0.252f), 0.035f, 0.008f, worn); ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (float x in new[] { -0.35f, 0.35f }) foreach (float z in new[] { -0.22f, 0.22f }) GameKit.Sphere(root, "Foot", new Vector3(x, 0.01f, z), 0.05f, bronze);
        // two heavy hinge knuckles at the back
        foreach (float x in new[] { -0.26f, 0.26f }) { var k = GameKit.Cyl(root, "HingeKnuckle", new Vector3(x, 0.4f, 0.255f), 0.018f, 0.12f, bronze); k.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); }
        // the lid, domed in steps, its rim bright
        Bit(lid, "Lid", new Vector3(0, 0.03f, -0.25f), new Vector3(0.82f, 0.06f, 0.52f), bronze);
        Bit(lid, "LidDome", new Vector3(0, 0.075f, -0.25f), new Vector3(0.66f, 0.04f, 0.38f), bronze);
        Bit(lid, "LidRim", new Vector3(0, 0.061f, -0.51f), new Vector3(0.82f, 0.006f, 0.008f), worn);
        Bit(lid, "LidBoss", new Vector3(0, 0.1f, -0.25f), new Vector3(0.12f, 0.02f, 0.12f), worn);
    }

    void BoneCasket(Transform root, Transform lid)
    {
        var carcass = GameKit.Lit(new Color(0.16f, 0.1f, 0.06f), 0.45f);
        var brass = GameKit.Lit(new Color(0.7f, 0.52f, 0.24f), 0.7f, 1f);
        Bit(root, "Body", new Vector3(0, 0.2f, 0), new Vector3(0.78f, 0.38f, 0.48f), carcass);
        // carved bone plaques set into the carcass, each a slightly different age; the dark lines between are the joins
        var rnd = new System.Random(66);
        for (int col = 0; col < 5; col++)
            for (int row = 0; row < 2; row++)
            {
                float tone = 0.6f + (float)rnd.NextDouble() * 0.1f;          // aged bone, not white: it caught the casket light like paper
                var bone = GameKit.Lit(new Color(tone, tone * 0.93f, tone * 0.76f), 0.38f);
                Bit(root, "BonePlaque", new Vector3(-0.31f + col * 0.155f, 0.105f + row * 0.18f, -0.243f), new Vector3(0.145f, 0.165f, 0.006f), bone);
                // a carved lotus (a raised boss and four petals), as carved bone caskets have
                Bit(root, "BoneCarving", new Vector3(-0.31f + col * 0.155f, 0.105f + row * 0.18f, -0.248f), new Vector3(0.04f, 0.04f, 0.004f), GameKit.Lit(new Color(tone * 0.9f, tone * 0.82f, tone * 0.6f), 0.3f));
            }
        foreach (float x in new[] { -0.392f, 0.392f })
            for (int row = 0; row < 2; row++)
                Bit(root, "BoneEndPlaque", new Vector3(x, 0.105f + row * 0.18f, 0f), new Vector3(0.006f, 0.165f, 0.42f), GameKit.Lit(new Color(0.64f, 0.58f, 0.46f), 0.36f));
        foreach (float x in new[] { -0.39f, 0.39f }) Bit(root, "CornerMount", new Vector3(x, 0.38f, -0.24f), new Vector3(0.03f, 0.03f, 0.03f), brass);
        foreach (float x in new[] { -0.2f, 0.2f }) { var k = GameKit.Cyl(root, "HingeKnuckle", new Vector3(x, 0.4f, 0.252f), 0.009f, 0.06f, brass); k.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); }
        Bit(root, "Clasp", new Vector3(0f, 0.35f, -0.25f), new Vector3(0.04f, 0.05f, 0.008f), brass);
        // the lid: a lighter frame with three plaques, a little thinner than the bronze's, so it lifts lightly
        Bit(lid, "Lid", new Vector3(0, 0.025f, -0.25f), new Vector3(0.8f, 0.05f, 0.5f), carcass);
        for (int k = 0; k < 3; k++)
            Bit(lid, "LidPlaque", new Vector3(-0.26f + k * 0.26f, 0.052f, -0.25f), new Vector3(0.24f, 0.006f, 0.42f), GameKit.Lit(new Color(0.66f, 0.6f, 0.48f) * (0.95f + k * 0.03f), 0.38f));
    }

    void IronCasket(Transform root, Transform lid)
    {
        var iron = GameKit.Lit(new Color(0.09f, 0.09f, 0.1f), 0.32f, 0.9f);
        var strap = GameKit.Lit(new Color(0.13f, 0.13f, 0.14f), 0.42f, 0.95f);
        var rust = GameKit.Lit(new Color(0.34f, 0.16f, 0.07f), 0.15f, 0.25f);
        Bit(root, "Body", new Vector3(0, 0.2f, 0), new Vector3(0.78f, 0.38f, 0.48f), iron);
        // riveted straps round the body
        foreach (float x in new[] { -0.26f, 0.26f })
        {
            Bit(root, "Strap", new Vector3(x, 0.2f, -0.243f), new Vector3(0.05f, 0.38f, 0.008f), strap);
            for (int r = 0; r < 4; r++) GameKit.Sphere(root, "Rivet", new Vector3(x, 0.05f + r * 0.1f, -0.249f), 0.012f, strap);
        }
        Bit(root, "StrapBottom", new Vector3(0, 0.03f, -0.243f), new Vector3(0.78f, 0.04f, 0.008f), strap);
        // rust where water stood: the bottom corners and under the straps
        foreach (float x in new[] { -0.36f, 0.36f }) Bit(root, "Rust", new Vector3(x, 0.05f, -0.2455f), new Vector3(0.06f, 0.07f, 0.002f), rust, false);
        Bit(root, "RustStreak", new Vector3(0.27f, 0.12f, -0.2485f), new Vector3(0.012f, 0.1f, 0.002f), rust, false);
        // a heavy hasp and staple on the front
        Bit(root, "Staple", new Vector3(0f, 0.34f, -0.25f), new Vector3(0.05f, 0.03f, 0.02f), strap);
        foreach (float x in new[] { -0.3f, 0.3f }) { var k = GameKit.Cyl(root, "HingeKnuckle", new Vector3(x, 0.4f, 0.256f), 0.022f, 0.1f, strap); k.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); }
        // the lid: thick plate with its own straps and the hasp hanging from it
        Bit(lid, "Lid", new Vector3(0, 0.035f, -0.25f), new Vector3(0.82f, 0.07f, 0.52f), iron);
        foreach (float x in new[] { -0.26f, 0.26f }) Bit(lid, "LidStrap", new Vector3(x, 0.073f, -0.25f), new Vector3(0.05f, 0.008f, 0.52f), strap);
        Bit(lid, "Hasp", new Vector3(0f, -0.02f, -0.515f), new Vector3(0.06f, 0.09f, 0.012f), strap);
        Bit(lid, "LidRust", new Vector3(-0.3f, 0.0715f, -0.48f), new Vector3(0.08f, 0.002f, 0.05f), rust, false);
    }

    /// <summary>Raj's phone, Rohan's printed email or Reddy's sign-out sheet: the thing this player's Act IV left them with,
    /// laid in front of the casket it bears on, under a glass dome like everything else in his collection.</summary>
    void BuildRouteExhibit()
    {
        var d = SaveSystem.Data;
        int casket = -1; Transform thing = null;
        var root = new GameObject("RouteExhibit").transform; root.SetParent(transform, false);
        if (d.allyReddy)
        {
            casket = 0;
            thing = PaperKit.SignOutSheet(root, Vector3.zero, 0f);
        }
        else if (d.allyRaj || d.hasRecordings)
        {
            casket = 2;
            thing = new GameObject("RajsPhone").transform; thing.SetParent(root, false);
            GameKit.Box(thing, "PhoneBody", Vector3.zero, new Vector3(0.072f, 0.0085f, 0.152f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.75f, 0.3f));
            GameKit.Box(thing, "PhoneScreen", new Vector3(0f, 0.0046f, 0f), new Vector3(0.064f, 0.0008f, 0.138f), GameKit.Glow(new Color(0.15f, 0.2f, 0.25f), new Color(0.35f, 0.55f, 0.75f), 0.8f));
        }
        else if (d.allyRohan || d.hasEmails)
        {
            casket = 2;
            thing = PaperKit.NotebookPage(root, Vector3.zero, 0f, "RE: Basti Nagar transfer", "Keep the 1965 order in the file");
        }
        if (thing == null || casket < 0) { Destroy(root.gameObject); return; }
        root.position = G + new Vector3(-1.9f + casket * 1.9f + 0.52f, 0.95f, 1.5f);
        root.rotation = Quaternion.Euler(0f, -12f, 0f);
        thing.localScale = Vector3.one * (thing.name == "SignOutSheet" ? 0.7f : 1f);
        var dome = Turned("ExhibitDome", root.position, new[] { new Vector2(0f, 0.13f), new Vector2(0.1f, 0.13f), new Vector2(0.15f, 0.11f), new Vector2(0.19f, 0.06f), new Vector2(0.2f, 0.0f) }, PaperKitGlass(), 20);
        dome.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Turned("ExhibitBase", root.position - Vector3.up * 0.005f, new[] { new Vector2(0f, 0.15f), new Vector2(0.015f, 0.15f) }, GameKit.Lit(new Color(0.18f, 0.1f, 0.05f), 0.5f), 20);
        routeExhibit = root;
    }

    static Material exhibitGlass;
    static Material PaperKitGlass()
    {
        if (exhibitGlass != null) return exhibitGlass;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "DomeGlass" };
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_BlendModePreserveSpecular", 1f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        var c = new Color(0.8f, 0.85f, 0.85f, 0.08f); m.color = c; m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.95f);
        m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        exhibitGlass = m;
        return m;
    }

    /// <summary>What a casket leads to and, if sealed, why: in the case's own terms, with this player's numbers.</summary>
    string CasketCard(int i)
    {
        var d = SaveSystem.Data;
        int n = SaveSystem.EvidenceCount;
        string body;
        if (i == 0)
        {
            body = "IF YOU CHOOSE IT:  Priya walks into the Municipal Court at ten with the original 1965 file, and you stand beside her. Deshpande's lawyer will call it theft; your evidence has to be stronger than the charge.\n\n";
            body += Sealed(0)
                ? "SEALED.  A judge will ask what you can prove. You have " + n + " pieces of evidence; a case this size needs " + EvidenceForJustice + ". " + (EvidenceForJustice - n) + " more would have opened it."
                : "OPEN.  " + n + " pieces of evidence: a case that can stand up in court.";
            if (d.allyReddy) body += "\n\nInspector Reddy has the original 1942 deed in her evidence room. She will bring it.";
            else if (d.allyRaj) body += "\n\nRaj Malhotra has said he will testify.";
            else if (d.allyRohan) body += "\n\nRohan will bring the emails he found.";
        }
        else if (i == 1)
        {
            body = "IF YOU CHOOSE IT:  you take the evidence in alone, and Priya stays hidden until it is safe. The eviction can be frozen. Without her standing in court, the case itself cannot be won.\n\nALWAYS OPEN.";
        }
        else
        {
            body = "IF YOU CHOOSE IT:  every document, recording and email goes to the press before dawn. The city will know by morning. The courts will move slower than the crowd, and Priya will be the face of it.\n\n";
            if (Sealed(2)) body += "SEALED.  The press needs something it can publish: Raj's recordings or the municipal emails. You have neither.";
            else
            {
                string what = d.hasRecordings && d.hasEmails ? "Raj's recordings and the municipal emails" : d.hasRecordings ? "Raj's phone: six months of his own voice" : "the municipal emails from Rohan's laptop";
                body += "OPEN.  You have " + what + ".";
            }
        }
        return body;
    }

    IEnumerator ExamineCasket(int i)
    {
        BeginBusy();
        examined++;
        yield return Turn(caskets[i].position + Vector3.up * 0.2f, 0.45f);
        string[] titles = { "THE BRONZE CASKET  -  Justice", "THE BONE CASKET  -  Sacrifice", "THE IRON CASKET  -  Revolution" };
        // his remark the first time only; the inscription is on the plate, so the card carries what it leads to
        if (!casketSeen[i])
        {
            casketSeen[i] = true;
            yield return Talk(i == 0 ? "gal_bronze" : i == 1 ? "gal_bone" : "gal_iron", curator);
            EndTalk();
        }
        lastCasketCard = CasketCard(i);
        yield return ShowCard(titles[i], lastCasketCard);
        if (Sealed(i))
        {
            yield return Talk(i == 0 ? "gal_sealed_bronze" : "gal_sealed_iron", curator);
            EndTalk();
            BeginFree("Read the inscriptions and choose a casket", G + new Vector3(0, 1.6f, 2f), "The three caskets");
            yield break;
        }
        // the confirmation: a separate decision (inspecting a casket never chooses it)
        string[] what = { "the bronze casket: Justice", "the bone casket: Sacrifice", "the iron casket: Revolution" };
        dlgSpeaker = "THE CURATOR";
        dlgText = "Well? Is this your casket?";
        yield return Choose(new[] { "Open it", "Step back" }, new[] { "Open " + what[i] + ".", "Step back from the table." }, null, null, "THE CURATOR", null);
        EndTalk();
        if (menuResult == 0) chosen = i;
        else { steppedBack++; BeginFree("Read the inscriptions and choose a casket", G + new Vector3(0, 1.6f, 2f), "The three caskets"); }
    }

    // ---- opening: each with its own weight and sound
    static AudioClip Creak(float f0, float f1, float len, float grit, int seed)
    {
        const int SR = 44100; int n = (int)(SR * len); var d = new float[n]; var r = new System.Random(seed);
        float ph = 0f, a = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, u = t / len;
            float f = Mathf.Lerp(f0, f1, Mathf.SmoothStep(0f, 1f, u)) * (1f + 0.02f * Mathf.Sin(t * 37f));
            ph += 2f * Mathf.PI * f / SR;
            float stick = 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(ph * 0.5f));
            float w = (float)(r.NextDouble() * 2 - 1); a += (w - a) * 0.3f;
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((len - t) / 0.15f);
            d[i] = (Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 2.01f) * 0.25f) * stick * env * (1f - grit) + a * grit * env * 1.5f;
        }
        Norm(d, 0.6f);
        var c = AudioClip.Create("casket_" + seed, n, 1, SR, false); c.SetData(d, 0); return c;
    }

    IEnumerator OpenCasket(int i)
    {
        // bronze: heavy and smooth, a low resonant creak and a ring at the top; bone: light and quick, a dry clack;
        // iron: a grinding lift that catches twice before it gives, and a clank
        float dur = i == 0 ? 1.7f : i == 1 ? 0.75f : 2.3f;
        var src = new GameObject("CasketSound").AddComponent<AudioSource>();
        src.transform.SetParent(caskets[i], false); src.transform.localPosition = new Vector3(0f, 0.4f, 0.25f);
        src.spatialBlend = 0.85f; src.minDistance = 1f; src.maxDistance = 14f;
        if (i == 0) src.PlayOneShot(Creak(150f, 230f, 1.6f, 0.15f, 11), 0.9f);
        else if (i == 1) { src.PlayOneShot(Tone(620f, 0.09f, 60f, 0.6f, true), 0.7f); src.PlayOneShot(Creak(900f, 700f, 0.5f, 0.6f, 12), 0.35f); }
        else src.PlayOneShot(Creak(70f, 95f, 2.2f, 0.75f, 13), 1f);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = t / dur, k;
            if (i == 0) k = Mathf.SmoothStep(0f, 1f, u * u * (3f - 2f * u));                 // slow to start, heavy
            else if (i == 1) k = 1f - (1f - u) * (1f - u);                                   // light: quick, easing out
            else k = u < 0.3f ? u / 0.3f * 0.22f : u < 0.42f ? 0.22f : u < 0.66f ? 0.22f + (u - 0.42f) / 0.24f * 0.3f : u < 0.76f ? 0.52f : 0.52f + (u - 0.76f) / 0.24f * 0.48f;   // two catches
            lids[i].localRotation = Quaternion.Euler(-110f * k, 0, 0);
            casketLights[i].intensity = Mathf.Lerp(1.2f, 9f, k);
            casketLights[i].range = Mathf.Lerp(2.5f, 9f, k);
            yield return null;
        }
        if (i == 0) src.PlayOneShot(Tone(523f, 1.4f, 2.2f, 0.4f), 0.6f);
        else if (i == 2) src.PlayOneShot(Tone(110f, 0.6f, 9f, 0.7f, true), 0.8f);
    }
}

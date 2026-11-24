using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The three endings, staged.
///  Justice: the hearing turns on one object from the player's Act IV (Reddy's 1942 deed, Raj's phone, Rohan's
///  printed email), carried up and laid in front of the bench by the witness who brought it; the judge looks at it,
///  rules, and the room is silent a moment before it answers. Arjun sits at Priya's table, close enough to read her
///  face and Deshpande's.
///  Sacrifice: the quieter court without Priya, then Arjun alone outside: the court door shuts behind him on the steps
///  and he reads Priya's message on his phone.
///  Revolution: the release staged in three short scenes on a coherent clock to sunrise: a reporter's phone at
///  5:10, a shop's shutter going up on the news at 5:55, Priya hearing people outside at 6:20, the roof at 6:25.
/// Sets used only by an ending (the courtroom, the roof, the steps, the reporter's flat, the shop) are built during
/// the white-out after the choice, not when Act V opens.
/// </summary>
public partial class Act5Story
{
    static readonly Vector3 Steps = new Vector3(60f, 0f, 160f);     // the court steps (Sacrifice)
    static readonly Vector3 Flat = new Vector3(-60f, 0f, 0f);       // a reporter's flat (Revolution)
    static readonly Vector3 Shop = new Vector3(-60f, 0f, 40f);      // a shop on Bazaar Road (Revolution)
    bool endingSetsBuilt, stepsBuilt, montageBuilt;
    MarketWorld signKit;
    // a time and place in the lower left (the montage's clock), drawn in OnGUI
    string stampA, stampB; float stampAt = -10f;
    // read by the play-test route
    string endingLog = "";
    Transform presentedThing;

    void Stamp(string time, string place) { stampA = time; stampB = place; stampAt = Time.time; endingLog += time + " " + place + "; "; }

    /// <summary>The courtroom and the roof, built once the choice is made (they are not needed in the shelter or the Gallery).</summary>
    void EnsureEndingSets()
    {
        if (endingSetsBuilt) return;
        endingSetsBuilt = true;
        BuildCourtroom();
        BuildTerrace();
        TexturePass();
    }

    IEnumerator MoveTo(Transform t, Vector3 a, Vector3 b, float dur, float arc = 0f)
    {
        for (float k = 0f; k < dur; k += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, k / dur);
            t.position = Vector3.Lerp(a, b, u) + Vector3.up * arc * Mathf.Sin(u * Mathf.PI);
            yield return null;
        }
        t.position = b;
    }

    static Transform HeadOf(CharacterNPC n) { if (n == null) return null; var h = n.HeadForFace; return h != null ? h : n.transform; }

    Transform PhoneThing(Transform parent)
    {
        var p = new GameObject("Phone").transform; p.SetParent(parent, false);
        GameKit.Box(p, "PhoneBody", Vector3.zero, new Vector3(0.072f, 0.0085f, 0.152f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.75f, 0.3f));
        var scr = GameKit.Box(p, "PhoneScreen", new Vector3(0f, 0.0046f, 0f), new Vector3(0.064f, 0.0008f, 0.138f), GameKit.Glow(new Color(0.15f, 0.2f, 0.25f), new Color(0.4f, 0.6f, 0.8f), 1.2f));
        scr.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return p;
    }

    // ===================================================================== JUSTICE
    /// <summary>A witness carries his piece of evidence up to the clerk's table under the bench, lays it down for the judge
    /// to read, says what it is, and steps back.</summary>
    IEnumerator WitnessPresents(CharacterNPC w, Transform thing, string line, float slotX)
    {
        var hand = new GameObject("WitnessHand").transform; hand.SetParent(w.transform, false);
        hand.localPosition = new Vector3(0.2f, 1.02f, 0.32f);
        thing.SetParent(hand, false); thing.localPosition = new Vector3(0f, 0.02f, 0.04f); thing.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        thing.gameObject.SetActive(true);
        w.Hold(1, hand, 0.4f);
        w.freezeFacing = false; w.lookTarget = null;
        yield return Turn(w.EyePoint(), 0.6f);
        Vector3 slot = C + new Vector3(slotX, 0f, 5.55f);
        yield return w.WalkTo(slot, 1.05f, 9f);
        w.freezeFacing = true;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime) { w.transform.rotation = Quaternion.Slerp(w.transform.rotation, Quaternion.identity, t / 0.35f); yield return null; }
        // down on the clerk's table, turned so the bench can read it
        Vector3 table = C + new Vector3(slotX, 0.79f, 6.12f);
        hand.SetParent(transform, true);
        yield return MoveTo(hand, hand.position, table + Vector3.up * 0.05f, 0.55f, 0.04f);
        thing.SetParent(transform, true);
        thing.position = table; thing.rotation = Quaternion.Euler(0f, 180f, 0f);
        sfx.PlayOneShot(EvidenceFolio.Put(), 0.5f);
        presentedThing = thing;
        w.Release(1, 0.5f);
        if (judge != null) judge.lookTarget = thing;
        if (deshpande != null) deshpande.lookTarget = thing;           // she turns to look at it
        w.lookTarget = HeadOf(judge);
        yield return Wait(0.5f);
        yield return Talk(line, w);
        EndTalk();
        yield return w.WalkTo(slot + new Vector3(0.9f, 0f, -0.9f), 0.9f, 4f);
        w.lookTarget = HeadOf(judge);
    }

    IEnumerator JusticeEnding()
    {
        var d = SaveSystem.Data;
        EnsureEndingSets();
        Show(false, false);
        // dawn over the old quarter: shutters, temple bells, chai stoves
        yield return ToTerrace(new Color(1f, 0.62f, 0.4f), new Color(0.45f, 0.5f, 0.75f), new Color(1f, 0.85f, 0.55f), 6f, 0f);
        fade = 0f; yield return WhiteIn();
        sfx.PlayOneShot(Tone(392f, 2.5f, 1.2f, 0.25f), 0.5f);
        Stamp("6:25 AM", "The old quarter");
        yield return Narrate("end1_vo_dawn");
        // the hearing: Arjun at Priya's table, the bench ahead, Deshpande across the aisle
        yield return FadeOut(1.2f);
        yield return ToCourtroom(new Vector3(2.15f, 0.1f, 4.0f), -6f);
        var e = cam.localPosition; e.y = Mathf.Max(1.1f, e.y - 0.47f); cam.localPosition = e;
        var p = Npc(C + new Vector3(0.95f, 0f, 4.05f), -10f, Cast.Priya(), "PriyaCourt", false);
        p.lookTarget = HeadOf(lawyer);
        // her own file, held at her side until she presents it
        var file = new GameObject("PriyaCourtFile").transform; file.SetParent(p.transform, false);
        file.localPosition = new Vector3(0.12f, 1.05f, 0.28f); file.localRotation = Quaternion.Euler(-60f, 0f, 0f);
        GameKit.Box(file, "File", Vector3.zero, new Vector3(0.24f, 0.012f, 0.34f), GameKit.Lit(new Color(0.72f, 0.6f, 0.42f), 0.15f));
        var fh = new GameObject("PriyaFileHand").transform; fh.SetParent(file, false); fh.localPosition = new Vector3(0.1f, 0f, -0.1f);
        p.Hold(1, fh, 0.01f);
        if (deshpande != null)
        {
            deshpande.transform.position = C + new Vector3(-2.95f, 0f, 3.7f);
            deshpande.Sit(C + new Vector3(-2.95f, 0.46f, 4.06f), 0f, 0.42f, true);
            deshpande.lookTarget = HeadOf(judge);
        }
        CharacterNPC raj = null, rohan = null, reddy = null;
        if (d.allyRaj) raj = Npc(C + new Vector3(2.5f, 0f, 1.2f), -10f, Cast.Raj(), "RajCourt", false);
        if (d.allyRohan) rohan = Npc(C + new Vector3(1.5f, 0f, 1.2f), 0f, Cast.Rohan(), "RohanCourt", false);
        var kam = Npc(C + new Vector3(3.5f, 0f, 1.2f), -20f, Cast.Kamala(), "KamalaCourt", false);
        kam.lookTarget = HeadOf(p);
        Stamp("10:00 AM", "Municipal Court");
        yield return FadeIn(1f);
        BeginBusy();
        yield return Turn(lawyer.EyePoint(), 0.7f);
        yield return Talk("end1_lawyer", lawyer);
        p.lookTarget = HeadOf(judge);
        yield return Turn(p.EyePoint(), 0.6f);
        yield return Talk("end1_priya", p);
        EndTalk();
        // the object from Arjun's night: carried up by the person who brought it
        if (d.allyReddy)
        {
            reddy = Npc(C + new Vector3(0f, 0f, -9.5f), 0f, Cast.Reddy(), "ReddyCourt", false);
            var deed = PaperKit.OldDeed(transform, Vector3.zero, 0f); deed.gameObject.SetActive(false);
            yield return WitnessPresents(reddy, deed, "end1_reddy", -0.35f);
        }
        if (raj != null) yield return WitnessPresents(raj, PhoneThing(transform), "end1_raj", 0.25f);
        if (rohan != null) yield return WitnessPresents(rohan, PaperKit.EmailPrintout(transform, Vector3.zero, 0f), "end1_rohan", 0.6f);
        // the judge reads it; Deshpande looks down; Priya watches the bench
        yield return Turn(judge.EyePoint(), 0.8f);
        yield return Wait(1.1f);
        if (deshpande != null) { var lap = new GameObject("DeshpandeLooksDown").transform; lap.SetParent(transform, false); lap.position = C + new Vector3(-2.95f, 0.75f, 4.6f); deshpande.lookTarget = lap; }
        judge.lookTarget = cam;
        yield return Talk("end1_judge", judge);
        EndTalk();
        // the decision lands before the room answers: a silence, one stroke of the gavel, then the room
        yield return Wait(1.3f);
        sfx.PlayOneShot(gavelClip, 0.9f);
        yield return Wait(0.5f);
        sfx.PlayOneShot(cheerClip, 0.45f);
        p.lookTarget = cam;
        if (deshpande != null) deshpande.lookTarget = HeadOf(lawyer);
        yield return Turn(p.EyePoint(), 0.7f);
        yield return Wait(2.2f);
        // late morning over the city
        yield return FadeOut(1f);
        yield return ToTerrace(new Color(1f, 0.78f, 0.45f), new Color(0.5f, 0.65f, 0.9f), new Color(1f, 0.92f, 0.7f), 14f, 0f);
        var e2 = cam.localPosition; e2.y = standEye > 0f ? standEye : 1.62f; cam.localPosition = e2;
        yield return FadeIn(0.6f);
        yield return Narrate("end1_vo_end");
        yield return ShowCard("EPILOGUE  -  JUSTICE",
            "Councilor Meghna Deshpande was arrested and charged with fraud, forgery and criminal conspiracy.\n\n" +
            "The Chandnipur Renewal Project was permanently halted. The 1942 deed was upheld by the High Court, securing the homes of Basti Nagar's 500 families.\n\n" +
            (d.allyRaj ? "Raj Malhotra received a reduced sentence for his testimony. He now builds affordable housing - honestly.\n\n" : "") +
            (d.allyReddy ? "Inspector Sonal Reddy was promoted to Deputy Commissioner for her undercover work.\n\n" : "") +
            (d.allyRohan ? "Rohan Kapoor went back to college, and never filed a document without reading it again.\n\n" : "") +
            "Priya Kapoor founded the Chandnipur Land Rights Foundation, which has since protected over 10,000 families.\n\n" +
            "Arjun Mehra never accepted payment for the case.");
        yield return BackToGallery("CHANDNIPUR  -  JUSTICE", new[] { "endJ_01", "endJ_02", "endJ_03", "endJ_04", "endJ_05" }, false);
        inGallery = false;
        RollCredits("JUSTICE", false);
    }
    float standEye = -1f;

    // ===================================================================== SACRIFICE
    Transform stepsDoorL, stepsDoorR;

    void BuildCourtSteps()
    {
        if (stepsBuilt) return;
        stepsBuilt = true;
        var stone = new Color(0.72f, 0.68f, 0.6f);
        var root = new GameObject("CourtSteps").transform; root.SetParent(transform, false);
        Tex(B("StepsStreet", Steps + new Vector3(0f, -0.05f, -6f), new Vector3(16f, 0.1f, 10f), Color.white, true), "asphalt", 2.5f, new Color(0.42f, 0.41f, 0.4f));
        Tex(B("StepsPavement", Steps + new Vector3(0f, 0.04f, -1.2f), new Vector3(16f, 0.08f, 2.4f), Color.white, true), "blocks", 1f, new Color(0.62f, 0.6f, 0.56f));
        for (int i = 0; i < 6; i++)
            Tex(B("CourtStep", Steps + new Vector3(0f, 0.08f + i * 0.16f + 0.08f, 0.2f + i * 0.4f), new Vector3(6f, 0.16f, 0.4f + (5 - i) * 0.001f), Color.white, true), "sandstone_floor", 1.2f, stone);
        Tex(B("CourtLanding", Steps + new Vector3(0f, 1.0f, 3.4f), new Vector3(7f, 0.08f, 2.2f), Color.white, true), "sandstone_floor", 1.4f, stone);
        // the front: a plastered facade, two columns, a deep doorway with tall double doors
        var wall = new Color(0.86f, 0.8f, 0.68f);
        Tex(B("CourtFacadeL", Steps + new Vector3(-3.1f, 3.5f, 4.7f), new Vector3(4f, 5.2f, 0.4f), wall, true), "plaster", 2.2f, wall);
        Tex(B("CourtFacadeR", Steps + new Vector3(3.1f, 3.5f, 4.7f), new Vector3(4f, 5.2f, 0.4f), wall, true), "plaster", 2.2f, wall);
        Tex(B("CourtFacadeHead", Steps + new Vector3(0f, 5.2f, 4.7f), new Vector3(2.2f, 1.8f, 0.4f), wall, true), "plaster", 2.2f, wall);
        Tex(B("CourtInterior", Steps + new Vector3(0f, 2.6f, 6.4f), new Vector3(2.4f, 3.2f, 3f), new Color(0.2f, 0.16f, 0.12f), true), "wood", 1f, new Color(0.2f, 0.16f, 0.12f));
        foreach (float cx in new[] { -2.2f, 2.2f })
            Turned("CourtColumn", Steps + new Vector3(cx, 1.04f, 3.2f), new[] { new Vector2(0f, 0.3f), new Vector2(0.2f, 0.26f), new Vector2(3.6f, 0.22f), new Vector2(3.8f, 0.28f), new Vector2(3.95f, 0.3f) }, ProcTex.Mat(ProcTex.Get("plaster"), wall, new Vector2(1f, 3f), 0.8f, 0.5f), 16);
        var leaf = ProcTex.Mat(ProcTex.Get("wood"), new Color(0.3f, 0.18f, 0.1f), new Vector2(1f, 3f), 0.7f, 1f);
        stepsDoorL = new GameObject("CourtDoorL").transform; stepsDoorL.SetParent(root, false); stepsDoorL.position = Steps + new Vector3(-1.1f, 1.04f, 4.55f);
        stepsDoorR = new GameObject("CourtDoorR").transform; stepsDoorR.SetParent(root, false); stepsDoorR.position = Steps + new Vector3(1.1f, 1.04f, 4.55f);
        GameKit.Box(stepsDoorL, "Leaf", new Vector3(0.55f, 1.5f, 0f), new Vector3(1.1f, 3f, 0.07f), leaf);
        GameKit.Box(stepsDoorR, "Leaf", new Vector3(-0.55f, 1.5f, 0f), new Vector3(1.1f, 3f, 0.07f), leaf);
        foreach (var dt in new[] { stepsDoorL, stepsDoorR })
            GameKit.Box(dt, "Ring", new Vector3(dt == stepsDoorL ? 1.0f : -1.0f, 1.3f, -0.05f), new Vector3(0.05f, 0.12f, 0.03f), GameKit.Lit(new Color(0.7f, 0.55f, 0.25f), 0.8f, 0.9f));
        stepsDoorL.localRotation = Quaternion.Euler(0f, 95f, 0f); stepsDoorR.localRotation = Quaternion.Euler(0f, -95f, 0f);      // open, inwards
        if (signKit == null) { signKit = new GameObject("SignKit").AddComponent<MarketWorld>(); signKit.transform.SetParent(transform, false); }
        B("CourtSignBoard", Steps + new Vector3(0f, 4.85f, 4.48f), new Vector3(3.6f, 0.62f, 0.05f), new Color(0.08f, 0.2f, 0.15f), false, 0.3f);
        var sign = signKit.Paint(root, "MUNICIPAL COURT, CHANDNIPUR", "नगर न्यायालय, चांदनीपुर", Steps + new Vector3(0f, 4.85f, 4.452f), Quaternion.Euler(0f, 180f, 0f), 0.2f, 3.3f, new Color(0.95f, 0.9f, 0.75f));
        if (sign != null && sign.GetComponent<TextMesh>() != null) GameKit.Solid(sign.GetComponent<TextMesh>());
        // a lawyer's scooter at the kerb, a bench, a tea glass someone left on the steps
        var sc = signKit.SpawnScooter(Steps + new Vector3(3.6f, 0f, -1.7f), 200f, new Color(0.2f, 0.3f, 0.5f));
        if (sc != null) sc.rotation = Quaternion.Euler(0f, 200f, 6f);
        Tex(B("StepsBench", Steps + new Vector3(-3.2f, 1.45f, 3.9f), new Vector3(1.6f, 0.06f, 0.4f), Color.white, true), "wood", 0.9f, new Color(0.35f, 0.22f, 0.12f));
        Cy("LeftGlass", Steps + new Vector3(-1.4f, 0.93f, 2.15f), 0.03f, 0.09f, new Color(0.62f, 0.62f, 0.64f), 0.7f, 0.9f);
        FenceArea(Steps + new Vector3(0f, 0f, 0f), 15f, 11f);
        // across the road: the old city's frontage closes the view (without it the street ran out into sky, with the
        // roof scene's distant blocks floating on the horizon)
        Tex(B("StepsRoadFar", Steps + new Vector3(0f, -0.05f, -14f), new Vector3(40f, 0.1f, 8f), Color.white, false), "asphalt", 2.5f, new Color(0.4f, 0.39f, 0.38f));
        float[] widths = { 8.5f, 7f, 9.5f, 7.5f }, heights = { 9.5f, 12.5f, 8f, 11f };
        Color[] plaster = { new Color(0.74f, 0.64f, 0.5f), new Color(0.58f, 0.62f, 0.6f), new Color(0.78f, 0.7f, 0.62f), new Color(0.66f, 0.56f, 0.5f) };
        float x = -16f;
        for (int b = 0; b < widths.Length; b++)
        {
            float w = widths[b], h = heights[b], cx = x + w * 0.5f;
            Tex(B("FarBlock", Steps + new Vector3(cx, h * 0.5f, -19f), new Vector3(w - 0.1f, h, 6f), Color.white, false), "facade", 2.4f, plaster[b]);
            Tex(B("FarCoping", Steps + new Vector3(cx, h + 0.45f, -16.1f), new Vector3(w, 0.9f, 0.25f), Color.white, false), "facade", 2.4f, plaster[b] * 0.92f);
            Tex(B("FarShutter", Steps + new Vector3(cx, 1.4f, -15.97f), new Vector3(w - 1.4f, 2.8f, 0.05f), Color.white, false), "shutter", 1.2f, new Color(0.48f, 0.49f, 0.5f));
            for (int f = 1; f * 3.1f + 1.5f < h - 0.5f; f++)
                for (int k = 0; k < Mathf.FloorToInt(w / 3f); k++)
                {
                    float wx = x + 1.5f + k * 3f, wy = f * 3.1f + 1.6f;
                    B("FarWindow", Steps + new Vector3(wx, wy, -15.98f), new Vector3(1.1f, 1.3f, 0.04f), new Color(0.06f, 0.06f, 0.07f), false, 0.6f);
                    B("FarSunshade", Steps + new Vector3(wx, wy + 0.85f, -15.8f), new Vector3(1.4f, 0.07f, 0.45f), plaster[b] * 0.85f, false, 0.2f);
                }
            x += w;
        }
        for (int t = 0; t < 2; t++)
        {
            Cy("RoadTreeTrunk", Steps + new Vector3(-6f + t * 11f, 1.6f, -11.2f), 0.18f, 3.2f, new Color(0.3f, 0.24f, 0.18f), 0.2f);
            GameKit.Sphere(transform, "RoadTreeCrown", Steps + new Vector3(-6f + t * 11f, 4.2f, -11.2f), 3.4f, GameKit.Lit(new Color(0.2f, 0.32f, 0.16f), 0.2f));
        }
    }

    /// <summary>The courthouse door behind him: both leaves swing to, a heavy knock and the latch.</summary>
    IEnumerator DoorShutsBehind()
    {
        var src = new GameObject("CourtDoorSound").AddComponent<AudioSource>();
        src.transform.SetParent(transform, false); src.transform.position = Steps + new Vector3(0f, 2.2f, 4.5f);
        src.spatialBlend = 1f; src.minDistance = 1.5f; src.maxDistance = 25f;
        src.PlayOneShot(Creak(95f, 70f, 1.2f, 0.4f, 21), 0.6f);
        Quaternion l0 = stepsDoorL.localRotation, r0 = stepsDoorR.localRotation;
        for (float t = 0f; t < 1.3f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.3f); k = k * k;
            stepsDoorL.localRotation = Quaternion.Slerp(l0, Quaternion.identity, k);
            stepsDoorR.localRotation = Quaternion.Slerp(r0, Quaternion.identity, k);
            yield return null;
        }
        src.PlayOneShot(Tone(85f, 0.5f, 10f, 0.9f, true), 0.9f);
        src.PlayOneShot(Tone(1400f, 0.06f, 70f, 0.5f), 0.5f);
    }

    /// <summary>His phone, held low in front of him: Priya's message on its screen, the screen's light on his hands.</summary>
    IEnumerator ReadMessage()
    {
        var phone = new GameObject("ArjunPhone").transform; phone.SetParent(cam, false);
        phone.localPosition = new Vector3(0.03f, -0.36f, 0.3f); phone.localRotation = Quaternion.Euler(-62f, 180f, 0f);
        GameKit.Box(phone, "PhoneBody", Vector3.zero, new Vector3(0.074f, 0.009f, 0.155f), GameKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.7f, 0.3f));
        var screen = GameKit.Box(phone, "PhoneScreen", new Vector3(0f, -0.0048f, 0f), new Vector3(0.066f, 0.0008f, 0.141f), GameKit.Glow(new Color(0.08f, 0.09f, 0.1f), new Color(0.55f, 0.6f, 0.65f), 0.22f));
        screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var lightGo = new GameObject("ScreenLight"); lightGo.transform.SetParent(phone, false); lightGo.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        var sl = lightGo.AddComponent<Light>(); sl.type = LightType.Point; sl.range = 0.9f; sl.intensity = 0f; sl.color = new Color(0.8f, 0.88f, 1f);
        // a buzz in his pocket, then the phone comes up and the screen wakes
        sfx.PlayOneShot(Tone(150f, 0.25f, 4f, 0.6f, true), 0.45f);
        Vector3 down = phone.localPosition, up = new Vector3(0.03f, -0.11f, 0.32f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { phone.localPosition = Vector3.Lerp(down, up, Mathf.SmoothStep(0f, 1f, t / 0.6f)); sl.intensity = Mathf.Lerp(0f, 0.9f, t / 0.6f); yield return null; }
        phoneAt = Time.time; phoneReplied = false;              // her message on his screen (drawn in OnGUI)
        yield return W(4.6f);
        phoneReplied = true;                                    // his reply goes out under it
        sfx.PlayOneShot(Tone(1100f, 0.08f, 40f, 0.25f), 0.3f);
        yield return W(3.4f);
        phoneAt = -10f;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { phone.localPosition = Vector3.Lerp(up, down, Mathf.SmoothStep(0f, 1f, t / 0.6f)); sl.intensity = Mathf.Lerp(0.9f, 0f, t / 0.6f); yield return null; }
        Destroy(phone.gameObject);
    }

    IEnumerator SacrificeEnding()
    {
        EnsureEndingSets();
        Show(false, false);
        yield return ToCourtroom(new Vector3(2.15f, 0.1f, 4.0f), -6f);
        var e = cam.localPosition; e.y = Mathf.Max(1.1f, e.y - 0.47f); cam.localPosition = e;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c.name.StartsWith("Spectator") && (c.name.EndsWith("1") || c.name.EndsWith("3") || c.name.EndsWith("5") || c.name.EndsWith("7") || c.name.EndsWith("9"))) c.gameObject.SetActive(false);
        }
        fade = 0f; yield return WhiteIn();
        BeginBusy();
        Stamp("10:00 AM", "Municipal Court");
        yield return Narrate("end2_vo");
        yield return Turn(lawyer.EyePoint(), 0.6f);
        yield return Talk("end2_lawyer", lawyer);
        var k = Npc(C + new Vector3(2.8f, 0f, 1.0f), -20f, Cast.Kamala(), "KamalaCourt", false);
        k.lookTarget = HeadOf(judge);
        yield return Turn(k.EyePoint(), 0.6f);
        yield return Talk("end2_kamala", k);
        var v = Npc(C + new Vector3(-1.2f, 0f, 0.9f), 20f, Cast.Vikram(), "VikramCourt", false);
        v.lookTarget = HeadOf(judge);
        yield return Turn(v.EyePoint(), 0.6f);
        yield return Talk("end2_vikram", v);
        yield return Turn(judge.EyePoint(), 0.8f);
        yield return Talk("end2_judge", judge);
        EndTalk();
        yield return Wait(1.0f);
        sfx.PlayOneShot(gavelClip, 0.8f);
        yield return Wait(2f);
        // outside, alone: the door shuts behind him on the steps
        yield return FadeOut(1f);
        BuildCourtSteps();
        TexturePass();
        CinematicLook.NoSky(camComp, new Color(0.62f, 0.7f, 0.82f));
        CinematicLook.Ambient(new Color(0.75f, 0.78f, 0.85f), new Color(0.62f, 0.6f, 0.56f), new Color(0.25f, 0.22f, 0.2f));
        RenderSettings.fogColor = new Color(0.68f, 0.72f, 0.78f); RenderSettings.fogDensity = 0.004f;
        SetSun(new Color(1f, 0.95f, 0.85f), 1.35f, new Vector3(58f, 205f, 0f));
        ambience.clip = MakeNoiseBed(0.0f, 0.06f, 0.3f); ambience.volume = 0.5f; ambience.Play();
        var e2 = cam.localPosition; e2.y = standEye > 0f ? standEye : 1.62f; cam.localPosition = e2;
        Teleport(Steps + new Vector3(0.3f, 1.14f, 4.1f), 180f, 4f);
        yield return FadeIn(0.9f);
        Stamp("11:40 AM", "The court steps");
        StartCoroutine(DoorShutsBehind());
        yield return Glide(Steps + new Vector3(0.3f, 1.14f, 2.9f), 185f, 10f, 1.6f);
        // he sits on the top step
        yield return Glide(Steps + new Vector3(0.35f, 0.66f, 2.55f), 182f, 7f, 0.9f, (standEye > 0f ? standEye : 1.62f) - 0.5f);
        yield return Wait(0.8f);
        yield return ReadMessage();
        yield return Narrate("end2_vo_end");
        yield return ShowCard("EPILOGUE  -  SACRIFICE",
            "The eviction stayed frozen while the investigation ran: eight months. The families stayed in their homes.\n\n" +
            "Deshpande resigned before she could be charged. A civil case against her goes on.\n\n" +
            "Priya came home after three months in hiding, and carries not having walked into that courtroom herself.\n\n" +
            "The fight for Basti Nagar is not over. But it has begun.");
        yield return BackToGallery("CHANDNIPUR  -  SACRIFICE", new[] { "endS_01", "endS_02", "endS_03", "endS_04", "endS_05" }, true);
        RollCredits("SACRIFICE", true);
    }

    // ===================================================================== REVOLUTION
    CharacterNPC reporter, shopkeeper;
    Transform reporterPhone, shopShutter, shopTv;
    Light reporterPhoneLight;

    void BuildMontageSets()
    {
        if (montageBuilt) return;
        montageBuilt = true;
        // ---- a reporter's one-room flat, 5 AM: the bed, a stool with her phone on it, the window on the city
        Room("Flat", Flat, new Vector3(4f, 2.8f, 3.6f), new Color(0.6f, 0.62f, 0.6f), new Color(0.4f, 0.32f, 0.25f), "wood_floor", "plaster");
        Tex(B("FlatBed", Flat + new Vector3(0.2f, 0.22f, 1.0f), new Vector3(1.9f, 0.44f, 1.2f), Color.white, true), "fabric", 0.5f, new Color(0.55f, 0.58f, 0.68f));
        Tex(B("FlatPillow", Flat + new Vector3(0.95f, 0.5f, 1.2f), new Vector3(0.4f, 0.12f, 0.6f), Color.white, false), "fabric", 0.3f, new Color(0.8f, 0.78f, 0.72f));
        Tex(B("FlatStool", Flat + new Vector3(-0.95f, 0.25f, 0.75f), new Vector3(0.36f, 0.5f, 0.36f), Color.white, true), "wood", 0.6f, new Color(0.35f, 0.24f, 0.15f));
        Glow("FlatWindow", Flat + new Vector3(0.1f, 1.5f, 1.79f), new Vector3(1.4f, 1.0f, 0.02f), new Color(0.12f, 0.16f, 0.32f), 0.9f);
        for (int i = 0; i < 5; i++) Glow("CityWindowLit", Flat + new Vector3(-0.45f + i * 0.24f, 1.25f + (i % 2) * 0.22f, 1.78f), new Vector3(0.05f, 0.04f, 0.01f), new Color(1f, 0.75f, 0.45f), 1.4f);
        B("FlatWindowBar", Flat + new Vector3(0.1f, 1.5f, 1.775f), new Vector3(0.03f, 1.0f, 0.02f), new Color(0.2f, 0.2f, 0.2f));
        Prop("metal_jug", Flat + new Vector3(-1.5f, 0f, -1.4f), 30f);
        reporter = Npc(Flat + new Vector3(0.2f, 0f, 0.55f), 180f, NpcStyle.CrowdWoman(1501), "Reporter", false);
        reporter.Sit(Flat + new Vector3(0.2f, 0.45f, 0.78f), 180f, 0.42f, true);
        reporter.lookTarget = null;
        reporterPhone = PhoneThing(transform);
        var rps = reporterPhone.Find("PhoneScreen");
        if (rps != null) rps.GetComponent<Renderer>().sharedMaterial = GameKit.Glow(new Color(0.08f, 0.09f, 0.1f), new Color(0.55f, 0.62f, 0.7f), 0.25f);     // a screen, not a lamp
        reporterPhone.position = Flat + new Vector3(-0.92f, 0.505f, 0.72f); reporterPhone.rotation = Quaternion.Euler(0f, 30f, 0f);
        var pl = new GameObject("ReporterPhoneLight"); pl.transform.SetParent(reporterPhone, false); pl.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        reporterPhoneLight = pl.AddComponent<Light>(); reporterPhoneLight.type = LightType.Point; reporterPhoneLight.range = 1.4f; reporterPhoneLight.intensity = 0f; reporterPhoneLight.color = new Color(0.75f, 0.85f, 1f);
        PL(Flat + new Vector3(0.6f, 2.2f, 1.2f), new Color(0.35f, 0.42f, 0.7f), 0.5f, 5f);       // the window's blue
        // ---- a shop on Bazaar Road at first light: a rolling shutter, a counter, a television on a shelf
        Tex(B("ShopRoad", Shop + new Vector3(0f, -0.05f, -3f), new Vector3(12f, 0.1f, 8f), Color.white, true), "asphalt", 2f, new Color(0.4f, 0.39f, 0.38f));
        var pl2 = new Color(0.72f, 0.64f, 0.5f);
        // the front: piers either side of the shop's opening and a header over it (the shutter rolls up into the opening)
        Tex(B("ShopFrontL", Shop + new Vector3(-2.8f, 2.6f, 0.6f), new Vector3(2.4f, 5.2f, 0.3f), pl2, true), "facade", 2.2f, pl2);
        Tex(B("ShopFrontR", Shop + new Vector3(2.8f, 2.6f, 0.6f), new Vector3(2.4f, 5.2f, 0.3f), pl2, true), "facade", 2.2f, pl2);
        Tex(B("ShopFrontHead", Shop + new Vector3(0f, 4.05f, 0.6f), new Vector3(3.2f, 2.3f, 0.3f), pl2, true), "facade", 2.2f, pl2);
        PL(Shop + new Vector3(0f, 2.4f, 1.6f), new Color(0.95f, 0.98f, 1f), 1.2f, 4f);           // the tube light inside, already on
        Tex(B("ShopInside", Shop + new Vector3(0f, 1.4f, 2.1f), new Vector3(3.2f, 2.8f, 3f), new Color(0.35f, 0.3f, 0.25f), true), "plaster", 1.5f, new Color(0.45f, 0.38f, 0.3f));
        Tex(B("ShopCounter", Shop + new Vector3(0f, 0.45f, 1.3f), new Vector3(2.4f, 0.9f, 0.5f), Color.white, true), "wood", 0.8f, new Color(0.38f, 0.26f, 0.16f));
        Tex(B("ShopShelf", Shop + new Vector3(0.7f, 1.7f, 2.9f), new Vector3(1.4f, 0.04f, 0.35f), Color.white, false), "wood", 0.8f, new Color(0.38f, 0.26f, 0.16f));
        for (int i = 0; i < 8; i++) B("ShopPacket", Shop + new Vector3(-1.2f + i * 0.3f, 1.05f + (i % 3) * 0.02f, 1.35f), new Vector3(0.18f, 0.22f, 0.08f), Color.HSVToRGB(i * 0.13f % 1f, 0.6f, 0.7f), false, 0.3f);
        shopTv = GameKit.Box(transform, "ShopTV", Shop + new Vector3(0.7f, 1.92f, 2.85f), new Vector3(0.5f, 0.36f, 0.3f), GameKit.Lit(new Color(0.12f, 0.12f, 0.13f), 0.4f)).transform;
        var tvScreen = Glow("ShopTVScreen", Shop + new Vector3(0.7f, 1.92f, 2.695f), new Vector3(0.42f, 0.28f, 0.01f), new Color(0.6f, 0.15f, 0.12f), 1.8f);
        GameKit.Solid(GameKit.Text(transform, "BREAKING", Shop + new Vector3(0.7f, 1.99f, 2.688f), Quaternion.Euler(0f, 180f, 0f), 0.05f, Color.white));
        GameKit.Solid(GameKit.Text(transform, "BASTI NAGAR: LAND ORDER FORGED?", Shop + new Vector3(0.7f, 1.88f, 2.688f), Quaternion.Euler(0f, 180f, 0f), 0.022f, new Color(1f, 0.95f, 0.8f)));
        PL(Shop + new Vector3(0.7f, 1.9f, 2.4f), new Color(0.9f, 0.55f, 0.5f), 0.8f, 3f);
        // the shutter, down, ready to go up: a sheet of ribs in front of the opening
        var sh = new GameObject("ShopShutter").transform; sh.SetParent(transform, false); sh.position = Shop + new Vector3(0f, 0f, 0.42f);
        var shM = GameKit.Lit(Color.white); var shutterGo = GameKit.Box(sh, "ShutterSheet", new Vector3(0f, 1.4f, 0f), new Vector3(3.1f, 2.8f, 0.04f), shM);
        GameKit.Surface(shutterGo, "shutter", 1.2f, 1f, 1f, new Color(0.5f, 0.52f, 0.54f));
        GameKit.Box(sh, "ShutterBar", new Vector3(0f, 0.08f, -0.03f), new Vector3(3.1f, 0.06f, 0.03f), GameKit.Lit(new Color(0.3f, 0.3f, 0.32f), 0.5f, 0.7f));
        shopShutter = sh;
        Tex(B("ShutterBox", Shop + new Vector3(0f, 3.0f, 0.46f), new Vector3(3.3f, 0.36f, 0.3f), Color.white, false), "metal", 1f, new Color(0.35f, 0.36f, 0.38f));
        if (signKit == null) { signKit = new GameObject("SignKit").AddComponent<MarketWorld>(); signKit.transform.SetParent(transform, false); }
        B("ShopBoard", Shop + new Vector3(0f, 3.55f, 0.43f), new Vector3(3.4f, 0.55f, 0.05f), new Color(0.5f, 0.1f, 0.08f), false, 0.3f);
        var sign = signKit.Paint(transform, "SHARMA GENERAL STORES", "शर्मा जनरल स्टोर्स", Shop + new Vector3(0f, 3.55f, 0.402f), Quaternion.Euler(0f, 180f, 0f), 0.18f, 3.1f, new Color(1f, 0.92f, 0.7f));
        if (sign != null && sign.GetComponent<TextMesh>() != null) GameKit.Solid(sign.GetComponent<TextMesh>());
        Cy("ShopLampPost", Shop + new Vector3(-3.2f, 2.6f, -1.2f), 0.07f, 5.2f, new Color(0.2f, 0.2f, 0.2f), 0.4f, 0.6f);
        PL(Shop + new Vector3(-2.8f, 4.8f, -1.4f), new Color(1f, 0.62f, 0.3f), 3f, 10f);      // the sodium lamp still on
        shopkeeper = Npc(Shop + new Vector3(-0.2f, 0f, -0.25f), 0f, NpcStyle.Crowd(1517), "Shopkeeper", false);
        shopkeeper.lookTarget = null;
        TexturePass();
    }

    void Prelap(string clipName, AudioClip clip, Vector3 at, float vol)
    {
        var src = new GameObject("Prelap_" + clipName).AddComponent<AudioSource>();
        src.transform.SetParent(transform, false); src.transform.position = at;
        src.clip = clip; src.spatialBlend = 0f; src.volume = vol; src.Play();
        Destroy(src.gameObject, clip.length + 0.5f);
    }

    IEnumerator RevolutionEnding()
    {
        // 4 AM in the shelter: everything goes out
        ambience.clip = shelterBed; ambience.Play();
        Show(true, false);                        // back in the shelter (the Gallery off)
        Teleport(U + new Vector3(0.8f, 0.1f, -1.2f), -30f);
        for (int i = 0; i < 2; i++) Glow("Laptop", U + new Vector3(-0.3f + i * 0.6f, 0.2f, 0.6f), new Vector3(0.35f, 0.22f, 0.02f), new Color(0.6f, 0.75f, 1f), 2f);
        fade = 0f; yield return WhiteIn();
        BeginBusy();
        yield return Turn(priya.EyePoint(), 0.6f);
        yield return Talk("end3_priya_1", priya);
        yield return Talk("end3_arjun_1");
        EndTalk();
        // sent: one soft whoosh, not a beep
        sfx.PlayOneShot(EvidenceFolio.Slide(), 0.5f);
        yield return FadeOut(1.5f);
        BuildMontageSets();
        EnsureEndingSets();
        var crowd = Crowd(9f, true);
        // ---- 5:10 AM: a reporter's phone wakes on the stool beside her bed
        CinematicLook.Ambient(new Color(0.22f, 0.25f, 0.4f), new Color(0.14f, 0.14f, 0.2f), new Color(0.05f, 0.05f, 0.06f));
        RenderSettings.fogDensity = 0.002f;
        Teleport(Flat + new Vector3(-0.15f, 0.1f, -1.25f), 8f, 12f);
        var e = cam.localPosition; e.y = Mathf.Max(1.1f, e.y - 0.35f); cam.localPosition = e;
        ambience.clip = MakeNoiseBed(0.01f, 0.03f, 0.2f); ambience.volume = 0.35f; ambience.Play();
        fade = 1f; fadeTarget = 0f; fadeSpeed = 1.6f;
        Stamp("5:10 AM", "A reporter's flat, Civil Lines");
        yield return Wait(0.8f);
        sfx.PlayOneShot(Tone(150f, 0.25f, 4f, 0.6f, true), 0.4f);              // the buzz on the wooden stool
        reporterPhoneLight.intensity = 0.7f;
        reporter.lookTarget = reporterPhone;
        yield return Wait(0.7f);
        var hand = new GameObject("ReporterHand").transform; hand.SetParent(transform, false); hand.position = reporter.HandPosition(1);
        reporter.Hold(1, hand, 0.35f);
        yield return MoveTo(hand, hand.position, reporterPhone.position + Vector3.up * 0.04f, 0.7f);
        reporterPhone.SetParent(hand, true);
        Vector3 read = reporter.EyePoint() + reporter.transform.forward * 0.32f - Vector3.up * 0.22f;
        yield return MoveTo(hand, hand.position, read, 0.8f, 0.05f);
        reporterPhone.rotation = Quaternion.LookRotation(reporter.transform.forward, Vector3.up) * Quaternion.Euler(-60f, 180f, 0f);
        reporterPhoneLight.intensity = 1.1f;                                       // the screen's light on her face
        yield return Wait(1.4f);
        reporter.Say(VO("a5r_reporter"));
        sub = DialogueDB.Speaker("a5r_reporter") + ":  " + DialogueDB.Text("a5r_reporter"); subUntil = Time.time + 3.2f;
        yield return Wait(2.2f);
        // the shop's shutter is heard before we see the shop
        var news = VO("a5r_news");
        Prelap("shutter", Rattle(), Shop, 0.5f);
        yield return Wait(0.8f);
        // ---- 5:55 AM: a shutter goes up on Bazaar Road, and the news is already on
        CinematicLook.Ambient(new Color(0.38f, 0.42f, 0.6f), new Color(0.3f, 0.28f, 0.32f), new Color(0.1f, 0.09f, 0.1f));
        Teleport(Shop + new Vector3(1.4f, 0.1f, -4.2f), -10f, 4f);
        var e2 = cam.localPosition; e2.y = standEye > 0f ? standEye : 1.62f; cam.localPosition = e2;
        Stamp("5:55 AM", "Bazaar Road");
        var tvSrc = new GameObject("ShopTVSound").AddComponent<AudioSource>(); tvSrc.transform.SetParent(transform, false); tvSrc.transform.position = shopTv.position;
        tvSrc.spatialBlend = 0.8f; tvSrc.minDistance = 1f; tvSrc.maxDistance = 14f; tvSrc.volume = 0.8f;
        var hl = new GameObject("ShopHandL").transform; hl.SetParent(transform, false);
        var hr = new GameObject("ShopHandR").transform; hr.SetParent(transform, false);
        Vector3 bar = shopShutter.position + new Vector3(0f, 0.12f, -0.05f);
        hl.position = bar - Vector3.right * 0.25f; hr.position = bar + Vector3.right * 0.25f;
        shopkeeper.lean = 25f;
        shopkeeper.Hold(0, hl, 0.3f); shopkeeper.Hold(1, hr, 0.3f);
        yield return Wait(0.4f);
        var rat = new GameObject("ShutterSound").AddComponent<AudioSource>(); rat.transform.SetParent(transform, false); rat.transform.position = shopShutter.position + Vector3.up;
        rat.spatialBlend = 0.9f; rat.clip = Creak(60f, 90f, 1.6f, 0.85f, 31); rat.volume = 0.8f; rat.Play();
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
            shopShutter.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.08f, k), 1f);
            shopShutter.position = Shop + new Vector3(0f, Mathf.Lerp(0f, 2.62f, k), 0.42f);
            float hy = Mathf.Min(1.85f, 0.12f + 2.6f * k);
            hl.position = new Vector3(hl.position.x, hy, hl.position.z); hr.position = new Vector3(hr.position.x, hy, hr.position.z);
            shopkeeper.lean = Mathf.Lerp(25f, 0f, k * 1.5f);
            if (t > 0.5f && news != null && !tvSrc.isPlaying) { tvSrc.clip = news; tvSrc.Play(); sub = DialogueDB.Speaker("a5r_news") + ":  " + DialogueDB.Text("a5r_news"); subUntil = Time.time + news.length + 0.3f; }
            yield return null;
        }
        shopkeeper.Release(0, 0.4f); shopkeeper.Release(1, 0.4f);
        // he stops with his hand still up, and turns to the screen
        shopkeeper.lookTarget = shopTv;
        yield return Wait(news != null ? Mathf.Max(2.6f, news.length - 1.4f) : 3f);
        // people outside the shelter, heard before the shelter is seen
        Prelap("crowd", crowd, U, 0.18f);
        yield return Wait(1.1f);
        // ---- 6:20 AM: upstairs in the shelter, Priya hears the street
        tvSrc.Stop();
        CinematicLook.Ambient(new Color(0.5f, 0.48f, 0.58f), new Color(0.42f, 0.36f, 0.36f), new Color(0.14f, 0.12f, 0.12f));
        var rw = Deep(transform, "RiverWindow");
        if (rw != null) { var m = rw.GetComponent<Renderer>().material; m.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.55f) * 1.6f); }
        priyaHoldsFile = false;
        priya.Release(0, 0.3f); priya.Release(1, 0.3f);
        priya.transform.position = U + new Vector3(-0.2f, 0f, 1.0f);
        priya.freezeFacing = true; priya.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
        priya.lookTarget = cam;
        Teleport(U + new Vector3(-1.9f, 0.1f, -1.5f), 40f, 2f);
        Stamp("6:20 AM", "The shelter");
        var outside = new GameObject("CrowdOutside").AddComponent<AudioSource>(); outside.transform.SetParent(transform, false); outside.transform.position = U + new Vector3(1.5f, 1.2f, 4f);
        outside.clip = crowd; outside.loop = true; outside.spatialBlend = 1f; outside.minDistance = 2f; outside.maxDistance = 20f; outside.volume = 0.25f; outside.Play();
        var win = new GameObject("PriyaLooksOut").transform; win.SetParent(transform, false); win.position = U + new Vector3(1.5f, 1.7f, 2.9f);
        yield return Wait(0.8f);
        priya.lookTarget = win;                          // she hears them before she sees them
        yield return Wait(1.0f);
        priya.freezeFacing = false;
        yield return priya.WalkTo(U + new Vector3(1.2f, 0f, 1.85f), 0.9f, 4f);
        priya.freezeFacing = true;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime) { outside.volume = Mathf.Lerp(0.25f, 0.6f, t / 2.2f); yield return null; }
        yield return FadeOut(1.4f);
        outside.Stop();
        // ---- 6:25 AM: the roof at sunrise; she steps out into it
        yield return ToTerrace(new Color(1f, 0.6f, 0.35f), new Color(0.45f, 0.45f, 0.7f), new Color(1f, 0.8f, 0.5f), 7f, 0f);
        var p = Npc(T + new Vector3(-3.8f, 0f, -2.2f), 40f, Cast.Priya(), "PriyaRoof", false);
        var below = new GameObject("CrowdBelow").AddComponent<AudioSource>(); below.transform.SetParent(transform, false); below.transform.position = T + new Vector3(0f, -6f, 12f);
        below.clip = crowd; below.loop = true; below.spatialBlend = 1f; below.minDistance = 4f; below.maxDistance = 40f; below.volume = 0.5f; below.Play();
        Stamp("6:25 AM", "Sunrise");
        yield return FadeIn(0.6f);
        yield return Wait(0.4f);
        yield return p.WalkTo(T + new Vector3(0.6f, 0f, 3.6f), 1.0f, 8f);
        p.lookTarget = cam;
        yield return Turn(p.EyePoint(), 0.8f);
        yield return Talk("end3_priya_2", p);
        yield return Talk("end3_arjun_2");
        EndTalk();
        yield return ShowCard("EPILOGUE  -  REVOLUTION",
            "By seven, #SaveBastiNagar was trending across the country. By ten, the largest protest Chandnipur had seen in decades filled the road outside the Municipal Office.\n\n" +
            "Three state ministers were investigated and an independent inquiry was ordered. Deshpande was never charged; she left the country six months later.\n\n" +
            "The families won a temporary stay from the Supreme Court. The Renewal Project is in limbo.\n\n" +
            "Priya became a national symbol of resistance, and needed police protection for two years. The truth is messy. Silence is worse.");
        below.Stop();
        yield return BackToGallery("CHANDNIPUR  -  REVOLUTION", new[] { "endR_01", "endR_02", "endR_03", "endR_04", "endR_05" }, false);
        RollCredits("REVOLUTION", false);
    }

    static AudioClip rattleShutter;
    static AudioClip Rattle()
    {
        if (rattleShutter != null) return rattleShutter;
        rattleShutter = Creak(55f, 85f, 1.2f, 0.9f, 41);
        return rattleShutter;
    }

    float phoneAt = -10f; bool phoneReplied;

    /// <summary>Priya's message as his phone shows it: her name, her words in a bubble, then his reply under it.</summary>
    void DrawPhone(float W, float H)
    {
        if (phoneAt < 0f) return;
        float a = Mathf.Clamp01((Time.time - phoneAt) / 0.35f);
        float pw = 300f, ph = 430f, px = W * 0.5f - pw * 0.5f, py = H * 0.5f - ph * 0.42f;
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
        UiKit.Fill(new Rect(px - 10f, py - 10f, pw + 20f, ph + 20f), new Color(0.03f, 0.03f, 0.035f, 0.96f));
        UiKit.Fill(new Rect(px, py, pw, ph), new Color(0.11f, 0.12f, 0.13f, 1f));
        var head = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        head.normal.textColor = new Color(0.92f, 0.92f, 0.9f, a);
        var body = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 17, wordWrap = true };
        body.normal.textColor = new Color(0.95f, 0.95f, 0.93f, a);
        UiKit.Fill(new Rect(px, py, pw, 46f), new Color(0.16f, 0.17f, 0.19f, 1f));
        UiKit.Text(new Rect(px, py + 4f, pw, 38f), "Priya", head);
        string hers = "I saw it on the news. Thank you. I'm sorry I couldn't be there. I'm sorry I made you carry this alone.";
        float hh = body.CalcHeight(new GUIContent(hers), pw - 90f);
        UiKit.Fill(new Rect(px + 14f, py + 64f, pw - 70f, hh + 18f), new Color(0.24f, 0.26f, 0.29f, 1f));
        UiKit.Text(new Rect(px + 24f, py + 72f, pw - 90f, hh + 4f), hers, body);
        if (phoneReplied)
        {
            string his = "You don't have to be sorry. You started this. I just finished a chapter.";
            float h2 = body.CalcHeight(new GUIContent(his), pw - 90f);
            UiKit.Fill(new Rect(px + 56f, py + 64f + hh + 32f, pw - 70f, h2 + 18f), new Color(0.13f, 0.36f, 0.26f, 1f));
            UiKit.Text(new Rect(px + 66f, py + 72f + hh + 32f, pw - 90f, h2 + 4f), his, body);
        }
        GUI.color = old;
    }

    // the montage clock: a time and a place in the lower left, fading in and out
    void DrawStamp(float W, float H)
    {
        float t = Time.time - stampAt;
        if (string.IsNullOrEmpty(stampA) || t > 4.2f) return;
        float a = Mathf.Clamp01(Mathf.Min(t / 0.5f, (4.2f - t) / 0.8f));
        var big = new GUIStyle(GUI.skin.label) { font = UiKit.Title, fontSize = 34 };
        var small = new GUIStyle(GUI.skin.label) { font = UiKit.Body, fontSize = 15, fontStyle = FontStyle.Bold };
        big.normal.textColor = new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, a);
        small.normal.textColor = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a * 0.9f);
        UiKit.Text(new Rect(56f, H * 0.915f - 98f, 600f, 44f), stampA, big);
        UiKit.Text(new Rect(58f, H * 0.915f - 52f, 600f, 22f), UiKit.Track(stampB), small);
    }
}

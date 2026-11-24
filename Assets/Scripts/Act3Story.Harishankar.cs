using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Harishankar: a man from the lanes round the temple who keeps its accounts in the evenings and has known Priya since
/// her Nani brought her to light lamps here. He sits at the temple office's table inside the gate, his cloth bag on the
/// back of his chair, writing up the day's offerings in a ledger. He closes the ledger to hear Arjun's answer, studies
/// him, and takes Priya's sealed envelope out of the table drawer. (Internally he is still "pandit": the save's
/// panditAnswer and the a3_pandit_* lines keep their names so existing saves and recordings carry over.)
/// </summary>
public partial class Act3Story
{
    Transform ledgerCover, deskDrawer, harishankarHand, harishankarPen;
    // read by the play-test route
    bool ledgerClosed, drawerOpened, envelopeOffered;

    void BuildTempleOffice(Vector3 at)
    {
        var root = new GameObject("TempleOffice").transform; root.SetParent(transform, false);
        root.position = at; root.rotation = Quaternion.identity;          // the table faces the courtyard (+z)
        var teak = ProcTex.Mat(ProcTex.Get("wood"), new Color(0.36f, 0.22f, 0.12f), new Vector2(1.2f, 0.7f), 0.75f, 1f);
        var teakDark = ProcTex.Mat(ProcTex.Get("wood"), new Color(0.24f, 0.14f, 0.08f), new Vector2(0.8f, 0.6f), 0.7f, 1f);
        // the table: a top, a front apron with the drawer in it, four turned legs
        var top = GameKit.Box(root, "OfficeTableTop", new Vector3(0f, 0.74f, 0f), new Vector3(1.1f, 0.035f, 0.62f), teak);
        top.AddComponent<BoxCollider>();
        GameKit.Box(root, "OfficeTableApron", new Vector3(0f, 0.66f, 0.29f), new Vector3(1.02f, 0.12f, 0.02f), teakDark);
        foreach (float lx in new[] { -0.5f, 0.5f }) foreach (float lz in new[] { -0.27f, 0.27f })
            Turned("OfficeTableLeg", at + new Vector3(lx, 0f, lz), new[] { new Vector2(0f, 0.022f), new Vector2(0.08f, 0.028f), new Vector2(0.62f, 0.02f), new Vector2(0.66f, 0.026f), new Vector2(0.72f, 0.024f) }, teakDark, 10);
        // the drawer, on Harishankar's side (-z), in its own frame, a brass knob
        var dr = new GameObject("OfficeDrawer").transform; dr.SetParent(root, false); dr.localPosition = new Vector3(0.18f, 0.655f, -0.02f);
        GameKit.Box(dr, "DrawerFront", new Vector3(0f, 0f, -0.29f), new Vector3(0.42f, 0.1f, 0.018f), teak);
        GameKit.Box(dr, "DrawerBox", new Vector3(0f, -0.005f, -0.06f), new Vector3(0.4f, 0.08f, 0.44f), teakDark);
        GameKit.Sphere(dr, "DrawerKnob", new Vector3(0f, 0f, -0.305f), 0.025f, GameKit.Lit(new Color(0.8f, 0.6f, 0.25f), 0.8f, 1f));
        deskDrawer = dr;
        // the ledger: a cloth-bound account book open on the table, the day's offerings entered in columns
        var ledger = new GameObject("TempleLedger").transform; ledger.SetParent(root, false); ledger.localPosition = new Vector3(-0.12f, 0.76f, -0.04f); ledger.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var cloth = GameKit.Lit(new Color(0.42f, 0.08f, 0.07f), 0.25f);
        var pages = ProcTex.Mat(ProcTex.Get("paper"), new Color(0.88f, 0.84f, 0.72f), new Vector2(0.5f, 0.5f), 0.12f, 0.25f);
        GameKit.Box(ledger, "LedgerBack", new Vector3(0.14f, 0.004f, 0f), new Vector3(0.28f, 0.008f, 0.36f), cloth);
        GameKit.Box(ledger, "LedgerPagesR", new Vector3(0.14f, 0.012f, 0f), new Vector3(0.265f, 0.01f, 0.345f), pages);
        GameKit.Box(ledger, "LedgerPagesL", new Vector3(-0.14f, 0.012f, 0f), new Vector3(0.265f, 0.01f, 0.345f), pages);
        var ink = new Color(0.12f, 0.12f, 0.32f);
        string[] lines = { "22 Nov   Diya oil      40", "Flowers (Mohan)     120", "Hundi              1,315", "Electricity (part)  -400" };
        for (int i = 0; i < lines.Length; i++)
        {
            var tm = GameKit.Solid(GameKit.Text(ledger, lines[i], new Vector3(0.025f, 0.0175f, 0.11f - i * 0.026f), Quaternion.Euler(90f, 0f, 0f), 0.0085f, ink));
            tm.anchor = TextAnchor.MiddleLeft;
        }
        var lcov = new GameObject("LedgerCoverHinge").transform; lcov.SetParent(ledger, false); lcov.localPosition = new Vector3(0f, 0.008f, 0f); lcov.localRotation = Quaternion.Euler(0f, 0f, 0f);
        GameKit.Box(lcov, "LedgerCoverL", new Vector3(-0.14f, -0.004f, 0f), new Vector3(0.28f, 0.008f, 0.36f), cloth);
        ledgerCover = lcov;
        // his pen, a steel tumbler of water, the lamp he writes by, and his cloth bag on the chair's back
        harishankarPen = GameKit.Cyl(transform, "HarishankarPen", at + new Vector3(-0.02f, 0.79f, -0.14f), 0.005f, 0.14f, GameKit.Lit(new Color(0.1f, 0.1f, 0.12f), 0.6f)).transform;
        harishankarPen.rotation = Quaternion.Euler(90f, 70f, 0f);
        Cy("OfficeTumbler", at + new Vector3(0.42f, 0.81f, 0.12f), 0.032f, 0.09f, new Color(0.62f, 0.62f, 0.64f), 0.7f, 0.9f);
        Diya(at + new Vector3(-0.44f, 0.76f, 0.16f), true);
        var chair = PropKit.Place(transform, "painted_wooden_chair_01", at + new Vector3(0f, 0f, -0.62f), 0f);
        var bag = GameKit.Box(transform, "ClothBag", at + new Vector3(0.0f, 0.62f, -0.86f), new Vector3(0.34f, 0.36f, 0.08f), ProcTex.Mat(ProcTex.Get("fabric"), new Color(0.68f, 0.62f, 0.46f), new Vector2(0.4f, 0.4f), 0.15f, 0.6f));
        bag.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        GameKit.Box(transform, "ClothBagStrap", at + new Vector3(0.0f, 0.86f, -0.86f), new Vector3(0.3f, 0.12f, 0.012f), GameKit.Lit(new Color(0.6f, 0.55f, 0.4f), 0.15f));
        // a calendar from a sweet shop and the temple's festival list on the alcove wall behind him
        B("FestivalList", at + new Vector3(-0.55f, 1.55f, -1.08f), new Vector3(0.4f, 0.55f, 0.01f), new Color(0.92f, 0.86f, 0.62f), false, 0.1f);
        GameKit.Solid(GameKit.Text(transform, "NAVRATRI SEVA", at + new Vector3(-0.55f, 1.74f, -1.07f), Quaternion.Euler(0f, 180f, 0f), 0.035f, new Color(0.6f, 0.1f, 0.06f)));
        PL(at + new Vector3(0.3f, 1.4f, 0.55f), new Color(1f, 0.72f, 0.45f), 0.9f, 3.2f);       // the face, softly, from the courtyard side
    }

    /// <summary>He is writing up the day's offerings when Arjun arrives: the pen moving along the line, his eyes on the page.</summary>
    IEnumerator HarishankarWrites()
    {
        harishankarHand = new GameObject("HarishankarHandR").transform; harishankarHand.SetParent(transform, false);
        var handL = new GameObject("HarishankarHandL").transform; handL.SetParent(transform, false);
        handL.position = ledgerCover.parent.position + new Vector3(0.12f, 0.03f, 0.06f);
        pandit.Hold(0, handL, 0.01f);
        pandit.Hold(1, harishankarHand, 0.01f);
        float t0 = Time.time;
        while (this != null && !panditTalking)
        {
            float t = Time.time - t0;
            harishankarHand.position = ledgerCover.parent.position + new Vector3(-0.08f + Mathf.Repeat(t * 0.035f, 0.12f), 0.035f + 0.004f * Mathf.Sin(t * 22f), -0.02f);
            if (harishankarPen != null) { harishankarPen.position = harishankarHand.position + new Vector3(0.0f, 0.02f, 0.03f); harishankarPen.rotation = Quaternion.Euler(60f, 160f, 0f); }
            yield return null;
        }
    }
    bool panditTalking;

    IEnumerator MoveHand(Transform h, Vector3 to, float dur)
    {
        Vector3 from = h.position;
        for (float t = 0f; t < dur; t += Time.deltaTime) { h.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur)); yield return null; }
        h.position = to;
    }

    /// <summary>He closes the ledger with both hands and puts the pen down on it.</summary>
    IEnumerator CloseLedger()
    {
        Quaternion c0 = ledgerCover.localRotation, c1 = Quaternion.Euler(0f, 0f, 180f);
        StartCoroutine(MoveHand(harishankarHand, ledgerCover.parent.position + new Vector3(0.1f, 0.06f, 0f), 0.35f));
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
            ledgerCover.localRotation = Quaternion.Slerp(c0, c1, k);
            yield return null;
        }
        ledgerCover.localRotation = c1;
        sfx.PlayOneShot(Tone(180f, 0.12f, 30f, 0.4f, true), 0.25f);
        if (harishankarPen != null) { harishankarPen.position = ledgerCover.parent.position + new Vector3(0.06f, 0.03f, 0f); harishankarPen.rotation = Quaternion.Euler(90f, 20f, 0f); }
        ledgerClosed = true;
        yield return MoveHand(harishankarHand, ledgerCover.parent.position + new Vector3(0.08f, 0.04f, 0.1f), 0.3f);
    }

    /// <summary>The drawer comes out, the envelope comes up out of it in his hand, and he holds it out across the table.</summary>
    IEnumerator EnvelopeFromDrawer()
    {
        Vector3 d0 = deskDrawer.localPosition, d1 = d0 + new Vector3(0f, 0f, -0.2f);
        yield return MoveHand(harishankarHand, deskDrawer.position + new Vector3(0f, 0.02f, -0.3f), 0.4f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            deskDrawer.localPosition = Vector3.Lerp(d0, d1, Mathf.SmoothStep(0f, 1f, t / 0.35f));
            harishankarHand.position = deskDrawer.position + new Vector3(0f, 0.02f, -0.3f);
            yield return null;
        }
        sfx.PlayOneShot(Tone(120f, 0.18f, 18f, 0.35f, true), 0.3f);
        drawerOpened = true;
        // the envelope was in the drawer all along
        envelope.gameObject.SetActive(true);
        envelope.position = deskDrawer.position + new Vector3(0f, 0.01f, -0.1f);
        envelope.rotation = Quaternion.Euler(0f, 180f, 0f);
        yield return MoveHand(harishankarHand, envelope.position + new Vector3(0f, 0.03f, 0f), 0.3f);
        envelope.SetParent(harishankarHand, true);
        Vector3 offer = pandit.transform.position + Vector3.up * 1.0f + pandit.transform.forward * 0.55f;
        yield return MoveHand(harishankarHand, offer, 0.6f);
        envelopeOffered = true;
    }
}

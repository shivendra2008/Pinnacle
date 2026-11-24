using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The library's close-up things. The two bookcases Arjun passes nearest (the first row, either side of the aisle) get
/// stitched municipal ledgers on their faces, books pushed in at different depths and gaps that show the back panel; the
/// cases further off keep their grouped meshes. In the Record Room the block and shelf numbers are enamel plates in
/// depth-tested lettering, the 1942 folder's place on Block C / Shelf 14 is an empty box-file with the sign-out sheet
/// clipped beside it (Reddy's signature, the same one she will see again in Act IV), and the librarian's drawer slides out
/// on the brass key when the code is right.
/// </summary>
public partial class Act3Story
{
    Transform signOutSheet, drawerFront, brassKey;
    static Material[] ledgerMats;

    /// <summary>Tall, thick, cloth-bound municipal ledgers, stitched at the spine, with paper labels; standing in a run.</summary>
    void LedgerRun(Transform bc, float shelfY, float x0, float x1, float faceZ, float face, System.Random rnd)
    {
        if (ledgerMats == null)
        {
            Color[] c = { new Color(0.34f, 0.07f, 0.06f), new Color(0.12f, 0.16f, 0.12f), new Color(0.2f, 0.12f, 0.08f), new Color(0.1f, 0.12f, 0.2f) };
            ledgerMats = new Material[c.Length];
            for (int i = 0; i < c.Length; i++) ledgerMats[i] = ProcTex.Mat(ProcTex.Get("fabric"), c[i], new Vector2(0.3f, 0.6f), 0.2f, 0.7f);
        }
        var label = GameKit.Lit(new Color(0.86f, 0.82f, 0.68f), 0.15f);
        var thread = GameKit.Lit(new Color(0.75f, 0.7f, 0.55f), 0.2f);
        int year = 1951 + rnd.Next(6);
        float x = x0;
        while (x < x1 - 0.07f)
        {
            float thick = 0.06f + (float)rnd.NextDouble() * 0.03f, h = 0.33f + (float)rnd.NextDouble() * 0.04f, dp = 0.25f;
            if (x + thick > x1) break;
            float push = rnd.NextDouble() < 0.3 ? (float)rnd.NextDouble() * 0.035f : 0f;
            var lz = face * (faceZ - dp * 0.5f - 0.012f - push);
            var book = GameKit.Box(bc, "Ledger", new Vector3(x + thick * 0.5f, shelfY + 0.011f + h * 0.5f, lz), new Vector3(thick, h, dp), ledgerMats[rnd.Next(ledgerMats.Length)]);
            book.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            float spineZ = lz - face * dp * 0.5f;
            // the stitched spine: three bands of thread across it, a paper label with the year in ink
            for (int k = 0; k < 3; k++)
                GameKit.Box(bc, "LedgerStitch", new Vector3(x + thick * 0.5f, shelfY + 0.06f + k * (h - 0.1f) * 0.5f, spineZ - face * 0.002f), new Vector3(thick * 0.96f, 0.008f, 0.004f), thread).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameKit.Box(bc, "LedgerLabel", new Vector3(x + thick * 0.5f, shelfY + h * 0.62f, spineZ - face * 0.003f), new Vector3(thick * 0.7f, 0.07f, 0.002f), label).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var tm = GameKit.Solid(GameKit.Text(bc, year.ToString(), new Vector3(x + thick * 0.5f, shelfY + h * 0.62f, spineZ - face * 0.0045f), Quaternion.Euler(0f, face < 0f ? 0f : 180f, 90f), 0.016f, new Color(0.15f, 0.1f, 0.08f)));
            year += 1 + rnd.Next(2);
            x += thick + 0.003f;
        }
    }

    /// <summary>An enamel plate (dark blue, white letters) screwed to the shelving: readable from the aisle.</summary>
    void EnamelPlate(string text, Vector3 at, Quaternion rot, float w, float h, float letter)
    {
        var plate = new GameObject("EnamelPlate_" + text).transform; plate.SetParent(transform, false);
        plate.position = at; plate.rotation = rot;
        GameKit.Box(plate, "Plate", Vector3.zero, new Vector3(w, h, 0.006f), GameKit.Lit(new Color(0.08f, 0.14f, 0.32f), 0.65f)).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameKit.Box(plate, "PlateRim", new Vector3(0f, 0f, 0.001f), new Vector3(w + 0.012f, h + 0.012f, 0.004f), GameKit.Lit(new Color(0.85f, 0.85f, 0.82f), 0.6f)).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameKit.Solid(GameKit.Text(plate, text, new Vector3(0f, 0f, -0.0045f), Quaternion.identity, letter, new Color(0.95f, 0.95f, 0.92f)));
    }

    /// <summary>Where the 1942 folder belongs: its box-file open and empty, a clean rectangle in the dust, the register clipped beside it.</summary>
    void EmptyFolderPlace(Vector3 slot, float faceX)
    {
        var card = GameKit.Lit(new Color(0.55f, 0.45f, 0.3f), 0.15f);
        var box = new GameObject("BoxFile1942").transform; box.SetParent(transform, false); box.position = slot;
        GameKit.Box(box, "BoxFileBack", new Vector3(0f, 0.14f, 0.2f), new Vector3(0.4f, 0.28f, 0.01f), card);
        GameKit.Box(box, "BoxFileBottom", new Vector3(0f, 0.005f, 0f), new Vector3(0.4f, 0.01f, 0.4f), card);
        foreach (float s in new[] { -1f, 1f }) GameKit.Box(box, "BoxFileSide", new Vector3(s * 0.2f, 0.14f, 0f), new Vector3(0.01f, 0.28f, 0.4f), card);
        var lid = GameKit.Box(box, "BoxFileLid", new Vector3(0f, 0.3f, -0.2f), new Vector3(0.4f, 0.01f, 0.4f), card);
        lid.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
        GameKit.Solid(GameKit.Text(box, "C / 14 / 1942", new Vector3(0f, 0.2f, 0.194f), Quaternion.Euler(0f, 180f, 0f), 0.03f, new Color(0.15f, 0.1f, 0.08f)));
        box.rotation = Quaternion.Euler(0f, faceX > 0f ? -90f : 90f, 0f);
        // the register, clipped upright to the shelf's front edge beside the empty box, facing the aisle
        signOutSheet = PaperKit.SignOutSheet(transform, slot + new Vector3(faceX * 0.2f, 0.17f, -0.36f), faceX > 0f ? -90f : 90f);
        signOutSheet.rotation = Quaternion.Euler(0f, faceX > 0f ? -90f : 90f, 0f) * Quaternion.Euler(-80f, 0f, 0f);
        var clip = GameKit.Box(signOutSheet, "BulldogClip", new Vector3(0f, 0.003f, 0.15f), new Vector3(0.05f, 0.012f, 0.02f), GameKit.Lit(new Color(0.15f, 0.15f, 0.16f), 0.6f, 0.9f));
    }

    /// <summary>The drawer slides out on its runners; the brass key lies on the felt inside.</summary>
    IEnumerator OpenDrawerOnKey()
    {
        if (drawerFront == null) yield break;
        if (brassKey == null)
        {
            brassKey = new GameObject("BrassKey").transform; brassKey.SetParent(drawerFront, false);
            brassKey.localPosition = new Vector3(0.05f, 0.03f, 0.18f); brassKey.localRotation = Quaternion.Euler(0f, 25f, 0f);
            var brass = GameKit.Lit(new Color(0.8f, 0.6f, 0.26f), 0.82f, 1f);
            var bow = GameKit.Cyl(brassKey, "KeyBow", new Vector3(-0.035f, 0f, 0f), 0.014f, 0.004f, brass);
            GameKit.Box(brassKey, "KeyShank", new Vector3(0.005f, 0f, 0f), new Vector3(0.06f, 0.004f, 0.006f), brass);
            GameKit.Box(brassKey, "KeyBit", new Vector3(0.03f, 0f, 0.007f), new Vector3(0.012f, 0.004f, 0.012f), brass);
            GameKit.Box(drawerFront, "DrawerFelt", new Vector3(0f, 0.02f, 0.2f), new Vector3(0.74f, 0.004f, 0.36f), GameKit.Lit(new Color(0.18f, 0.08f, 0.1f), 0.1f));
        }
        Vector3 a = drawerFront.localPosition, b = a + new Vector3(0f, 0f, -0.2f);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime) { drawerFront.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / 0.45f)); yield return null; }
        drawerFront.localPosition = b;
        sfx.PlayOneShot(Tone(140f, 0.2f, 16f, 0.35f, true), 0.3f);
        yield return Turn(brassKey.position, 0.5f);
        yield return Wait(0.6f);
    }
}

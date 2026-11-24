using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Act III: the stair between the reading room and the Record Room. It is walked, not faded.
/// BuildBasement still authors the Record Room at its old origin (Base, x = 60). MoveRecordRoom then carries the whole
/// room under the north end of the library, so the door behind the reading room opens on a real flight of stone
/// steps: twenty risers of 17 cm on 28 cm goings, Kota-stone treads with brass nosing strips, a teak handrail on iron
/// brackets, an oil-painted dado, and bulbs at the top, half-way and the foot. An invisible ramp under the nosing line
/// carries the player's capsule, so the camera goes down smoothly while the feet stay on the steps.
/// Light, fog and the room tone blend with the player's height on the stair. The story's floor (area 0 / 1) changes
/// half-way down.
/// </summary>
public partial class Act3Story
{
    static readonly Vector3 Below = new Vector3(5.6f, -3.4f, 26f);      // the Record Room's real centre, floor level
    const float DoorX = 5.6f, DoorHalf = 0.525f, DoorH = 2.1f;         // the doorway at both ends of the stair
    const float WellX0 = 4.95f, WellX1 = 6.25f;                          // the stairwell's inner wall faces
    const float WellZ0 = 11.0f, WellZ1 = 19.0f;                          // reading-room wall face .. Record Room wall face
    const float LibWallBack = 11.25f, BaseWallBack = 18.75f;             // the far faces of those two walls
    const float TopZ = 12.25f, Rise = 0.17f, Going = 0.28f, LowY = -3.4f;
    const int Risers = 20;
    static float PitchTan => -LowY / (Going * Risers);                  // 3.4 m down over 5.6 m
    static float FlightEnd => TopZ + Going * Risers;                    // where the nosing line meets the Record Room's floor
    static float NoseY(float z) => -Mathf.Clamp(z - TopZ, 0f, Going * Risers) * PitchTan;

    Transform recordRoom, libDoor;
    bool doorOpen, basementTalked;
    AudioSource libSrc, baseSrc;
    float downK = -1f;
    Coroutine arriving;
    float stairMinY = 1f, stairMaxStep;          // read by the route: the lowest point walked, the largest move in one frame
    Vector3 lastPos; bool lastInWell;

    // ======================================================================= WORLD
    /// <summary>Re-parents everything BuildBasement made (children from firstChild on) and moves it under the library.</summary>
    void MoveRecordRoom(int firstChild)
    {
        recordRoom = new GameObject("RecordRoom").transform; recordRoom.SetParent(transform, false);
        var kids = new List<Transform>();
        for (int i = firstChild; i < transform.childCount; i++) { var k = transform.GetChild(i); if (k != recordRoom) kids.Add(k); }
        foreach (var k in kids) k.SetParent(recordRoom, true);
        recordRoom.position += Below - Base;
    }

    static List<GameObject> Named(Transform parent, string name)
    {
        var l = new List<GameObject>();
        foreach (Transform k in parent) if (k.name == name) l.Add(k.gameObject);
        return l;
    }

    static void Remove(GameObject go) { if (go == null) return; go.SetActive(false); Destroy(go); }

    /// <summary>Cuts the doorway (DoorX +/- DoorHalf, DoorH high from the box's foot) out of an axis-aligned box that runs
    /// along X. The pieces keep the box's material and collider and get their own tiling.</summary>
    void CutDoorway(GameObject box, string tex, float metersPerTile)
    {
        if (box == null) { Debug.LogWarning("[Act3] a wall to cut the Record Room doorway from is missing"); return; }
        var t = box.transform; Vector3 c = t.position, s = t.lossyScale;
        float bx0 = c.x - s.x * 0.5f, bx1 = c.x + s.x * 0.5f, by0 = c.y - s.y * 0.5f, by1 = c.y + s.y * 0.5f;
        float x0 = DoorX - DoorHalf, x1 = DoorX + DoorHalf, yTop = by0 + DoorH;
        bool col = box.GetComponent<Collider>() != null;
        var mat = box.GetComponent<Renderer>().sharedMaterial;
        Color tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
        var par = t.parent; string n = box.name;
        Remove(box);
        void Piece(float xa, float xb, float ya, float yb)
        {
            if (xb - xa < 0.005f || yb - ya < 0.005f) return;
            var p = GameKit.Box(par, n, Vector3.zero, new Vector3(xb - xa, yb - ya, s.z), mat, col);
            p.transform.position = new Vector3((xa + xb) * 0.5f, (ya + yb) * 0.5f, c.z);
            if (tex != null) GameKit.Surface(p, tex, metersPerTile, 1f, 1f, tint);
        }
        Piece(bx0, Mathf.Min(x0, bx1), by0, by1);
        Piece(Mathf.Max(x1, bx0), bx1, by0, by1);
        Piece(Mathf.Max(x0, bx0), Mathf.Min(x1, bx1), Mathf.Max(by0, yTop), by1);
    }

    /// <summary>A box laid along the stair's pitch from z0 to z1, its centre line starting at height y0, lifted by `lift`
    /// along the slope's normal. wx is its width across the stair, thick its depth perpendicular to the slope.</summary>
    GameObject Pitched(string n, float x, float z0, float z1, float y0, float wx, float thick, float lift, Material m, bool col = false)
    {
        float ang = Mathf.Atan(PitchTan);
        var rot = Quaternion.Euler(ang * Mathf.Rad2Deg, 0f, 0f);
        var a = new Vector3(x, y0, z0); var b = new Vector3(x, y0 - (z1 - z0) * PitchTan, z1);
        var go = GameKit.Box(transform, n, Vector3.zero, new Vector3(wx, thick, (z1 - z0) / Mathf.Cos(ang)), m, col);
        go.transform.rotation = rot;
        go.transform.position = (a + b) * 0.5f + rot * Vector3.up * lift;
        return go;
    }

    static void NoShadow(GameObject go) { var r = go.GetComponent<Renderer>(); if (r != null) r.shadowCastingMode = ShadowCastingMode.Off; }

    void BuildStairwell()
    {
        var lime = new Color(0.84f, 0.8f, 0.68f);          // lime-washed plaster
        var dado = new Color(0.17f, 0.27f, 0.21f);         // oil-painted green dado, as on every old municipal stair
        var kota = new Color(0.47f, 0.5f, 0.44f);          // Kota stone: grey-green, a little polished by feet
        var teak = new Color(0.3f, 0.19f, 0.11f);
        var brass = new Color(0.78f, 0.6f, 0.3f);
        var iron = new Color(0.1f, 0.1f, 0.1f);
        float w = WellX1 - WellX0, ang = Mathf.Atan(PitchTan), cosA = Mathf.Cos(ang);

        // ---- the two doorways: the reading room's north wall and the Record Room's south wall are opened up
        CutDoorway(transform.Find("Library_WallN")?.gameObject, "plaster", 2f);
        foreach (var go in Named(transform, "Wainscot")) if (Mathf.Abs(go.transform.position.z - 10.97f) < 0.02f) CutDoorway(go, "wood", 1.2f);
        foreach (var go in Named(transform, "ChairRail")) if (Mathf.Abs(go.transform.position.z - 10.97f) < 0.02f) CutDoorway(go, null, 1f);
        CutDoorway(recordRoom.Find("Basement_WallS")?.gameObject, "facade", 2f);
        foreach (var go in Named(transform, "BasementDoorFrame")) Remove(go);        // the painted-on door of the old fade
        foreach (var go in Named(transform, "BasementDoorDark")) Remove(go);
        foreach (var go in Named(recordRoom, "StairsUpFrame")) Remove(go);

        // ---- the shaft: side walls from below the Record Room's floor to above the landing's ceiling
        foreach (float sx in new[] { WellX0 - 0.125f, WellX1 + 0.125f })
            GameKit.Surface(B("StairWall", new Vector3(sx, -0.4f, (WellZ0 + WellZ1) * 0.5f), new Vector3(0.25f, 6.4f, WellZ1 - WellZ0), lime, true), "plaster", 1.6f, 1f, 1f, lime);

        // ---- floors: the top landing (level with the reading room) and the foot (level with the Record Room)
        var stone = GameKit.Lit(kota, 0.5f);
        GameKit.Surface(B("StairLanding", new Vector3(DoorX, -0.05f, (LibWallBack + TopZ) * 0.5f), new Vector3(w, 0.1f, TopZ - LibWallBack), kota, true, 0.5f), "marble", 0.9f, 0.8f, 1f, kota);
        GameKit.Surface(B("LandingNosing", new Vector3(DoorX, -0.02f, TopZ + 0.015f), new Vector3(w, 0.04f, 0.03f), kota, false, 0.5f), "marble", 0.9f, 0.8f, 1f, kota);
        float lastNose = TopZ + Going * (Risers - 1);
        GameKit.Surface(B("StairFoot", new Vector3(DoorX, LowY - 0.05f, (lastNose + BaseWallBack) * 0.5f), new Vector3(w, 0.1f, BaseWallBack - lastNose), kota, true, 0.5f), "marble", 0.9f, 0.8f, 1f, kota);
        // marble thresholds in both doorways, over the joint between the rooms' floors and the stair's stone
        foreach (var th in new[] { new Vector2(0f, (WellZ0 + LibWallBack) * 0.5f), new Vector2(LowY, (BaseWallBack + WellZ1) * 0.5f) })
            NoShadow(Tex(B("Threshold", new Vector3(DoorX, th.x + 0.006f, th.y), new Vector3(DoorHalf * 2f, 0.012f, 0.27f), new Color(0.8f, 0.78f, 0.72f), false, 0.55f), "marble", 0.6f, new Color(0.8f, 0.78f, 0.72f)));

        // ---- the flight: a stone tread with a nosing over a riser, nineteen times; a brass strip near each nosing
        var rnd = new System.Random(19);
        var brassM = GameKit.Lit(brass, 0.5f, 0.85f);
        for (int j = 0; j < Risers - 1; j++)
        {
            float z0 = TopZ + Going * j, z1 = z0 + Going, top = -Rise * (j + 1);
            float tone = 0.93f + 0.12f * (float)rnd.NextDouble();
            GameKit.Surface(B("StepRiser", new Vector3(DoorX, top - 0.24f, (z0 + z1) * 0.5f), new Vector3(w, 0.4f, Going), kota * 0.8f), "marble", 0.9f, 0.6f, 1f, kota * 0.8f * tone);
            GameKit.Surface(B("Tread", new Vector3(DoorX, top - 0.02f, (z0 + z1 + 0.03f) * 0.5f), new Vector3(w, 0.04f, Going + 0.03f), kota * tone, false, 0.55f), "marble", 0.9f, 0.8f, 1f, kota * tone);
            NoShadow(GameKit.Box(transform, "NosingStrip", new Vector3(DoorX, top + 0.002f, z1 - 0.02f), new Vector3(w - 0.3f, 0.004f, 0.025f), brassM));
            // a walked-pale middle and dust along both walls, where feet never go
            NoShadow(B("TreadWear", new Vector3(DoorX + 0.05f, top + 0.0015f, (z0 + z1) * 0.5f + 0.02f), new Vector3(0.5f, 0.002f, 0.15f), kota * (tone * 1.04f), false, 0.62f));
            foreach (float ex in new[] { WellX0 + 0.04f, WellX1 - 0.04f })
                NoShadow(B("TreadDust", new Vector3(ex, top + 0.001f, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.002f, Going), kota * 0.62f, false, 0.05f));
        }
        NoShadow(GameKit.Box(transform, "NosingStrip", new Vector3(DoorX, 0.002f, TopZ - 0.02f), new Vector3(w - 0.3f, 0.004f, 0.025f), brassM));

        // the ramp the capsule walks on: along the nosing line, invisible, running on under the Record Room's floor
        var ramp = Pitched("StairRamp", DoorX, TopZ, FlightEnd + 0.3f, 0f, w, 0.2f, -0.1f, stone, true);
        ramp.GetComponent<Renderer>().enabled = false;

        // ---- ceilings: flat over the landing, a soffit along the flight, flat again over the foot
        var limeM = GameKit.Lit(lime * 0.95f, 0.2f);
        GameKit.Surface(GameKit.Box(transform, "StairCeiling", new Vector3(DoorX, 2.65f, (WellZ0 + TopZ) * 0.5f), new Vector3(w + 0.3f, 0.1f, TopZ - WellZ0), limeM), "plaster", 1.6f);
        GameKit.Surface(Pitched("StairSoffit", DoorX, TopZ, FlightEnd, 2.6f, w + 0.3f, 0.1f, 0.05f, limeM), "plaster", 1.6f);
        GameKit.Surface(GameKit.Box(transform, "StairCeilingFoot", new Vector3(DoorX, -0.75f, (FlightEnd + WellZ1) * 0.5f), new Vector3(w + 0.3f, 0.1f, WellZ1 - FlightEnd), limeM), "plaster", 1.6f);

        // ---- the dado: green oil paint to 1.1 m above the steps, a darker rail along its top, on both walls
        var dadoM = GameKit.Lit(dado, 0.5f);
        var railM = GameKit.Lit(dado * 0.55f, 0.6f);
        foreach (float side in new[] { -1f, 1f })
        {
            float fx = side < 0 ? WellX0 + 0.004f : WellX1 - 0.004f, rx = side < 0 ? WellX0 + 0.01f : WellX1 - 0.01f;
            // one painted plate per wall that follows the steps and bends level at both landings (separate boxes left a
            // pale triangle at each bend)
            WallBand("Dado", fx, -side, -0.45f, 1.1f, dadoM, "plaster", 1.2f, dado);      // from inside the steps up
            WallBand("DadoRail", rx, -side, 1.1f - 0.018f, 1.1f + 0.018f, railM, null, 1f, dado * 0.55f);
        }

        // ---- the handrail: teak, 90 cm above the nosings, on iron brackets off the east wall, level at both ends
        var teakM = GameKit.Lit(teak, 0.6f);
        var ironM = GameKit.Lit(iron, 0.4f, 0.6f);
        float hx = WellX1 - 0.075f, hTop = 0.9f, hLow = LowY + 0.9f;
        Rail("Handrail", new Vector3(hx, hTop, 11.55f), new Vector3(hx, hTop, TopZ), teakM);
        Rail("Handrail", new Vector3(hx, hTop, TopZ), new Vector3(hx, hLow, FlightEnd), teakM);
        Rail("Handrail", new Vector3(hx, hLow, FlightEnd), new Vector3(hx, hLow, 18.45f), teakM);
        foreach (var k in new[] { new Vector3(hx, hTop, TopZ), new Vector3(hx, hLow, FlightEnd) }) GameKit.Sphere(transform, "HandrailKnee", k, 0.058f, teakM);
        foreach (float ez in new[] { 11.55f, 18.45f })        // the ends turn back into the wall
        {
            float ey = ez < 12f ? hTop : hLow;
            Rail("HandrailReturn", new Vector3(hx, ey, ez), new Vector3(WellX1, ey, ez), teakM);
            GameKit.Sphere(transform, "HandrailKnee", new Vector3(hx, ey, ez), 0.058f, teakM);
        }
        foreach (float bz in new[] { 11.9f, 13.3f, 14.8f, 16.3f, 17.6f, 18.2f })
        {
            float by = (bz < TopZ ? hTop : bz > FlightEnd ? hLow : NoseY(bz) + 0.9f) - 0.045f;
            NoShadow(GameKit.Box(transform, "RailBracket", new Vector3((hx + WellX1) * 0.5f, by, bz), new Vector3(WellX1 - hx, 0.014f, 0.022f), ironM));
            NoShadow(GameKit.Box(transform, "RailBracketRose", new Vector3(WellX1 - 0.004f, by, bz), new Vector3(0.008f, 0.06f, 0.06f), ironM));
            NoShadow(GameKit.Box(transform, "RailBracketCup", new Vector3(hx, by + 0.012f, bz), new Vector3(0.03f, 0.012f, 0.05f), ironM));
        }

        // ---- wiring: a switchboard by the door, conduit up the wall and down along the soffit to the lamps
        var condM = GameKit.Lit(new Color(0.55f, 0.55f, 0.52f), 0.35f, 0.3f);
        float cx = WellX0 + 0.014f;
        NoShadow(B("SwitchBoard", new Vector3(WellX0 + 0.012f, 1.3f, 11.55f), new Vector3(0.022f, 0.2f, 0.15f), teak, false, 0.45f));
        foreach (float sz in new[] { 11.52f, 11.58f })
        {
            NoShadow(B("SwitchBase", new Vector3(WellX0 + 0.027f, 1.33f, sz), new Vector3(0.01f, 0.045f, 0.035f), new Color(0.06f, 0.05f, 0.05f), false, 0.7f));
            NoShadow(B("SwitchToggle", new Vector3(WellX0 + 0.038f, 1.34f, sz), new Vector3(0.012f, 0.014f, 0.008f), new Color(0.05f, 0.04f, 0.04f), false, 0.8f));
        }
        NoShadow(GameKit.Box(transform, "Conduit", new Vector3(cx, (1.4f + 2.5f) * 0.5f, 11.55f), new Vector3(0.022f, 1.1f, 0.022f), condM));
        NoShadow(GameKit.Box(transform, "Conduit", new Vector3(cx, 2.5f, (11.55f + TopZ) * 0.5f), new Vector3(0.022f, 0.022f, TopZ - 11.55f), condM));
        NoShadow(Pitched("Conduit", cx, TopZ, FlightEnd, 2.5f, 0.022f, 0.022f, 0f, condM));
        NoShadow(GameKit.Box(transform, "Conduit", new Vector3(cx, -0.9f, (FlightEnd + 18.3f) * 0.5f), new Vector3(0.022f, 0.022f, 18.3f - FlightEnd), condM));

        // ---- light: an enamel pendant over the landing, a caged bulb half-way on the west wall, a batten at the foot
        var warm = new Color(1f, 0.79f, 0.52f);
        Pendant(new Vector3(DoorX, 2.6f, 11.75f), 0.3f, warm);
        PL(new Vector3(DoorX, 2.15f, 11.75f), warm, 1.7f, 4.2f);
        float mz = 15.0f, my = NoseY(mz) + 2.05f;
        var rose = GameKit.Cyl(transform, "LampRose", new Vector3(WellX0 + 0.01f, my + 0.12f, mz), 0.045f, 0.02f, ironM);
        rose.transform.rotation = Quaternion.Euler(0f, 0f, 90f); NoShadow(rose);
        NoShadow(GameKit.Box(transform, "Conduit", new Vector3(cx, (my + 0.12f + NoseY(mz) + 2.5f) * 0.5f, mz), new Vector3(0.022f, NoseY(mz) + 2.5f - (my + 0.12f), 0.022f), condM));
        NoShadow(GameKit.Box(transform, "LampArm", new Vector3(WellX0 + 0.09f, my + 0.12f, mz), new Vector3(0.16f, 0.016f, 0.016f), ironM));
        NoShadow(GameKit.Box(transform, "LampHolder", new Vector3(WellX0 + 0.17f, my + 0.08f, mz), new Vector3(0.035f, 0.05f, 0.035f), GameKit.Lit(new Color(0.08f, 0.06f, 0.05f), 0.7f)));
        GameKit.Sphere(transform, "LampBulb", new Vector3(WellX0 + 0.17f, my, mz), 0.075f, GameKit.Glow(warm * 0.5f, warm, 3.2f));
        for (int i = 0; i < 4; i++)          // the wire cage round the bulb
        {
            float a = i * Mathf.PI * 0.5f;
            NoShadow(GameKit.Box(transform, "LampCage", new Vector3(WellX0 + 0.17f + Mathf.Cos(a) * 0.055f, my - 0.005f, mz + Mathf.Sin(a) * 0.055f), new Vector3(0.004f, 0.13f, 0.004f), ironM));
        }
        NoShadow(GameKit.Box(transform, "LampCageRing", new Vector3(WellX0 + 0.17f, my - 0.07f, mz), new Vector3(0.11f, 0.004f, 0.11f), ironM));
        var mid = PL(new Vector3(WellX0 + 0.32f, my - 0.05f, mz), warm, 2.1f, 5.2f);
        mid.shadows = LightShadows.Soft; mid.shadowStrength = 0.8f;        // the handrail and the nosings throw shadows down the flight
        Pendant(new Vector3(DoorX, -0.8f, 18.3f), 0.12f, warm);
        PL(new Vector3(DoorX, -1.05f, 18.25f), warm, 1.6f, 4.2f);

        // ---- things on the walls
        // a notice at the top, the library's own rule about the registers (east wall, facing the stair)
        var paper = new Color(0.9f, 0.86f, 0.72f); var ink = new Color(0.12f, 0.1f, 0.12f);
        var noteRot = Quaternion.Euler(0f, 90f, 0f);
        NoShadow(B("StairNotice", new Vector3(WellX1 - 0.003f, 1.55f, 11.75f), new Vector3(0.003f, 0.3f, 0.22f), paper, false, 0.1f));
        NoShadow(B("StairNoticePin", new Vector3(WellX1 - 0.006f, 1.69f, 11.75f), new Vector3(0.006f, 0.012f, 0.012f), new Color(0.7f, 0.1f, 0.1f), false, 0.6f));
        string[] lines = { "NOTICE", "RECORD ROOM", "Staff and authorised", "readers only.", "Every register taken out", "must be signed for.", "- Librarian" };
        float[] sizes = { 0.03f, 0.024f, 0.017f, 0.017f, 0.015f, 0.015f, 0.016f };
        float ly = 1.66f;
        for (int i = 0; i < lines.Length; i++)
        {
            GameKit.Solid(GameKit.Text(transform, lines[i], new Vector3(WellX1 - 0.0055f, ly, 11.75f), noteRot, sizes[i], i == 1 ? new Color(0.55f, 0.08f, 0.06f) : ink));
            ly -= i < 2 ? 0.042f : 0.032f;
        }
        // two red fire buckets of sand on a bracket at the foot, as in every government building
        var red = GameKit.Lit(new Color(0.62f, 0.07f, 0.05f), 0.55f, 0.2f);
        NoShadow(GameKit.Box(transform, "BucketBracket", new Vector3(WellX0 + 0.03f, LowY + 1.02f, 18.2f), new Vector3(0.04f, 0.03f, 0.6f), ironM));
        foreach (float bz in new[] { 18.02f, 18.38f })
        {
            GameKit.Cyl(transform, "FireBucket", new Vector3(WellX0 + 0.14f, LowY + 0.84f, bz), 0.12f, 0.26f, red);
            NoShadow(GameKit.Cyl(transform, "FireBucketSand", new Vector3(WellX0 + 0.14f, LowY + 0.955f, bz), 0.112f, 0.01f, GameKit.Lit(new Color(0.62f, 0.52f, 0.38f), 0.05f)));
            NoShadow(GameKit.Box(transform, "FireBucketHook", new Vector3(WellX0 + 0.08f, LowY + 0.99f, bz), new Vector3(0.12f, 0.008f, 0.008f), ironM));
            GameKit.Solid(GameKit.Text(transform, "FIRE", new Vector3(WellX0 + 0.262f, LowY + 0.84f, bz), Quaternion.Euler(0f, -90f, 0f), 0.04f, new Color(0.95f, 0.92f, 0.85f)));
        }

        // ---- door frames on both doorways, the reading-room door (closed until Priya's files are read), the
        // Record Room door standing open
        Casings(WellZ0, -1f, 0f, teak, WellZ0, LibWallBack);
        Casings(LibWallBack, 1f, 0f, teak * 0.9f, 0f, 0f);
        Casings(WellZ1, 1f, LowY, teak * 0.9f, BaseWallBack, WellZ1);
        Casings(BaseWallBack, -1f, LowY, teak * 0.9f, 0f, 0f);
        libDoor = DoorLeaf(new Vector3(DoorX - DoorHalf + 0.02f, 0f, WellZ0), 0f, new Color(0.3f, 0.18f, 0.1f));
        var inner = DoorLeaf(new Vector3(DoorX + DoorHalf - 0.02f, LowY, WellZ1), 180f, new Color(0.27f, 0.17f, 0.1f));
        inner.rotation = Quaternion.Euler(0f, 180f + 96f, 0f);
        EnamelPlate("STAIRS UP  -  READING ROOM", new Vector3(DoorX, LowY + DoorH + 0.24f, WellZ1 + 0.012f), Quaternion.Euler(0f, 180f, 0f), 0.78f, 0.1f, 0.05f);
        // the reading room's lamp over the door gets a fitting (its light was there already)
        NoShadow(B("DoorLampArm", new Vector3(DoorX, 2.86f, 10.93f), new Vector3(0.025f, 0.025f, 0.14f), brass, false, 0.6f, 0.85f));
        GameKit.Sphere(transform, "DoorLampGlobe", new Vector3(DoorX, 2.86f, 10.84f), 0.15f, GameKit.Glow(warm * 0.45f, warm, 2.2f));
    }

    /// <summary>A band on a stairwell wall between heights lo and hi above the walking line (level on the landings,
    /// following the pitch on the flight), as one bent mesh. inward (+1 / -1) is the x direction it faces.</summary>
    void WallBand(string n, float x, float inward, float lo, float hi, Material m, string tex, float tile, Color tint)
    {
        float[] zs = { LibWallBack, TopZ, FlightEnd, BaseWallBack };
        float Walk(float z) => NoseY(z);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float u = 0f;
        for (int i = 0; i < zs.Length; i++)
        {
            float z = zs[i], y = Walk(z);
            if (i > 0) u += new Vector2(z - zs[i - 1], y - Walk(zs[i - 1])).magnitude;
            v.Add(new Vector3(x, y + lo, z)); uv.Add(new Vector2(u, lo) / tile);
            v.Add(new Vector3(x, y + hi, z)); uv.Add(new Vector2(u, hi) / tile);
        }
        for (int i = 0; i < zs.Length - 1; i++)
        {
            int a = i * 2;
            // faces -x when inward < 0 (the east wall), +x otherwise
            if (inward > 0f) { tri.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 }); }
            else { tri.AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 }); }
        }
        var mesh = new Mesh { name = n };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var go = new GameObject(n); go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        if (tex != null)
        {
            var mat = ProcTex.Mat(ProcTex.Get(tex), tint, Vector2.one, 1.4f, 1f);
            r.sharedMaterial = mat;
        }
    }

    /// <summary>A round teak rail between two points.</summary>
    void Rail(string n, Vector3 a, Vector3 b, Material m)
    {
        var go = GameKit.Cyl(transform, n, (a + b) * 0.5f, 0.026f, (b - a).magnitude, m);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
    }

    /// <summary>A bulb under a white enamel shade, hung from the ceiling on a cord.</summary>
    void Pendant(Vector3 ceiling, float cord, Color warm)
    {
        NoShadow(GameKit.Box(transform, "PendantCord", ceiling + Vector3.down * (cord * 0.5f), new Vector3(0.008f, cord, 0.008f), GameKit.Lit(new Color(0.1f, 0.1f, 0.1f), 0.3f)));
        Vector3 s = ceiling + Vector3.down * cord;
        NoShadow(GameKit.Cyl(transform, "PendantCap", s + Vector3.down * 0.02f, 0.03f, 0.05f, GameKit.Lit(new Color(0.15f, 0.14f, 0.13f), 0.5f, 0.5f)));
        NoShadow(GameKit.Cyl(transform, "PendantShade", s + Vector3.down * 0.07f, 0.15f, 0.04f, GameKit.Lit(new Color(0.18f, 0.3f, 0.22f), 0.7f)));
        NoShadow(GameKit.Cyl(transform, "PendantShadeIn", s + Vector3.down * 0.092f, 0.145f, 0.004f, GameKit.Glow(new Color(0.5f, 0.45f, 0.38f), warm * 0.6f, 0.8f)));
        GameKit.Sphere(transform, "PendantBulb", s + Vector3.down * 0.13f, 0.07f, GameKit.Glow(warm * 0.5f, warm, 3.4f));
    }

    /// <summary>Architraves round the doorway on the wall face at z = face, on the side `facing` (-1: toward -z).</summary>
    void Casings(float face, float facing, float floor, Color c, float liningZ0, float liningZ1)
    {
        var m = GameKit.Lit(c, 0.45f);
        float zc = face + facing * 0.045f, d = 0.09f, wcas = 0.09f;
        float x0 = DoorX - DoorHalf, x1 = DoorX + DoorHalf;
        foreach (float x in new[] { x0 - wcas * 0.5f, x1 + wcas * 0.5f })
        {
            GameKit.Box(transform, "DoorCasing", new Vector3(x, floor + (DoorH + wcas) * 0.5f, zc), new Vector3(wcas, DoorH + wcas, d), m);
            GameKit.Box(transform, "DoorPlinth", new Vector3(x, floor + 0.11f, zc + facing * 0.006f), new Vector3(wcas + 0.012f, 0.22f, d + 0.012f), m);
        }
        GameKit.Box(transform, "DoorCasing", new Vector3(DoorX, floor + DoorH + wcas * 0.5f, zc), new Vector3(x1 - x0 + 2f * wcas + 0.02f, wcas, d), m);
        GameKit.Box(transform, "DoorCasingCap", new Vector3(DoorX, floor + DoorH + wcas + 0.012f, zc + facing * 0.006f), new Vector3(x1 - x0 + 2f * wcas + 0.06f, 0.024f, d + 0.016f), m);
        // the linings of the reveal (once per doorway: given with the room side's casings)
        if (liningZ1 > liningZ0)
        {
            float z0 = liningZ0, z1 = liningZ1;
            foreach (float x in new[] { x0 + 0.01f, x1 - 0.01f })
                GameKit.Box(transform, "DoorLining", new Vector3(x, floor + DoorH * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.02f, DoorH, z1 - z0), m);
            GameKit.Box(transform, "DoorLining", new Vector3(DoorX, floor + DoorH - 0.01f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.02f, z1 - z0), m);
        }
    }

    /// <summary>A four-panel painted door hung on its hinge at `hinge`. Local +x runs from the hinge to the latch, local
    /// -z is the face it opens toward. Returns the hinge (rotate it to open the door).</summary>
    Transform DoorLeaf(Vector3 hinge, float yaw, Color paint)
    {
        var pivot = new GameObject("DoorHinge").transform; pivot.SetParent(transform, false);
        pivot.position = hinge; pivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        float W = DoorHalf * 2f - 0.04f - 0.006f, H = DoorH - 0.035f, T = 0.045f;
        var paintM = GameKit.Lit(paint, 0.42f);
        var panelM = GameKit.Lit(paint * 0.92f, 0.38f);
        var core = GameKit.Box(pivot, "DoorPanelCore", new Vector3(W * 0.5f, 0.012f + H * 0.5f, T * 0.5f), new Vector3(W, H, 0.022f), panelM, true);
        GameKit.Surface(core, "wood", 0.8f, 0.8f, 1f, paint * 0.92f);
        void Part(string n, float x, float y, float pw, float ph)
        {
            var p = GameKit.Box(pivot, n, new Vector3(x, 0.012f + y, T * 0.5f), new Vector3(pw, ph, T), paintM);
            GameKit.Surface(p, "wood", 0.8f, 0.8f, 1f, paint);
        }
        Part("DoorStile", 0.06f, H * 0.5f, 0.12f, H);
        Part("DoorStile", W - 0.06f, H * 0.5f, 0.12f, H);
        Part("DoorRail", W * 0.5f, H - 0.06f, W - 0.24f, 0.12f);
        Part("DoorRail", W * 0.5f, 1.0f, W - 0.24f, 0.18f);
        Part("DoorRail", W * 0.5f, 0.12f, W - 0.24f, 0.24f);
        Part("DoorMuntin", W * 0.5f, (0.24f + 0.91f) * 0.5f, 0.1f, 0.91f - 0.24f);
        Part("DoorMuntin", W * 0.5f, (1.09f + H - 0.12f) * 0.5f, 0.1f, H - 0.12f - 1.09f);
        // brass lever handles on both faces, a keyhole under each, three hinges
        var br = GameKit.Lit(new Color(0.75f, 0.57f, 0.28f), 0.55f, 0.9f);
        foreach (float side in new[] { -1f, 1f })
        {
            float z = side < 0 ? -0.004f : T + 0.004f;
            NoShadow(GameKit.Box(pivot, "DoorHandlePlate", new Vector3(W - 0.07f, 1.0f, z), new Vector3(0.045f, 0.2f, 0.008f), br));
            NoShadow(GameKit.Box(pivot, "DoorHandle", new Vector3(W - 0.12f, 1.05f, z + side * 0.03f), new Vector3(0.11f, 0.018f, 0.018f), br));
            NoShadow(GameKit.Box(pivot, "DoorHandleNeck", new Vector3(W - 0.07f, 1.05f, z + side * 0.016f), new Vector3(0.018f, 0.018f, 0.028f), br));
            NoShadow(GameKit.Box(pivot, "DoorKeyhole", new Vector3(W - 0.07f, 0.93f, z + side * 0.0045f), new Vector3(0.008f, 0.02f, 0.002f), GameKit.Lit(new Color(0.03f, 0.03f, 0.03f), 0.2f)));
        }
        foreach (float hy in new[] { 0.22f, 1.05f, 1.88f })
            NoShadow(GameKit.Box(pivot, "DoorHingeLeaf", new Vector3(0.006f, hy, -0.002f), new Vector3(0.012f, 0.1f, 0.004f), GameKit.Lit(new Color(0.2f, 0.18f, 0.16f), 0.5f, 0.7f)));
        return pivot;
    }

    /// <summary>A dry hinge's creak: a stick-slip pulse train whose pitch rises and falls as the leaf swings.</summary>
    static AudioClip Creak(float len)
    {
        const int SR = 44100; int n = (int)(SR * len);
        var d = new float[n]; var r = new System.Random(7); float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, u = t / len;
            float f = 150f + 230f * Mathf.Sin(u * Mathf.PI * 0.9f) + 35f * Mathf.Sin(t * 21f);
            ph += f / SR;
            float pulse = Mathf.Exp(-(ph % 1f) * 9f);                          // one slip per cycle
            float grain = 0.55f + 0.45f * (float)r.NextDouble();
            float env = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.6f) * (0.75f + 0.25f * Mathf.Sin(t * 33f));
            lp += (pulse * grain - lp) * 0.35f;                                  // soften the clicks into wood
            d[i] = lp * env;
        }
        Norm(d, 0.45f);
        var c = AudioClip.Create("creak", n, 1, SR, false); c.SetData(d, 0); return c;
    }

    // ======================================================================= PLAY
    void StairsAudio(AudioClip lib, AudioClip down)
    {
        AudioSource Bed(AudioClip c)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = c; s.loop = true; s.spatialBlend = 0f; s.volume = 0f; s.playOnAwake = false; s.Play();
            return s;
        }
        libSrc = Bed(lib); baseSrc = Bed(down);
        ambience.Stop(); ambience.clip = null;          // `ambience` keeps its volume as the act's master fade
    }

    /// <summary>Light and fog between the reading room (0) and the Record Room (1).</summary>
    void BlendLook(float k)
    {
        CinematicLook.Ambient(Color.Lerp(new Color(0.62f, 0.6f, 0.66f), new Color(0.5f, 0.52f, 0.55f), k),
                              Color.Lerp(new Color(0.5f, 0.44f, 0.38f), new Color(0.4f, 0.39f, 0.37f), k),
                              Color.Lerp(new Color(0.24f, 0.2f, 0.16f), new Color(0.2f, 0.19f, 0.17f), k));
        RenderSettings.fogColor = Color.Lerp(new Color(0.13f, 0.12f, 0.14f), new Color(0.14f, 0.14f, 0.13f), k);
        RenderSettings.fogDensity = Mathf.Lerp(0.006f, 0.012f, k);
    }

    void StairsUpdate()
    {
        if (player == null || libSrc == null) return;
        if (area == 2) { if (libSrc.isPlaying) { libSrc.Stop(); baseSrc.Stop(); } return; }
        Vector3 p = player.transform.position;
        bool inWell = p.x > WellX0 - 0.3f && p.x < WellX1 + 0.3f && p.z > WellZ0 - 0.3f && p.z < WellZ1 + 0.3f;
        if (inWell && lastInWell) stairMaxStep = Mathf.Max(stairMaxStep, (p - lastPos).magnitude);
        if (inWell) stairMinY = Mathf.Min(stairMinY, p.y);
        lastPos = p; lastInWell = inWell;

        float k = Mathf.Clamp01(Mathf.InverseLerp(-0.3f, LowY + 0.4f, p.y));
        if (Mathf.Abs(k - downK) > 0.002f) { downK = k; BlendLook(k); }
        float m = ambience.volume;
        libSrc.volume = m * (1f - k); baseSrc.volume = m * k;
        libSrc.pitch = baseSrc.pitch = ambience.pitch;
        PriyaRoomPlayer.Surface = (inWell && p.z > WellZ0 + 0.1f) || k > 0.5f ? 1 : 0;      // stone underfoot from the threshold on

        int now = area == 0 ? (k > 0.55f ? 1 : 0) : (k < 0.45f ? 0 : 1);                   // half-way down the flight
        if (now != area)
        {
            area = now;
            if (arriving != null) StopCoroutine(arriving);
            arriving = StartCoroutine(now == 1 ? ArriveDown() : ArriveUp());
        }
    }

    IEnumerator OpenLibDoor()
    {
        BeginBusy();
        Vector3 handle = libDoor.TransformPoint(new Vector3(DoorHalf * 2f - 0.16f, 1.05f, -0.03f));
        yield return Turn(handle, 0.45f);
        sfx.PlayOneShot(Tone(1700f, 0.05f, 70f, 0.5f, true), 0.45f);        // the latch
        yield return Wait(0.18f);
        sfx.PlayOneShot(Creak(1.35f), 0.5f);
        doorOpen = true;
        Quaternion a = libDoor.rotation, b = Quaternion.Euler(0f, 96f, 0f);
        for (float t = 0f; t < 1.35f; t += Time.deltaTime)
        {
            float u = t / 1.35f;
            libDoor.rotation = Quaternion.Slerp(a, b, 1f - (1f - u) * (1f - u) * (1f - u));
            yield return null;
        }
        libDoor.rotation = b;
        yield return Turn(new Vector3(DoorX, -0.6f, 14.5f), 0.6f);           // and the stair going down into the dark
        BeginFree("Go down to the Record Room", stairsUp.position + Vector3.up * 0.3f, "Record Room");
    }

    /// <summary>On the way down, past half-way: Arjun's line once he is off the last step, then the objective.</summary>
    IEnumerator ArriveDown()
    {
        bool talked = false;
        if (!basementTalked && !cageSeen)
        {
            while (true)
            {
                if (area != 1) yield break;
                var p = player.transform.position;
                if (p.y < LowY + 0.25f && p.z > 18.2f && phase == Phase.Free) break;
                yield return null;
            }
            basementTalked = true;
            BeginBusy();
            yield return Talk("a3_arjun_basement");
            EndTalk();
            talked = true;
        }
        if (!talked) while (phase != Phase.Free) { if (area != 1) yield break; yield return null; }
        if (area != 1) yield break;
        if (cageSeen && !keyFound && deskTried && !calendarSeen)
            BeginFree("Find the drawer code - check the calendar on the east wall", calendar.position, "Wall calendar");
        else
            BeginFree(keyFound ? "Open the cage at Block C, Shelf 14" : (cageSeen ? "Find the key - try the librarian's desk upstairs" : "Find Block C, Shelf 14"),
                      cage.position + Vector3.up * 0.8f, "Block C, Shelf 14");
    }

    IEnumerator ArriveUp()
    {
        while (phase != Phase.Free) { if (area != 0) yield break; yield return null; }
        BeginFree(cageSeen && !keyFound ? "Open the librarian's desk" : "Go back down to the Record Room",
                  cageSeen && !keyFound ? libDesk.position + Vector3.up : stairsUp.position + Vector3.up * 0.3f, cageSeen && !keyFound ? "Librarian's desk" : "Record Room");
    }
}

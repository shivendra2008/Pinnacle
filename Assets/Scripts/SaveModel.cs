using System;
using System.Collections.Generic;

// The save data and the rules that change it. Plain C#, with no UnityEngine, so the rules can be unit-tested
// outside the editor (Tools/tests). Reading and writing files is in SaveSystem.cs.
//
// Two kinds of data:
//   SaveData    = the active CAMPAIGN (this playthrough): progress, evidence, choices, checkpoints.
//   ProfileData = the PLAYER, kept across campaigns: name, endings discovered, chapters unlocked, chapter-start
//                 snapshots for replay.
// What each menu entry does to them is described in Docs/SAVES.md.

/// <summary>The active campaign.</summary>
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public int actsCompleted;                         // 0..5: acts finished in this campaign
    public int currentAct = 1;                        // the act this campaign is in (its checkpoint)
    public string lastSaved = "";

    // Act I: The Empty Room
    public List<string> clues = new List<string>();   // ids of the clues found so far
    public bool dlgDone, moveOnOffered, moveOnDeclined;

    // Acts II-V: evidence notes, each tagged with the act it was found in (same length as evidence)
    public List<string> evidence = new List<string>();
    public List<int> evidenceActs = new List<int>();
    public bool bhaskarDone, vikramDone, reddyResolved;
    public bool hasUsb, hasIdCard, hasPlate;

    // Act III: The File
    public int act3Stage;                              // 0 start, 1 USB read, 2 sign-out sheet found, 3 envelope received
    public int panditAnswer = -1;                      // 0 truth, 1 people, 2 both
    public int branch = -1;                            // Act IV path: 0 Cafe Irani (Raj), 1 Kapoor house (Rohan), 2 Inspector Reddy

    // Act IV: The Web
    public bool allyRaj, allyRohan, allyReddy, hasRecordings, hasEmails;

    // Act V: Sunrise
    public int act5Stage;                              // 0 start, 1 reached the Gallery (resume there)

    // ---- version 1 fields: read only, moved into the profile by SaveRules.Migrate
    public string playerName = "";
    public int sessions;
    public List<int> endingsSeen = new List<int>();
}

/// <summary>The player, across campaigns.</summary>
[Serializable]
public class ProfileData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public string playerName = "";
    public int sessions;
    public int highestActCompleted;                    // unlocks chapter replay
    public List<int> endingsSeen = new List<int>();   // 0 Justice, 1 Sacrifice, 2 Revolution - recorded when the ending's credits roll
    public List<string> chapterStarts = new List<string>();   // [act-1] = the campaign as it stood when that act began (JSON)
}

public static class SaveRules
{
    public const int Acts = 5;

    /// <summary>Brings an older campaign up to date and moves player-level fields into the profile. Returns true if anything changed.</summary>
    public static bool Migrate(SaveData d, ProfileData p)
    {
        bool changed = false;
        if (d.version < 2)
        {
            // v1 kept the name, sessions and endings in the campaign; they belong to the player
            if (string.IsNullOrEmpty(p.playerName) && !string.IsNullOrEmpty(d.playerName)) p.playerName = d.playerName;
            p.sessions = Math.Max(p.sessions, d.sessions);
            foreach (var e in d.endingsSeen ?? new List<int>()) if (e >= 0 && e <= 2 && !p.endingsSeen.Contains(e)) p.endingsSeen.Add(e);
            p.highestActCompleted = Math.Max(p.highestActCompleted, d.actsCompleted);
            // v1 evidence was not tagged. Act II was the first act to record evidence and starting Act II over cleared
            // all of it, so tagging it as Act II keeps exactly the old behaviour for those notes.
            d.evidenceActs = new List<int>();
            if (d.evidence != null) for (int i = 0; i < d.evidence.Count; i++) d.evidenceActs.Add(2);
            d.currentAct = Clamp(d.actsCompleted + 1, 1, Acts);
            d.version = 2;
            changed = true;
        }
        d.playerName = ""; d.sessions = 0; if (d.endingsSeen != null) d.endingsSeen.Clear();
        return changed;
    }

    /// <summary>Repairs anything out of range or missing (a hand-edited or damaged file). Returns false if it had to repair something.</summary>
    public static bool Validate(SaveData d)
    {
        bool ok = true;
        if (d.clues == null) { d.clues = new List<string>(); ok = false; }
        if (d.evidence == null) { d.evidence = new List<string>(); ok = false; }
        if (d.evidenceActs == null) { d.evidenceActs = new List<int>(); ok = false; }
        if (d.endingsSeen == null) d.endingsSeen = new List<int>();
        ok &= Dedupe(d.clues);
        // evidence and its act tags must line up, with no duplicate notes
        var seen = new HashSet<string>();
        var ev = new List<string>(); var acts = new List<int>();
        for (int i = 0; i < d.evidence.Count; i++)
        {
            string e = d.evidence[i];
            if (string.IsNullOrEmpty(e) || !seen.Add(e)) { ok = false; continue; }
            ev.Add(e);
            acts.Add(i < d.evidenceActs.Count ? Clamp(d.evidenceActs[i], 2, Acts) : 2);
        }
        if (d.evidenceActs.Count != d.evidence.Count) ok = false;
        d.evidence = ev; d.evidenceActs = acts;
        ok &= Fix(ref d.actsCompleted, 0, Acts);
        ok &= Fix(ref d.currentAct, 1, Acts);
        if (d.currentAct < d.actsCompleted + 1 && d.actsCompleted < Acts) { d.currentAct = d.actsCompleted + 1; ok = false; }
        ok &= Fix(ref d.act3Stage, 0, 3);
        ok &= Fix(ref d.panditAnswer, -1, 2);
        ok &= Fix(ref d.branch, -1, 2);
        ok &= Fix(ref d.act5Stage, 0, 1);
        return ok;
    }

    public static bool Validate(ProfileData p)
    {
        bool ok = true;
        if (p.endingsSeen == null) { p.endingsSeen = new List<int>(); ok = false; }
        if (p.chapterStarts == null) { p.chapterStarts = new List<string>(); ok = false; }
        if (p.playerName == null) { p.playerName = ""; ok = false; }
        p.endingsSeen.RemoveAll(e => e < 0 || e > 2);
        ok &= Dedupe(p.endingsSeen);
        ok &= Fix(ref p.highestActCompleted, 0, Acts);
        return ok;
    }

    /// <summary>
    /// Start act `act` over: forget everything that act and every later act did (progress, choices, allies and the
    /// evidence found in them), so nothing stale reaches the later acts or the ending conditions. Earlier acts stay.
    /// </summary>
    public static void ResetFrom(SaveData d, int act)
    {
        act = Clamp(act, 1, Acts);
        if (act <= 1) { d.clues.Clear(); d.dlgDone = d.moveOnOffered = d.moveOnDeclined = false; }
        if (act <= 2) { d.bhaskarDone = d.vikramDone = d.reddyResolved = false; d.hasUsb = d.hasIdCard = d.hasPlate = false; }
        if (act <= 3) { d.act3Stage = 0; d.panditAnswer = -1; d.branch = -1; }
        if (act <= 4) { d.allyRaj = d.allyRohan = d.allyReddy = d.hasRecordings = d.hasEmails = false; }
        d.act5Stage = 0;
        for (int i = d.evidence.Count - 1; i >= 0; i--)
            if (i >= d.evidenceActs.Count || d.evidenceActs[i] >= act) { d.evidence.RemoveAt(i); if (i < d.evidenceActs.Count) d.evidenceActs.RemoveAt(i); }
        d.actsCompleted = Math.Min(d.actsCompleted, act - 1);
        d.currentAct = act;
    }

    /// <summary>Adds an evidence note once, tagged with the act it was found in. Returns false if it was already there.</summary>
    public static bool AddEvidence(SaveData d, string e, int act)
    {
        if (string.IsNullOrEmpty(e) || d.evidence.Contains(e)) return false;
        d.evidence.Add(e);
        d.evidenceActs.Add(Clamp(act, 2, Acts));
        return true;
    }

    public static void CompleteAct(SaveData d, ProfileData p, int act)
    {
        d.actsCompleted = Math.Max(d.actsCompleted, act);
        d.currentAct = Clamp(act + 1, 1, Acts);
        if (p != null) p.highestActCompleted = Math.Max(p.highestActCompleted, d.actsCompleted);
    }

    public static void RecordEnding(ProfileData p, int ending)
    {
        if (ending >= 0 && ending <= 2 && !p.endingsSeen.Contains(ending)) p.endingsSeen.Add(ending);
    }

    // ---- where Continue resumes
    public static bool InProgress(SaveData d, int act)
    {
        if (d.actsCompleted != act - 1) return false;
        switch (act)
        {
            case 1: return d.clues.Count > 0;
            case 2: return d.evidence.Count > 0 || d.bhaskarDone || d.vikramDone;
            case 3: return d.act3Stage > 0;
            case 4: return false;                    // Act IV restarts at its own start (the branch is kept)
            case 5: return d.act5Stage > 0;
        }
        return false;
    }

    public static int EvidenceCount(SaveData d) { return d.clues.Count + d.evidence.Count; }

    // ---- helpers
    static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
    static bool Fix(ref int v, int lo, int hi) { int c = Clamp(v, lo, hi); bool ok = c == v; v = c; return ok; }
    static bool Dedupe<T>(List<T> l)
    {
        var set = new HashSet<T>(); bool ok = true;
        for (int i = 0; i < l.Count; i++) if (!set.Add(l[i])) { l.RemoveAt(i--); ok = false; }
        return ok;
    }
}

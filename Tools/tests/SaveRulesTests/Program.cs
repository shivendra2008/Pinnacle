using System;
using System.Collections.Generic;

// Focused tests for the campaign rules: migration, validation, act restarts, evidence, endings, resume points.
static class Program
{
    static int run, failed;

    static void Check(bool ok, string what)
    {
        run++;
        if (!ok) { failed++; Console.WriteLine("  FAIL  " + what); }
    }

    static void Test(string name, Action body)
    {
        int before = failed;
        try { body(); } catch (Exception e) { failed++; Console.WriteLine("  FAIL  " + name + " threw " + e); }
        Console.WriteLine((failed == before ? "pass  " : "FAIL  ") + name);
    }

    static SaveData Campaign()
    {
        var d = new SaveData();
        d.clues.AddRange(new[] { "Laptop", "Diary" });
        SaveRules.AddEvidence(d, "USB", 2);
        SaveRules.AddEvidence(d, "Sign-out sheet", 3);
        SaveRules.AddEvidence(d, "Emails", 4);
        d.hasUsb = true; d.vikramDone = true; d.act3Stage = 3; d.panditAnswer = 1; d.branch = 1; d.allyRohan = true; d.hasEmails = true;
        d.actsCompleted = 4; d.currentAct = 5;
        return d;
    }

    static int Main()
    {
        Test("v1 save migrates: name, sessions and endings move to the profile", () =>
        {
            var d = new SaveData { version = 1, playerName = "Asha", sessions = 7, actsCompleted = 2 };
            d.endingsSeen.Add(2); d.evidence.Add("a"); d.evidence.Add("b"); d.evidenceActs = null;
            var p = new ProfileData();
            Check(SaveRules.Migrate(d, p), "reports a change");
            Check(p.playerName == "Asha" && p.sessions == 7 && p.endingsSeen.Contains(2), "profile has the player's fields");
            Check(p.highestActCompleted == 2, "unlocks carried over");
            Check(d.version == SaveData.CurrentVersion && d.playerName == "" && d.endingsSeen.Count == 0, "campaign no longer holds player fields");
            Check(d.evidenceActs.Count == 2 && d.evidenceActs.TrueForAll(a => a == 2), "legacy evidence tagged as Act II");
            Check(d.currentAct == 3, "checkpoint is the next act");
        });

        Test("migration does not overwrite a newer profile", () =>
        {
            var d = new SaveData { version = 1, playerName = "Old" };
            var p = new ProfileData { playerName = "New" };
            SaveRules.Migrate(d, p);
            Check(p.playerName == "New", "profile name kept");
        });

        Test("validation repairs a damaged file", () =>
        {
            var d = new SaveData { clues = null, actsCompleted = 9, act3Stage = -4, branch = 7, act5Stage = 3 };
            d.evidence = new List<string> { "x", "x", "", "y" };
            d.evidenceActs = new List<int> { 3 };
            Check(!SaveRules.Validate(d), "reports the repair");
            Check(d.clues != null && d.actsCompleted == 5 && d.act3Stage == 0 && d.branch == 2 && d.act5Stage == 1, "ranges clamped");
            Check(d.evidence.Count == 2 && d.evidenceActs.Count == 2, "duplicates and blanks removed, tags aligned");
            Check(d.evidenceActs[0] == 3 && d.evidenceActs[1] == 2, "known tag kept, missing tag defaults to Act II");
        });

        Test("a clean campaign validates unchanged", () =>
        {
            var d = Campaign();
            Check(SaveRules.Validate(d), "no repairs needed");
            Check(d.evidence.Count == 3, "evidence intact");
        });

        Test("restarting Act III clears III-V state and their evidence, keeps I-II", () =>
        {
            var d = Campaign();
            SaveRules.ResetFrom(d, 3);
            Check(d.clues.Count == 2 && d.hasUsb && d.vikramDone, "Acts I-II kept");
            Check(d.evidence.Count == 1 && d.evidence[0] == "USB", "only Act II evidence left");
            Check(d.act3Stage == 0 && d.panditAnswer == -1 && d.branch == -1, "Act III choices cleared");
            Check(!d.allyRohan && !d.hasEmails, "Act IV allies and documents cleared");
            Check(d.actsCompleted == 2 && d.currentAct == 3, "progress rolled back to the restarted act");
        });

        Test("restarting Act II leaves no downstream choices or evidence", () =>
        {
            var d = Campaign();
            SaveRules.ResetFrom(d, 2);
            Check(d.evidence.Count == 0 && SaveRules.EvidenceCount(d) == 2, "only Act I clues remain");
            Check(d.branch == -1 && !d.allyRohan, "no downstream choices survive");
        });

        Test("evidence is never duplicated", () =>
        {
            var d = new SaveData();
            Check(SaveRules.AddEvidence(d, "Deed copy", 3), "first add");
            Check(!SaveRules.AddEvidence(d, "Deed copy", 4), "second add refused");
            Check(d.evidence.Count == 1 && d.evidenceActs[0] == 3, "one note, first act kept");
        });

        Test("completing acts moves the checkpoint and the unlock forward", () =>
        {
            var d = new SaveData(); var p = new ProfileData();
            SaveRules.CompleteAct(d, p, 1);
            Check(d.actsCompleted == 1 && d.currentAct == 2 && p.highestActCompleted == 1, "after Act I");
            SaveRules.ResetFrom(d, 1);
            Check(p.highestActCompleted == 1, "restarting the campaign keeps chapter unlocks");
        });

        Test("endings are recorded once and only valid ids", () =>
        {
            var p = new ProfileData();
            SaveRules.RecordEnding(p, 0); SaveRules.RecordEnding(p, 0); SaveRules.RecordEnding(p, 5);
            Check(p.endingsSeen.Count == 1 && p.endingsSeen[0] == 0, "one Justice entry, junk ignored");
        });

        Test("resume points (Continue)", () =>
        {
            var d = new SaveData();
            Check(!SaveRules.InProgress(d, 1), "fresh campaign: Act I not in progress");
            d.clues.Add("Laptop");
            Check(SaveRules.InProgress(d, 1), "a clue found: resume Act I");
            d = new SaveData { actsCompleted = 2, act3Stage = 2 };
            Check(SaveRules.InProgress(d, 3), "Act III mid-act checkpoint");
            d = new SaveData { actsCompleted = 3 };
            Check(!SaveRules.InProgress(d, 4), "Act IV restarts at its start");
            d = new SaveData { actsCompleted = 4, act5Stage = 1 };
            Check(SaveRules.InProgress(d, 5), "Act V resumes at the Gallery");
            d = new SaveData { actsCompleted = 4, act3Stage = 3 };
            Check(!SaveRules.InProgress(d, 3), "an old act's stage does not resume a later campaign");
        });

        Test("profile validation", () =>
        {
            var p = new ProfileData { endingsSeen = new List<int> { 1, 1, 9, -1 }, chapterStarts = null, highestActCompleted = 8 };
            Check(!SaveRules.Validate(p), "reports the repair");
            Check(p.endingsSeen.Count == 1 && p.chapterStarts != null && p.highestActCompleted == 5, "repaired");
        });

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "ALL PASSED  (" + run + " checks)" : failed + " FAILED of " + run + " checks");
        return failed == 0 ? 0 : 1;
    }
}

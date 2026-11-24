using System.Collections.Generic;

/// <summary>
/// The case file's wording for an evidence note. A saved note is its own id (saves, the Justice count and the
/// "already noted" checks all compare the exact string), so a note is never rewritten in a save; when its wording
/// changes, the old string stays the id and the case file shows the new words.
/// </summary>
public static class EvidenceText
{
    static readonly Dictionary<string, string> shown = new Dictionary<string, string>
    {
        // Act III: Harishankar is a neighbourhood man who volunteers at the temple, not its priest
        { "Panditji: the hearing is tomorrow at 10 AM - Priya wants the documents there.", "Harishankar: the hearing is tomorrow at 10 AM - Priya wants the documents there." },
        { "Panditji: Priya is safe at the NGO shelter in the old school by the river.", "Harishankar: Priya is safe at the NGO shelter in the old school by the river." },
    };

    public static string Shown(string id)
    {
        string s;
        return id != null && shown.TryGetValue(id, out s) ? s : id;
    }
}

using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;

namespace Veronica;

// Every new session reads the skill and wiki files again, so edits on disk reach the next chat or client.
internal static class SkillShelf
{
    public static SessionState Load()
    {
        var load = SkillFiles.Load(Install.SkillsDir);
        foreach (var problem in load.Problems)
            Log.Default.WriteLine($"[Veronica] Skill skipped: {problem}");

        var wiki = WikiFiles.Load(Install.WikiDir);
        foreach (var problem in wiki.Problems)
            Log.Default.WriteLine($"[Veronica] Wiki page skipped: {problem}");

        return new SessionState(load.Skills, wiki.Pages);
    }
}

using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Features;
using Keen.VRage.ContentPipeline.Project.Features.AssetProcessing;
using Keen.VRage.ContentPipeline.Project.Utils;
using Keen.VRage.ContentPipeline.Project.Features.AssetValidation;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Structure.Interfaces.Validation;
using Keen.VRage.Library.Serialization;
using Veronica.Tools;

namespace Veronica;

// The Editor's validation issues for an eval run's grading; never offered to the model, as valid mods carry many
// benign warnings.
internal static class EvalValidation
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);

    // Issues are read once processing and content pushes have been idle this long: the validator applies a push's
    // issues after it ends, throttled.
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    public static async Task<string> JsonAsync(TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + maxWait;
        DateTime? idleSince = null;
        bool idle = false;
        while (true)
        {
            bool now = await EditorProject.RunAsync(IsIdle, cancellationToken).ConfigureAwait(false);
            if (!now)
                idleSince = null;
            else
                idleSince ??= DateTime.UtcNow;
            if (idleSince is { } since && DateTime.UtcNow - since >= Settle)
            {
                idle = true;
                break;
            }
            if (DateTime.UtcNow >= deadline)
                break;
            await Task.Delay(Poll, cancellationToken).ConfigureAwait(false);
        }
        var issues = await EditorProject.RunAsync(Collect, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(new
        {
            idle,
            errors = issues.Count(i => i.severity == "Error"),
            warnings = issues.Count(i => i.severity == "Warning"),
            mod_errors = issues.Count(i => i.severity == "Error" && i.mod),
            other_errors = issues.Count(i => i.severity == "Error" && !i.mod),
            issues,
        });
    }

    private sealed record Issue(string severity, bool mod, string project, string item, string field, string message);

    private static bool IsIdle(LoadedProject root)
    {
        foreach (var project in root.EnumerateProjectDAG())
        {
            if (!project.TryGetProjectFeature<AssetsProcessingFeature>(out var feature))
                continue;
            if (feature.ProcessingQueue.AssetProcessingsInProgress > 0)
                return false;
            var push = feature.ContentManager.ContentPushStatus.Value;
            if (push.ActivePush != null || push.PendingPushes.Count > 0)
                return false;
        }
        return true;
    }

    private static List<Issue> Collect(LoadedProject root)
    {
        var issues = new List<Issue>();
        var seen = new HashSet<(Guid, string, string)>();
        var projects = root.EnumerateProjectDAG().ToList();

        // The validator's controllers cover the writable projects' items, processing issues included.
        foreach (var project in projects)
        {
            if (!project.TryGetProjectFeature<AssetValidationFeature>(out var feature) || feature.Validator is not { } validator)
                continue;
            foreach (var entry in validator.ValidationControllers)
            {
                var (guid, controller) = (entry.Key, entry.Value);
                foreach (var issue in controller.ValidationIssues.Value)
                {
                    if (issue.Severity == ValidationIssue.IssueSeverity.Info)
                        continue;
                    string field = issue is MemberValidationIssue member ? member.LongName : "";
                    if (seen.Add((guid, field, issue.Message)))
                        issues.Add(Make(projects, guid, issue.Severity.ToString(), field, issue.Message));
                }
            }
        }

        // The content pipeline validates every loaded item, read-only dependencies such as vanilla included.
        var contentManagers = projects
            .Select(p => p.TryGetProjectFeature<AssetsProcessingFeature>(out var f) ? f.ContentManager : null)
            .OfType<Keen.VRage.ContentPipeline.Project.Features.AssetProcessing.ContentManager.IContentManager>()
            .Distinct();
        foreach (var manager in contentManagers)
        {
            foreach (var entry in manager.GetValidationIssues())
            {
                var (guid, list) = (entry.Key, entry.Value);
                foreach (var issue in list)
                {
                    if (issue.Severity == Keen.VRage.Library.Content.ValidationSeverity.Info)
                        continue;
                    string field = FieldOf(issue.Member);
                    if (seen.Add((guid, field, issue.Message)))
                        issues.Add(Make(projects, guid, issue.Severity.ToString(), field, issue.Message));
                }
            }
        }
        return issues.OrderBy(i => i.mod ? 0 : 1).ThenByDescending(i => i.severity).ThenBy(i => i.item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string FieldOf(MemberReference member)
    {
        try
        {
            return member.GetMemberPath().ToString();
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static Issue Make(List<LoadedProject> projects, Guid guid, string severity, string field, string message)
    {
        foreach (var project in projects)
        {
            if (project.GetAssetFileByGuidInProject(guid) is { } file)
                return new Issue(severity, !project.IsReadOnly, project.Name, Content.Shown(file), field, message);
        }
        return new Issue(severity, false, "", guid.ToString(), field, message);
    }
}

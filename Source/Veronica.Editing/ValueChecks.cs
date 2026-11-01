using System.Globalization;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.Validation;
using Keen.VRage.Library.Utils;

namespace Veronica.Editing;

// The write is never refused: two fields can bound each other, and refusing either would deadlock.
public static class ValueChecks
{
    // The value is already written, so a check that fails in any way gives no hint rather than fail the write.
    public static string? Hint(MemberReference member, IDefinitionLocatorStrategy? definitions)
    {
        try
        {
            var checks = new List<(ValidationAttribute Attribute, MemberReference Member)>();
            for (var current = member; ; current = current.GetParent())
            {
                checks.AddRange(current.GetMemberAttributes<ValidationAttribute>().Where(Checked).Select(a => (a, current)));
                // A field of a struct is checked with the struct too: a unit vector is written one axis at a time.
                if (current.IsRoot || current.IsCollectionItem || current.GetParent().IsRoot || !current.GetParent().CurrentMemberType.IsValueType)
                    break;
            }
            if (checks.Count == 0)
                return null;

            var listener = new Collector();
            var services = new ServiceLocatorContext.Builder();
            if (definitions != null)
                services = services.Add<IDefinitionLocatorStrategy>(definitions);
            var context = new ValidationContext(listener, services: services);
            foreach (var (attribute, checkedMember) in checks)
                Run(attribute, context, checkedMember, listener);

            return listener.Messages.Count == 0 ? null : string.Join(" ", listener.Messages.Distinct());
        }
        catch (Exception)
        {
            return null;
        }
    }

    // TimeRange is left out: it reports ticks, which the model cannot match to the TimeSpan read shows.
    private static bool Checked(ValidationAttribute attribute) =>
        attribute is UnitVectorAttribute || (RangeBase(attribute.GetType()) != null && attribute is not TimeRangeAttribute);

    private static Type? RangeBase(Type? type)
    {
        for (; type != null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseRangeAttribute<,>))
                return type;
        return null;
    }

    private static void Run(ValidationAttribute attribute, ValidationContext context, MemberReference member, Collector listener)
    {
        int before = listener.Messages.Count;
        try
        {
            var task = ((IValidation)attribute).ValidateAsync(context, member);
            // The checks used here complete at once; one that would wait is skipped rather than block the UI thread.
            if (task.IsCompleted)
            {
                task.GetAwaiter().GetResult();
            }
            else
            {
                listener.Messages.RemoveRange(before, listener.Messages.Count - before);
                return;
            }
        }
        catch (Exception)
        {
            // A check that needs what this write has not got, such as a definition outside a block, says nothing.
            listener.Messages.RemoveRange(before, listener.Messages.Count - before);
            return;
        }

        if (listener.Messages.Count > before && Limits(attribute, context, member) is { } limits)
            listener.Messages[^1] += $" {limits}";
    }

    // A bound named by another field gets that field's current value, so the model knows which value to correct.
    private static string? Limits(ValidationAttribute attribute, ValidationContext context, MemberReference member)
    {
        if (RangeBase(attribute.GetType()) is not { } range)
            return null;
        var parts = new List<string>();
        foreach (var (identifier, getter) in new[] { ("MinIdentifier", "GetMin"), ("MaxIdentifier", "GetMax") })
        {
            if (range.GetProperty(identifier)!.GetValue(attribute) is not string name)
                continue;
            var method = range.GetMethod(getter)!;
            Type contextType = method.GetParameters()[0].ParameterType;
            object? argument = contextType == typeof(MemberReference) ? member
                : contextType == typeof((ValidationContext, MemberReference)) ? (context, member)
                : null;
            if (argument == null)
                continue;
            try
            {
                object value = method.Invoke(attribute, [argument])!;
                string where = OnDefinition(attribute) ? $"{name} on the component's definition" : name;
                parts.Add($"{where} is {(value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value)}.");
            }
            catch (Exception)
            {
            }
        }
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    // The game's DefinitionRange and DefinitionStringLength take their bounds from the component's definition. By name,
    // since this assembly does not reference the game assembly they live in.
    private static bool OnDefinition(ValidationAttribute attribute) =>
        attribute.GetType().Name.StartsWith("Definition", StringComparison.Ordinal);

    private sealed class Collector : ValidationListenerBase
    {
        public List<string> Messages { get; } = [];

        protected override bool ReportInfoInternal(MemberReference member, string message, IValidation validator, bool isRecoverable) => false;

        protected override bool ReportWarningInternal(MemberReference member, string message, IValidation validator, bool isRecoverable)
        {
            Messages.Add(message);
            return false;
        }

        // False tells the validator not to recover, so the written value is never clamped.
        protected override bool ReportErrorInternal(MemberReference member, string message, IValidation validator, bool isRecoverable)
        {
            Messages.Add(message);
            return false;
        }
    }
}

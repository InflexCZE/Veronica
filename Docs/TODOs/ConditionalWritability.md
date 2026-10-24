# Conditional writability

**Exists:** editor tools treat a field as writable when it has a setter, is not `[ReadOnlyInInspector]`, and no read-only struct contains it (`Fields.IsWritable` in `Source/Veronica.Editing/Fields.cs`).

**Missing:** the inspector also locks fields by condition (`WritableInInspectorWhen`), which the tools ignore, so a write can succeed where the inspector would refuse it. Start from how `VRage.UI.Shared` evaluates the condition for a `MemberReference`.

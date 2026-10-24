namespace Veronica.Editing;

// A problem the model can fix. The message is written for it.
public sealed class EditError(string message) : Exception(message);

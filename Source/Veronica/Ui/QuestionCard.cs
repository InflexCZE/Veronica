using Veronica.Agent;
using Veronica.Agent.Chat;

namespace Veronica.Ui;

public sealed record QuestionOption(string Text, UiCommand Pick);

// The open question card, one question at a time (Docs/Tech/EditorUi.md, Question card).
public sealed class QuestionCard : ChatItem
{
    private readonly Action<IReadOnlyList<string?>> _answer;
    private IReadOnlyList<Question> _questions;
    private string?[] _answers;
    private int _index;
    private string _typed = "";

    public QuestionCard(IReadOnlyList<Question> questions, int generation, Action<IReadOnlyList<string?>> answer, Action close)
    {
        _answer = answer;
        _questions = questions;
        _answers = new string?[questions.Count];
        Generation = generation;
        PreviousCommand = new UiCommand(() => Page(-1));
        NextCommand = new UiCommand(() => Page(1));
        CommitCommand = new UiCommand(() => Commit(_typed));
        CloseCommand = new UiCommand(close);
    }

    // The board's card this one shows (QuestionBoard.Snapshot).
    public int Generation { get; }

    public string Question => _questions[_index].Text;

    public IReadOnlyList<QuestionOption> Options =>
        _questions[_index].Options.Select(o => new QuestionOption(o, new UiCommand(() => Commit(o)))).ToList();

    public bool HasOptions => _questions[_index].Options.Count > 0;

    public bool Paging => _questions.Count > 1;

    public string Position => $"{_index + 1} of {_questions.Count}";

    public bool CanGoBack => _index > 0;

    public bool CanGoForward => _index < _questions.Count - 1;

    public string Typed
    {
        get => _typed;
        set
        {
            Set(ref _typed, value);
            Changed(nameof(CommitLabel));
        }
    }

    public string CommitLabel => _typed.Trim().Length == 0 ? "Skip" : CanGoForward ? "Next" : "Answer";

    public UiCommand PreviousCommand { get; }

    public UiCommand NextCommand { get; }

    public UiCommand CommitCommand { get; }

    public UiCommand CloseCommand { get; }

    // Calls of one round merge into the open card: it grows, keeping what was answered.
    public void Update(IReadOnlyList<Question> questions)
    {
        KeepTyped();
        var answers = new string?[questions.Count];
        Array.Copy(_answers, answers, Math.Min(_answers.Length, answers.Length));
        _questions = questions;
        _answers = answers;
        MoveTo(Math.Min(_index, questions.Count - 1));
    }

    private void Page(int step)
    {
        KeepTyped();
        MoveTo(_index + step);
    }

    private void KeepTyped() => _answers[_index] = _typed.Trim().Length == 0 ? null : _typed.Trim();

    private void Commit(string answer)
    {
        _answers[_index] = answer.Trim().Length == 0 ? null : answer.Trim();
        if (CanGoForward)
            MoveTo(_index + 1);
        else
            _answer(_answers);
    }

    private void MoveTo(int index)
    {
        if (index < 0 || index >= _questions.Count)
            return;
        _index = index;
        Typed = _answers[index] ?? "";
        foreach (var name in new[] { nameof(Question), nameof(Options), nameof(HasOptions), nameof(Paging), nameof(Position), nameof(CanGoBack), nameof(CanGoForward), nameof(CommitLabel) })
            Changed(name);
    }
}

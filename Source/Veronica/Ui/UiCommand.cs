using System.Windows.Input;

namespace Veronica.Ui;

public sealed class UiCommand(Action<object?> execute) : ICommand
{
    public UiCommand(Action execute)
        : this(_ => execute())
    {
    }

    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}

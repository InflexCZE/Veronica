using Avalonia.Controls;

namespace Veronica.Ui;

public partial class McpView : UserControl
{
    public McpView()
    {
        InitializeComponent();
        DataContext = McpPanelViewModel.Current;
    }
}

using System.Windows;
using System.Windows.Controls;
using WinBgScheduler.App.ViewModels;

namespace WinBgScheduler.App.Views;

public partial class ContentEditorView : UserControl
{
    public ContentEditorView() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && DataContext is ContentEditorViewModel vm)
            vm.AddPaths(paths);
    }
}

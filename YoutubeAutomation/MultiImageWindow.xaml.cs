using System.Windows;
using YoutubeAutomation.ViewModels;

namespace YoutubeAutomation;

public partial class MultiImageWindow : Window
{
    public MultiImageWindow()
    {
        InitializeComponent();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MultiImageViewModel vm)
        {
            vm.OnWindowClosing();
        }
    }
}

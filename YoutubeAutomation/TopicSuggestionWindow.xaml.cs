using System;
using System.Windows;
using YoutubeAutomation.ViewModels;

namespace YoutubeAutomation;

public partial class TopicSuggestionWindow : Window
{
    public TopicSuggestionWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TopicSuggestionViewModel vm)
        {
            try
            {
                await vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาดในการโหลดข้อมูล: {ex.Message}",
                                "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

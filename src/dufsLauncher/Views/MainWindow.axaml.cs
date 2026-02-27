using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using dufsLauncher.ViewModels;

namespace dufsLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.BrowseFolderInteraction = BrowseFolderAsync;
            vm.ShowErrorInteraction = ShowErrorAsync;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && change.NewValue is WindowState.Minimized)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Hide();
                WindowState = WindowState.Normal;
            });
        }
    }

    private async Task<string?> BrowseFolderAsync()
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择共享文件夹",
            AllowMultiple = false
        });
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        if (!IsVisible)
        {
            Show();
            Activate();
        }

        var okButton = new Button
        {
            Content = "确定",
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(32, 8),
        };

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new Border
            {
                Padding = new Thickness(24, 20),
                Child = new StackPanel
                {
                    Spacing = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 14,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        okButton
                    }
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}

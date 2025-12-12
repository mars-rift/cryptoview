using System.Windows;
using System.Windows.Controls;
using cryptoview.Services;

namespace cryptoview
{
    public partial class SettingsWindow : Window
    {
        private readonly DataService _dataService = new();
        private CheckBox _saveLastExchangeCheckBox = new();
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Settings";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new Label { Content = "Settings" };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            _saveLastExchangeCheckBox = new CheckBox { Content = "Save last-selected exchange between runs", Margin = new Thickness(10) };
            Grid.SetRow(_saveLastExchangeCheckBox, 1);
            grid.Children.Add(_saveLastExchangeCheckBox);

            // Load initial setting value
            _ = LoadSettingsAsync();

            var button = new Button { Content = "Close", Margin = new Thickness(10) };
            button.Click += (s, e) => Close();
            Grid.SetRow(button, 2);
            grid.Children.Add(button);

            Content = grid;
        }

        private async System.Threading.Tasks.Task LoadSettingsAsync()
        {
            try
            {
                var val = await _dataService.GetSettingAsync("SaveLastSelectedExchange");
                bool enabled = false;
                if (!string.IsNullOrEmpty(val) && bool.TryParse(val, out bool parsed))
                {
                    enabled = parsed;
                }
                _saveLastExchangeCheckBox.IsChecked = enabled;

                _saveLastExchangeCheckBox.Checked += async (s, e) =>
                {
                    await _dataService.SaveSettingAsync("SaveLastSelectedExchange", "true");
                    // If owner is set and has a current selection, save it as the last selected exchange
                    if (Owner is MainWindow main)
                    {
                        var current = main.GetCurrentSelectedExchangeName();
                        if (!string.IsNullOrEmpty(current))
                        {
                            await _dataService.SaveSettingAsync("LastSelectedExchange", current);
                        }
                    }
                };
                _saveLastExchangeCheckBox.Unchecked += async (s, e) =>
                {
                    await _dataService.SaveSettingAsync("SaveLastSelectedExchange", "false");
                    // Optionally remove the stored last selected exchange
                    await _dataService.DeleteSettingAsync("LastSelectedExchange");
                };
            }
            catch { }
        }
    }
}

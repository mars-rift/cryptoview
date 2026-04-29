using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using cryptoview.Services;

namespace cryptoview
{
    public partial class SettingsWindow : Window
    {
        private readonly DataService _dataService = new();
        private CheckBox _saveLastExchangeCheckBox = new();
        private CheckBox _usePrimaryExchangeCheckBox = new();
        private ComboBox _primaryExchangeComboBox = new();

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Settings";
            Width = 480;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleLabel = new Label
            {
                Content = "Settings",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(titleLabel, 0);
            grid.Children.Add(titleLabel);

            _saveLastExchangeCheckBox = new CheckBox
            {
                Content = "Save last-selected exchange between runs",
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_saveLastExchangeCheckBox, 1);
            grid.Children.Add(_saveLastExchangeCheckBox);

            _usePrimaryExchangeCheckBox = new CheckBox
            {
                Content = "Use primary exchange for favorites",
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_usePrimaryExchangeCheckBox, 2);
            grid.Children.Add(_usePrimaryExchangeCheckBox);

            var primaryLabel = new Label
            {
                Content = "Primary Exchange:",
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(primaryLabel, 3);
            grid.Children.Add(primaryLabel);

            _primaryExchangeComboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_primaryExchangeComboBox, 4);
            grid.Children.Add(_primaryExchangeComboBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            var closeButton = new Button { Content = "Close", Width = 100, Margin = new Thickness(0, 10, 0, 0) };
            closeButton.Click += (s, e) => Close();
            buttonPanel.Children.Add(closeButton);
            Grid.SetRow(buttonPanel, 5);
            grid.Children.Add(buttonPanel);

            Content = grid;
            Loaded += SettingsWindow_Loaded;
        }

        private async void SettingsWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadSettingsAsync();
        }

        private async System.Threading.Tasks.Task LoadSettingsAsync()
        {
            try
            {
                var saveLastValue = await _dataService.GetSettingAsync("SaveLastSelectedExchange");
                bool saveLastEnabled = false;
                if (!string.IsNullOrEmpty(saveLastValue) && bool.TryParse(saveLastValue, out var parsedSaveLast))
                {
                    saveLastEnabled = parsedSaveLast;
                }
                _saveLastExchangeCheckBox.IsChecked = saveLastEnabled;

                var usePrimaryValue = await _dataService.GetSettingAsync("UsePrimaryExchangeForFavorites");
                bool usePrimaryEnabled = false;
                if (!string.IsNullOrEmpty(usePrimaryValue) && bool.TryParse(usePrimaryValue, out var parsedUsePrimary))
                {
                    usePrimaryEnabled = parsedUsePrimary;
                }
                _usePrimaryExchangeCheckBox.IsChecked = usePrimaryEnabled;

                var exchangeNames = GetAvailableExchangeNames();
                foreach (var exchangeName in exchangeNames)
                {
                    _primaryExchangeComboBox.Items.Add(exchangeName);
                }

                var primaryExchangeId = await _dataService.GetSettingAsync("PrimaryExchangeForFavorites");
                if (!string.IsNullOrEmpty(primaryExchangeId) && Owner is MainWindow mainWindow)
                {
                    var exchangeName = mainWindow.GetExchangeNameById(primaryExchangeId);
                    if (!string.IsNullOrEmpty(exchangeName) && _primaryExchangeComboBox.Items.Contains(exchangeName))
                    {
                        _primaryExchangeComboBox.SelectedItem = exchangeName;
                    }
                }

                _saveLastExchangeCheckBox.Checked += async (s, e) =>
                {
                    await _dataService.SaveSettingAsync("SaveLastSelectedExchange", "true");
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
                    await _dataService.DeleteSettingAsync("LastSelectedExchange");
                };

                _usePrimaryExchangeCheckBox.Checked += async (s, e) => await _dataService.SetUsePrimaryExchangeForFavoritesAsync(true);
                _usePrimaryExchangeCheckBox.Unchecked += async (s, e) => await _dataService.SetUsePrimaryExchangeForFavoritesAsync(false);

                _primaryExchangeComboBox.SelectionChanged += async (s, e) =>
                {
                    if (_primaryExchangeComboBox.SelectedItem is string selectedName && Owner is MainWindow parentWindow)
                    {
                        var exchangeId = parentWindow.GetExchangeIdByName(selectedName);
                        if (!string.IsNullOrEmpty(exchangeId))
                        {
                            await _dataService.SetPrimaryExchangeForFavoritesAsync(exchangeId);
                        }
                    }
                };
            }
            catch
            {
            }
        }

        private IEnumerable<string> GetAvailableExchangeNames()
        {
            if (Owner is MainWindow parent)
                return parent.GetAvailableExchangeNames();

            return new List<string>();
        }
    }
}

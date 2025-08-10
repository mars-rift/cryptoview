using System.Windows;
using System.Windows.Controls;

namespace cryptoview
{
    public partial class SettingsWindow : Window
    {
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

            var label = new Label { Content = "Settings panel will be implemented in future updates." };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var button = new Button { Content = "Close", Margin = new Thickness(10) };
            button.Click += (s, e) => Close();
            Grid.SetRow(button, 1);
            grid.Children.Add(button);

            Content = grid;
        }
    }
}

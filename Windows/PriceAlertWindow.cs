using System.Windows;
using System.Windows.Controls;
using cryptoview.Models;

namespace cryptoview
{
    public partial class PriceAlertWindow : Window
    {
        private PriceAlert? _alert;

        public PriceAlertWindow(string symbol)
        {
            InitializeComponent();
            Symbol = symbol;
        }

        public string Symbol { get; set; }

        public PriceAlert? GetAlert()
        {
            return _alert;
        }

        private void InitializeComponent()
        {
            // Simple implementation
            Title = "Price Alert";
            Width = 300;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new Label { Content = $"Set price alert for {Symbol}" };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var button = new Button { Content = "OK", Margin = new Thickness(10) };
            button.Click += (s, e) => { DialogResult = true; Close(); };
            Grid.SetRow(button, 2);
            grid.Children.Add(button);

            Content = grid;
        }
    }
}

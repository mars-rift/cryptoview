using System;
using System.Windows;
using cryptoview.Models;

namespace cryptoview.Windows
{
    public partial class PriceAlertDialog : Window
    {
        public decimal TargetPrice { get; private set; }
        public AlertType AlertType { get; private set; }
        
        private readonly decimal _currentPrice;

        public PriceAlertDialog(string symbol, decimal currentPrice)
        {
            InitializeComponent();
            
            _currentPrice = currentPrice;
            
            SymbolTextBlock.Text = $"Symbol: {symbol}";
            CurrentPriceTextBlock.Text = $"Current Price: ${currentPrice:N2}";
            TargetPriceTextBox.Text = currentPrice.ToString("F2");
            
            // Focus on the target price textbox
            TargetPriceTextBox.Focus();
            TargetPriceTextBox.SelectAll();
        }

        private void QuickAbove5_Click(object sender, RoutedEventArgs e)
        {
            AboveRadioButton.IsChecked = true;
            TargetPriceTextBox.Text = (_currentPrice * 1.05m).ToString("F2");
        }

        private void QuickAbove10_Click(object sender, RoutedEventArgs e)
        {
            AboveRadioButton.IsChecked = true;
            TargetPriceTextBox.Text = (_currentPrice * 1.10m).ToString("F2");
        }

        private void QuickBelow5_Click(object sender, RoutedEventArgs e)
        {
            BelowRadioButton.IsChecked = true;
            TargetPriceTextBox.Text = (_currentPrice * 0.95m).ToString("F2");
        }

        private void QuickBelow10_Click(object sender, RoutedEventArgs e)
        {
            BelowRadioButton.IsChecked = true;
            TargetPriceTextBox.Text = (_currentPrice * 0.90m).ToString("F2");
        }

        private void CreateAlert_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TargetPriceTextBox.Text, out decimal targetPrice))
            {
                MessageBox.Show("Please enter a valid price", "Invalid Price", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (targetPrice <= 0)
            {
                MessageBox.Show("Price must be greater than zero", "Invalid Price", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TargetPrice = targetPrice;
            AlertType = AboveRadioButton.IsChecked == true ? AlertType.Above : AlertType.Below;
            
            // Validate the alert makes sense
            if (AlertType == AlertType.Above && targetPrice <= _currentPrice)
            {
                var result = MessageBox.Show(
                    $"You've set an 'Above' alert for ${targetPrice:N2}, but the current price is ${_currentPrice:N2}.\n\nThis alert will trigger immediately. Continue?",
                    "Alert Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            else if (AlertType == AlertType.Below && targetPrice >= _currentPrice)
            {
                var result = MessageBox.Show(
                    $"You've set a 'Below' alert for ${targetPrice:N2}, but the current price is ${_currentPrice:N2}.\n\nThis alert will trigger immediately. Continue?",
                    "Alert Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

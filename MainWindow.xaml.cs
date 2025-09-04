using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using cryptoview.Models;
using cryptoview.Services;
using cryptoview.Windows;
using Microsoft.Win32;

namespace cryptoview
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly Dictionary<string, string> _exchangeMap = new();
        
        // Enhanced features
        private readonly DataService _dataService = new();
        private readonly ObservableCollection<TradingPair> _allPairs = new();
        private readonly ObservableCollection<TradingPair> _filteredPairs = new();
        private readonly ObservableCollection<PriceAlert> _priceAlerts = new();
        private readonly ObservableCollection<TradingPair> _favoritePairs = new();
        private readonly System.Timers.Timer _refreshTimer = new();
        private readonly System.Timers.Timer _priceAlertTimer = new();
        private CollectionViewSource? _pairsViewSource;
        private List<string> _favoriteSymbols = new();
        private string _currentSearchText = "";
        private bool _showFavoritesOnly = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            
            // Initialize price alert monitoring timer (check every 30 seconds)
            _priceAlertTimer.Interval = 30000; // 30 seconds
            _priceAlertTimer.Elapsed += PriceAlertTimer_Elapsed;
            _priceAlertTimer.Start();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Initializing...";
            LoadExchangeButton.IsEnabled = false;
            
            // Initialize data collections and view source
            InitializeDataBindings();
            
            // Load saved data from database
            await LoadDataFromService();
            
            // Default to Binance (exchange ID 2) for fast startup
            await LoadDefaultExchange();
            
            LoadExchangeButton.IsEnabled = true;
            StatusTextBlock.Text = "Ready - Default exchange loaded. Use 'Add Exchanges' to add more options.";
        }

        private async Task LoadDefaultExchange()
        {
            try
            {
                StatusTextBlock.Text = "Loading default exchange...";
                
                // Try multiple popular exchanges until one works
                var defaultExchanges = new[] 
                {
                    new { Name = "Binance", Id = "2" },
                    new { Name = "Coinbase Pro", Id = "37" },
                    new { Name = "Kraken", Id = "29" },
                    new { Name = "Huobi Global", Id = "102" },
                    new { Name = "KuCoin", Id = "311" }
                };

                _exchangeMap.Clear();
                ExchangesComboBox.Items.Clear();

                foreach (var exchange in defaultExchanges)
                {
                    StatusTextBlock.Text = $"Trying {exchange.Name}...";
                    
                    // Test if this exchange has valid data
                    bool isValid = await HasValidExchangeData(exchange.Id);
                    
                    if (isValid)
                    {
                        _exchangeMap.Add(exchange.Name, exchange.Id);
                        ExchangesComboBox.Items.Add(exchange.Name);
                        ExchangesComboBox.SelectedIndex = 0;
                        
                        StatusTextBlock.Text = $"Loading {exchange.Name} data...";
                        await LoadExchangeDataAsync(exchange.Id);
                        
                        StatusTextBlock.Text = $"Ready - {exchange.Name} loaded. Use 'Add Exchanges' to see more options.";
                        return;
                    }
                }
                
                // If no default exchanges work, show error
                StatusTextBlock.Text = "Unable to load default exchange. Click 'Add Exchanges' to load all available exchanges.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading default exchange: {ex.Message}. Click 'Add Exchanges' to try all exchanges.";
            }
        }

        private void InitializeDataBindings()
        {
            // Initialize view source for data binding
            _pairsViewSource = new CollectionViewSource { Source = _filteredPairs };
            _pairsViewSource.Filter += PairsViewSource_Filter;
            
            // Bind DataGrids to collections
            PairsDataGrid.ItemsSource = _pairsViewSource.View;
            AlertsDataGrid.ItemsSource = _priceAlerts;
            FavoritesDataGrid.ItemsSource = _favoritePairs;
        }

        private void PairsViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is TradingPair pair)
            {
                bool matchesSearch = string.IsNullOrEmpty(_currentSearchText) ||
                    pair.Base?.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) == true ||
                    pair.Quote?.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) == true;

                bool matchesFavorites = !_showFavoritesOnly || _favoriteSymbols.Contains($"{pair.Base}/{pair.Quote}");

                e.Accepted = matchesSearch && matchesFavorites;
            }
        }

        private async Task LoadDataFromService()
        {
            try
            {
                // Load favorites from database
                _favoriteSymbols = await _dataService.GetFavoriteSymbolsAsync();
                
                // Load alerts from database  
                var alerts = await _dataService.GetPriceAlertsAsync();
                _priceAlerts.Clear();
                foreach (var alert in alerts)
                {
                    _priceAlerts.Add(alert);
                }

                // Refresh favorites tab to show all saved favorites
                await RefreshFavoritesTab();

                StatusTextBlock.Text = $"Loaded {_favoriteSymbols.Count} favorites and {alerts.Count} alerts";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading saved data: {ex.Message}";
            }
        }

        private async Task LoadExchangeDataAsync(string exchangeId)
        {
            try
            {
                StatusTextBlock.Text = "Loading exchange data...";
                string url = $"https://api.coinlore.net/api/exchange/?id={exchangeId}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}" || json.Trim() == "[]")
                    {
                        StatusTextBlock.Text = $"Exchange returned empty data";
                        return;
                    }

                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        var root = document.RootElement;

                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            StatusTextBlock.Text = "Using array format parsing...";
                            await ParseExchangeDataArray(exchangeId, json);
                            return;
                        }

                        try
                        {
                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                AllowTrailingCommas = true,
                                ReadCommentHandling = JsonCommentHandling.Skip
                            };

                            bool hasPairsProperty = root.TryGetProperty("pairs", out _);
                            bool hasInfoProperty = root.TryGetProperty("0", out _);

                            if (!hasPairsProperty || !hasInfoProperty)
                            {
                                StatusTextBlock.Text = $"Exchange data format is not supported";
                                return;
                            }

                            var exchangeData = JsonSerializer.Deserialize<ExchangeData>(json, options);

                            if (exchangeData != null && exchangeData.Pairs != null && exchangeData.Pairs.Count > 0)
                            {
                                if (exchangeData.Info != null && exchangeData.Info.TryGetValue("0", out var info) && info != null)
                                {
                                    ExchangeInfoTextBlock.Text = $"{info.Name} | Founded: {info.DateLive} | URL: {info.Url}";
                                }
                                else
                                {
                                    ExchangeInfoTextBlock.Text = "Exchange information not available";
                                }

                                ProcessPairsWithCurrentTime(exchangeData.Pairs);
                                await UpdateDataCollections(exchangeData.Pairs);
                                StatusTextBlock.Text = $"Loaded {exchangeData.Pairs.Count} pairs";
                            }
                            else
                            {
                                StatusTextBlock.Text = "No trading pairs available for this exchange";
                            }
                        }
                        catch (JsonException)
                        {
                            StatusTextBlock.Text = "Using alternative parsing for this exchange...";
                            await ParseExchangeDataAlternative(exchangeId, json);
                        }
                        catch (InvalidOperationException)
                        {
                            StatusTextBlock.Text = "Using alternative parsing for this exchange...";
                            await ParseExchangeDataAlternative(exchangeId, json);
                        }
                    }
                }
                else
                {
                    StatusTextBlock.Text = $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading exchange data: {ex.Message}";
                MessageBox.Show($"Error: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private Task ParseExchangeDataArray(string exchangeId, string json)
        {
            try
            {
                var pairs = new List<TradingPair>();

                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    var root = document.RootElement;

                    foreach (JsonElement pairElement in root.EnumerateArray())
                    {
                        try
                        {
                            var pair = new TradingPair();

                            if (pairElement.TryGetProperty("base", out JsonElement baseElem))
                                pair.Base = baseElem.GetString() ?? "Unknown";

                            if (pairElement.TryGetProperty("quote", out JsonElement quoteElem))
                                pair.Quote = quoteElem.GetString() ?? "Unknown";

                            if (pairElement.TryGetProperty("price_usd", out JsonElement priceUsdElem))
                            {
                                if (priceUsdElem.ValueKind == JsonValueKind.Number)
                                    pair.PriceUsd = priceUsdElem.GetDecimal();
                                else if (priceUsdElem.ValueKind == JsonValueKind.String &&
                                        decimal.TryParse(priceUsdElem.GetString(), out decimal priceValue))
                                    pair.PriceUsd = priceValue;
                            }

                            if (pairElement.TryGetProperty("volume", out JsonElement volumeElem))
                            {
                                if (volumeElem.ValueKind == JsonValueKind.Number)
                                    pair.Volume = volumeElem.GetDecimal();
                                else if (volumeElem.ValueKind == JsonValueKind.String &&
                                        decimal.TryParse(volumeElem.GetString(), out decimal volumeValue))
                                    pair.Volume = volumeValue;
                            }

                            if (pairElement.TryGetProperty("price", out JsonElement priceElem))
                            {
                                if (priceElem.ValueKind == JsonValueKind.Number)
                                    pair.Price = priceElem.GetDecimal();
                                else if (priceElem.ValueKind == JsonValueKind.String &&
                                        decimal.TryParse(priceElem.GetString(), out decimal price))
                                    pair.Price = price;
                            }

                            if (pairElement.TryGetProperty("time", out JsonElement timeElem))
                            {
                                if (timeElem.ValueKind == JsonValueKind.Number)
                                    pair.Time = timeElem.GetInt64();
                            }

                            if (pair.Time <= 0 || IsTimestampOutdated(pair.Time))
                            {
                                pair.Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            }

                            var dateTime = DateTimeOffset.FromUnixTimeSeconds(pair.Time).DateTime.ToLocalTime();
                            pair.FormattedTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                            pairs.Add(pair);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                if (pairs.Count > 0)
                {
                    ExchangeInfoTextBlock.Text = $"Exchange ID: {exchangeId}";
                    PairsDataGrid.ItemsSource = pairs;
                    StatusTextBlock.Text = $"Loaded {pairs.Count} pairs from array format data";
                }
                else
                {
                    StatusTextBlock.Text = "Could not load any trading pairs from this exchange";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to parse array format: {ex.Message}";
            }
            
            return Task.CompletedTask; // Return a completed task since we're not doing any async work
        }

        private bool IsTimestampOutdated(long timestamp)
        {
            const int MAX_AGE_SECONDS = 3600;
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return (currentTime - timestamp) > MAX_AGE_SECONDS;
        }

        private void ProcessPairsWithCurrentTime(List<TradingPair> pairs)
        {
            DateTime now = DateTime.Now;

            foreach (var pair in pairs)
            {
                if (pair.Time <= 0 || IsTimestampOutdated(pair.Time))
                {
                    pair.Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    pair.FormattedTime = now.ToString("yyyy-MM-dd HH:mm:ss") + " (Current)";
                }
                else
                {
                    try
                    {
                        var dateTime = DateTimeOffset.FromUnixTimeSeconds(pair.Time).DateTime.ToLocalTime();
                        pair.FormattedTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    catch
                    {
                        pair.FormattedTime = now.ToString("yyyy-MM-dd HH:mm:ss") + " (Current)";
                    }
                }
            }
        }

        private async Task UpdateDataCollections(List<TradingPair> pairs)
        {
            // Update main collection
            _allPairs.Clear();
            foreach (var pair in pairs)
            {
                _allPairs.Add(pair);
            }

            // Update filtered collection
            _filteredPairs.Clear();
            foreach (var pair in _allPairs)
            {
                _filteredPairs.Add(pair);
            }

            // Refresh favorites tab if needed
            await RefreshFavoritesTab();

            // Refresh the view
            _pairsViewSource?.View.Refresh();
            
            // Update favorite button states after a short delay to allow DataGrid to render
            _ = Dispatcher.BeginInvoke(new Action(() => UpdateFavoriteButtonStates()), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateFavoriteButtonStates()
        {
            // Find all favorite buttons in the DataGrid and update their display
            if (PairsDataGrid?.Items != null)
            {
                for (int i = 0; i < PairsDataGrid.Items.Count; i++)
                {
                    if (PairsDataGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                    {
                        var favoriteButton = FindVisualChild<Button>(row, btn => btn.Content?.ToString() == "★" || btn.Content?.ToString() == "☆");
                        if (favoriteButton != null && favoriteButton.Tag is string symbol)
                        {
                            favoriteButton.Content = _favoriteSymbols.Contains(symbol) ? "★" : "☆";
                        }
                    }
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result && (predicate == null || predicate(result)))
                {
                    return result;
                }

                var foundChild = FindVisualChild<T>(child, predicate);
                if (foundChild != null)
                {
                    return foundChild;
                }
            }
            return null;
        }

        private Task ParseExchangeDataAlternative(string exchangeId, string json)
        {
            try
            {
                var pairs = new List<TradingPair>();

                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    var root = document.RootElement;

                    if (root.TryGetProperty("pairs", out JsonElement pairsElement) &&
                        pairsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement pairElement in pairsElement.EnumerateArray())
                        {
                            try
                            {
                                var pair = new TradingPair();

                                if (pairElement.TryGetProperty("base", out JsonElement baseElem))
                                    pair.Base = baseElem.GetString() ?? "Unknown";

                                if (pairElement.TryGetProperty("quote", out JsonElement quoteElem))
                                    pair.Quote = quoteElem.GetString() ?? "Unknown";

                                if (pairElement.TryGetProperty("price_usd", out JsonElement priceUsdElem))
                                {
                                    if (priceUsdElem.ValueKind == JsonValueKind.Number)
                                        pair.PriceUsd = priceUsdElem.GetDecimal();
                                    else if (priceUsdElem.ValueKind == JsonValueKind.String &&
                                            decimal.TryParse(priceUsdElem.GetString(), out decimal priceValue))
                                        pair.PriceUsd = priceValue;
                                }

                                if (pairElement.TryGetProperty("volume", out JsonElement volumeElem))
                                {
                                    if (volumeElem.ValueKind == JsonValueKind.Number)
                                        pair.Volume = volumeElem.GetDecimal();
                                    else if (volumeElem.ValueKind == JsonValueKind.String &&
                                            decimal.TryParse(volumeElem.GetString(), out decimal volumeValue))
                                        pair.Volume = volumeValue;
                                }

                                if (pairElement.TryGetProperty("price", out JsonElement priceElem))
                                {
                                    if (priceElem.ValueKind == JsonValueKind.Number)
                                        pair.Price = priceElem.GetDecimal();
                                    else if (priceElem.ValueKind == JsonValueKind.String &&
                                            decimal.TryParse(priceElem.GetString(), out decimal price))
                                        pair.Price = price;
                                }

                                if (pairElement.TryGetProperty("time", out JsonElement timeElem))
                                {
                                    if (timeElem.ValueKind == JsonValueKind.Number)
                                        pair.Time = timeElem.GetInt64();
                                }

                                if (pair.Time <= 0 || IsTimestampOutdated(pair.Time))
                                {
                                    pair.Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    pair.FormattedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (Current)";
                                }
                                else
                                {
                                    var dateTime = DateTimeOffset.FromUnixTimeSeconds(pair.Time).DateTime.ToLocalTime();
                                    pair.FormattedTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                                }

                                pairs.Add(pair);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }

                    string exchangeName = "Unknown";
                    string dateLive = "Unknown";
                    string url = "Unknown";

                    if (root.TryGetProperty("0", out JsonElement infoElement) &&
                        infoElement.ValueKind == JsonValueKind.Object)
                    {
                        if (infoElement.TryGetProperty("name", out JsonElement nameElem))
                            exchangeName = nameElem.GetString() ?? "Unknown";

                        if (infoElement.TryGetProperty("date_live", out JsonElement dateElem))
                            dateLive = dateElem.GetString() ?? "Unknown";

                        if (infoElement.TryGetProperty("url", out JsonElement urlElem))
                            url = urlElem.GetString() ?? "Unknown";
                    }

                    ExchangeInfoTextBlock.Text = $"{exchangeName} | Founded: {dateLive} | URL: {url}";
                }

                if (pairs.Count > 0)
                {
                    PairsDataGrid.ItemsSource = pairs;
                    StatusTextBlock.Text = $"Loaded {pairs.Count} pairs using alternative parsing";
                }
                else
                {
                    StatusTextBlock.Text = "Could not load any trading pairs from this exchange";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"This exchange format is not supported: {ex.Message}";
            }
            
            return Task.CompletedTask;
        }

        private async Task LoadExchangesAsync()
        {
            try
            {
                // Show loading indicators  
                LoadingGrid.Visibility = Visibility.Visible;
                LoadingStatusTextBlock.Text = "Loading exchanges...";
                LoadingProgressBar.Value = 0;
                
                StatusTextBlock.Text = "Loading exchanges...";
                string url = "https://api.coinlore.net/api/exchanges/";
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var exchanges = JsonSerializer.Deserialize<Dictionary<string, Exchange>>(json, options);

                    if (exchanges != null)
                    {
                        // Don't clear existing exchanges - keep the default one loaded
                        var existingExchanges = new HashSet<string>();
                        for (int i = 0; i < ExchangesComboBox.Items.Count; i++)
                        {
                            existingExchanges.Add(ExchangesComboBox.Items[i].ToString() ?? "");
                        }
                        
                        LoadingStatusTextBlock.Text = "Filtering exchanges with valid data...";
                        StatusTextBlock.Text = "Filtering exchanges with valid data...";
                        
                        int totalExchanges = exchanges.Count;
                        int validExchanges = ExchangesComboBox.Items.Count; // Start with existing count
                        int checkedExchanges = 0;
                        
                        // Create a list to hold new exchanges that have valid data
                        var validExchangeNames = new List<string>();

                        foreach (var exchange in exchanges)
                        {
                            if (!string.IsNullOrEmpty(exchange.Value.Name) && !existingExchanges.Contains(exchange.Value.Name))
                            {
                                // Check if this exchange has valid data before adding it
                                bool isValid = await HasValidExchangeData(exchange.Key);
                                
                                if (isValid)
                                {
                                    _exchangeMap.Add(exchange.Value.Name, exchange.Key);
                                    validExchangeNames.Add(exchange.Value.Name);
                                    validExchanges++;
                                }
                                
                                checkedExchanges++;
                                
                                // Update progress
                                double progressPercentage = (double)checkedExchanges / totalExchanges * 100;
                                LoadingProgressBar.Value = progressPercentage;
                                
                                // Update status periodically to show progress
                                if (checkedExchanges % 5 == 0 || checkedExchanges == 1 || checkedExchanges == totalExchanges)
                                {
                                    LoadingStatusTextBlock.Text = $"Filtering exchanges: {validExchanges} valid out of {checkedExchanges} checked...";
                                    StatusTextBlock.Text = $"Filtering exchanges: {validExchanges} valid out of {checkedExchanges} checked...";
                                }
                            }
                        }
                        
                        // Sort new exchange names alphabetically
                        validExchangeNames.Sort();
                        
                        // Add the new valid exchanges to the ComboBox
                        foreach (var name in validExchangeNames)
                        {
                            ExchangesComboBox.Items.Add(name);
                        }

                        if (ExchangesComboBox.Items.Count > 0)
                        {
                            // Keep the current selection if it exists
                            if (ExchangesComboBox.SelectedIndex < 0)
                            {
                                ExchangesComboBox.SelectedIndex = 0;
                            }
                            StatusTextBlock.Text = $"Ready - {validExchanges} exchanges available";
                        }
                        else
                        {
                            StatusTextBlock.Text = "No valid exchanges found";
                        }
                    }
                }
                else
                {
                    StatusTextBlock.Text = $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading exchanges: {ex.Message}";
                MessageBox.Show($"Error: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading indicators
                LoadingGrid.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadExchangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExchangesComboBox.SelectedItem == null)
            {
                StatusTextBlock.Text = "Please select an exchange first";
                return;
            }

            LoadExchangeButton.IsEnabled = false;
            try
            {
                string? exchangeName = ExchangesComboBox.SelectedItem?.ToString();
                if (exchangeName != null && _exchangeMap.TryGetValue(exchangeName, out string? exchangeId))
                {
                    await LoadExchangeDataAsync(exchangeId);
                }
            }
            finally
            {
                LoadExchangeButton.IsEnabled = true;
            }
        }

        private async void LoadMoreExchangesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMoreExchangesButton.IsEnabled = false;
            try
            {
                StatusTextBlock.Text = "Loading additional exchanges...";
                await LoadExchangesAsync();
                StatusTextBlock.Text = $"Loaded {ExchangesComboBox.Items.Count} total exchanges. Select any exchange and click 'LOAD DATA'.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading exchanges: {ex.Message}";
            }
            finally
            {
                LoadMoreExchangesButton.IsEnabled = true;
            }
        }

        private async Task<bool> HasValidExchangeData(string exchangeId)
        {
            try
            {
                // Configure a shorter timeout for this check to keep the filtering process moving
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5); // Shorter timeout for validation
                
                string url = $"https://api.coinlore.net/api/exchange/?id={exchangeId}";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                string json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}" || json.Trim() == "[]")
                {
                    return false;
                }

                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    var root = document.RootElement;

                    // Case 1: If it's an object with a "pairs" property containing a non-empty array, it's valid
                    if (root.ValueKind == JsonValueKind.Object && 
                        root.TryGetProperty("pairs", out JsonElement pairsElement) &&
                        pairsElement.ValueKind == JsonValueKind.Array && 
                        pairsElement.GetArrayLength() > 0)
                    {
                        return true;
                    }

                    // Case 2: If it's an array with more than a few items, it's probably valid
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 3)
                    {
                        // Do a quick check of the first few elements to see if they look like pairs
                        bool hasPairProperties = false;
                        int checkedItems = 0;
                        
                        foreach (JsonElement item in root.EnumerateArray())
                        {
                            if (checkedItems++ >= 3) break; // Only check the first few
                            
                            // Check if it has typical pair properties
                            if ((item.TryGetProperty("base", out _) || 
                                 item.TryGetProperty("symbol", out _)) &&
                                (item.TryGetProperty("quote", out _) || 
                                 item.TryGetProperty("price", out _) || 
                                 item.TryGetProperty("price_usd", out _)))
                            {
                                hasPairProperties = true;
                                break;
                            }
                        }
                        
                        return hasPairProperties;
                    }

                    // No valid data structure found
                    return false;
                }
            }
            catch
            {
                // If there's any error in processing, consider it invalid
                return false;
            }
        }

        // Enhanced event handlers for new features
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentData();
        }

        private async Task RefreshCurrentData()
        {
            if (ExchangesComboBox.SelectedItem != null)
            {
                string? exchangeName = ExchangesComboBox.SelectedItem.ToString();
                if (exchangeName != null && _exchangeMap.TryGetValue(exchangeName, out string? exchangeId))
                {
                    await LoadExchangeDataAsync(exchangeId);
                    LastUpdateTextBlock.Text = $"Last Update: {DateTime.Now:HH:mm:ss}";
                }
            }
        }

        private void AutoRefreshCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Start();
            StatusTextBlock.Text = "Auto-refresh enabled";
        }

        private void AutoRefreshCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
            StatusTextBlock.Text = "Auto-refresh disabled";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = SearchTextBox.Text;
            RefreshFilter();
        }

        private void FavoritesOnlyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _showFavoritesOnly = true;
            RefreshFilter();
        }

        private void FavoritesOnlyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _showFavoritesOnly = false;
            RefreshFilter();
        }

        private void RefreshFilter()
        {
            _pairsViewSource?.View?.Refresh();
        }

        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string symbol)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Favorite button clicked for symbol: {symbol}");
                    
                    if (_favoriteSymbols.Contains(symbol))
                    {
                        _favoriteSymbols.Remove(symbol);
                        await _dataService.RemoveFavoriteAsync(symbol);
                        button.Content = "☆";
                        System.Diagnostics.Debug.WriteLine($"Removed {symbol} from favorites");
                    }
                    else
                    {
                        _favoriteSymbols.Add(symbol);
                        
                        // Find the current pair to get additional details
                        var currentPair = _allPairs.FirstOrDefault(p => $"{p.Base}/{p.Quote}" == symbol);
                        string? currentExchange = "Unknown"; // Will fix control reference later
                        
                        System.Diagnostics.Debug.WriteLine($"Adding {symbol} to favorites. Current pair found: {currentPair != null}");
                        
                        await _dataService.AddFavoriteAsync(
                            symbol, 
                            currentPair?.Base, 
                            currentPair?.Quote, 
                            currentPair?.PriceUsd, 
                            currentExchange);
                        
                        button.Content = "★";
                        System.Diagnostics.Debug.WriteLine($"Added {symbol} to favorites");
                    }
                    
                    // Refresh favorites tab
                    System.Diagnostics.Debug.WriteLine("Refreshing favorites tab...");
                    await RefreshFavoritesTab();
                    RefreshFilter();
                    System.Diagnostics.Debug.WriteLine($"Favorites tab refreshed. Total favorites: {_favoritePairs.Count}");
                }
                catch (Exception ex) when (ex.Message.Contains("no column named"))
                {
                    System.Diagnostics.Debug.WriteLine($"Database schema error: {ex.Message}");
                    // Database schema issue - offer to reset
                    var result = MessageBox.Show(
                        $"Database schema error: {ex.Message}\n\nWould you like to reset the database? This will clear all saved favorites and alerts but fix the issue.",
                        "Database Error", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            await _dataService.ResetDatabaseAsync();
                            _favoriteSymbols.Clear();
                            _priceAlerts.Clear();
                            await RefreshFavoritesTab();
                        }
                        catch (Exception resetEx)
                        {
                            MessageBox.Show($"Failed to reset database: {resetEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in FavoriteButton_Click: {ex.Message}");
                    MessageBox.Show($"Error: {ex.Message}", "Favorite Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"FavoriteButton_Click called but sender is not a button or tag is not a string. Sender: {sender?.GetType()}, Tag: {(sender as Button)?.Tag}");
            }
        }

        private async void AlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string symbol)
            {
                // Find the current pair to get current price
                var currentPair = _allPairs.FirstOrDefault(p => $"{p.Base}/{p.Quote}" == symbol);
                if (currentPair == null)
                {
                    MessageBox.Show("Unable to find current price for alert setup", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // Show a more sophisticated alert creation dialog
                    var alertDialog = new PriceAlertDialog(symbol, currentPair.PriceUsd);
                    if (alertDialog.ShowDialog() == true)
                    {
                        // Check for duplicate alerts
                        var existingAlert = _priceAlerts.FirstOrDefault(a => 
                            a.Symbol == symbol && 
                            a.TargetPrice == alertDialog.TargetPrice && 
                            a.Type == alertDialog.AlertType &&
                            a.IsEnabled);

                        if (existingAlert != null)
                        {
                            MessageBox.Show($"An identical alert already exists for {symbol} at ${alertDialog.TargetPrice:N2} {(alertDialog.AlertType == AlertType.Above ? "above" : "below")}", 
                                          "Duplicate Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var alert = new PriceAlert
                        {
                            Symbol = symbol,
                            TargetPrice = alertDialog.TargetPrice,
                            Type = alertDialog.AlertType,
                            CreatedAt = DateTime.Now,
                            IsEnabled = true,
                            Message = $"Alert for {symbol} when price goes {(alertDialog.AlertType == AlertType.Above ? "above" : "below")} ${alertDialog.TargetPrice:N2}"
                        };

                        await _dataService.SavePriceAlertAsync(alert);
                        _priceAlerts.Add(alert);
                        
                        MessageBox.Show($"Price alert created for {symbol} when price goes {(alertDialog.AlertType == AlertType.Above ? "above" : "below")} ${alertDialog.TargetPrice:N2}", 
                                      "Alert Created", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating alert: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ClearAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _dataService.ClearAllPriceAlertsAsync();
                _priceAlerts.Clear();
                MessageBox.Show("All alerts cleared", "Alerts Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing alerts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PriceAlert alert)
            {
                try
                {
                    await _dataService.DeletePriceAlertAsync(alert);
                    _priceAlerts.Remove(alert);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting alert: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RemoveFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string symbol)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"RemoveFavoriteButton_Click: Removing symbol: '{symbol}'");
                    System.Diagnostics.Debug.WriteLine($"RemoveFavoriteButton_Click: Symbol length: {symbol.Length}");
                    System.Diagnostics.Debug.WriteLine($"RemoveFavoriteButton_Click: Contains in favorites list: {_favoriteSymbols.Contains(symbol)}");
                    
                    _favoriteSymbols.Remove(symbol);
                    await _dataService.RemoveFavoriteAsync(symbol);
                    await RefreshFavoritesTab();
                    
                    System.Diagnostics.Debug.WriteLine($"RemoveFavoriteButton_Click: Successfully removed '{symbol}'");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RemoveFavoriteButton_Click: Error removing '{symbol}': {ex.Message}");
                    MessageBox.Show($"Error removing favorite: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"RemoveFavoriteButton_Click: Invalid sender or tag. Sender type: {sender?.GetType()}, Tag: '{(sender as Button)?.Tag}'");
                MessageBox.Show("Error: Unable to identify which favorite to remove", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshFavoritesTab()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("RefreshFavoritesTab called");
                
                _favoritePairs.Clear();
                
                // Get all detailed favorites from database
                var detailedFavorites = await _dataService.GetDetailedFavoritesAsync();
                System.Diagnostics.Debug.WriteLine($"Got {detailedFavorites.Count} detailed favorites from database");
                
                foreach (var favorite in detailedFavorites)
                {
                    // Update with current price data if available
                    var currentPair = _allPairs.FirstOrDefault(p => $"{p.Base}/{p.Quote}" == favorite.Symbol);
                    if (currentPair != null)
                    {
                        // Update prices with current exchange data
                        favorite.Price = currentPair.Price;
                        favorite.PriceUsd = currentPair.PriceUsd;
                        favorite.Volume = currentPair.Volume;
                    }
                    
                    _favoritePairs.Add(favorite);
                    System.Diagnostics.Debug.WriteLine($"Added favorite to UI: {favorite.Symbol} - Price: ${favorite.PriceUsd:N2}");
                }
                
                // Also add any current pairs that are favorites but might not be in the detailed list yet
                foreach (var pair in _allPairs.Where(p => _favoriteSymbols.Contains($"{p.Base}/{p.Quote}")))
                {
                    // Only add if not already in the favorites list
                    if (!_favoritePairs.Any(f => f.Symbol == $"{pair.Base}/{pair.Quote}"))
                    {
                        _favoritePairs.Add(pair);
                        System.Diagnostics.Debug.WriteLine($"Added current pair to favorites: {pair.Symbol}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"RefreshFavoritesTab completed. Total favorites in UI: {_favoritePairs.Count}");
                
                // Force the UI to update on the dispatcher thread
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Refresh the view to ensure binding updates
                        var view = CollectionViewSource.GetDefaultView(_favoritePairs);
                        view?.Refresh();
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing UI: {uiEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing favorites: {ex.Message}");
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    DefaultExt = "csv",
                    FileName = $"cryptoview_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToCsv(saveFileDialog.FileName);
                    MessageBox.Show($"Data exported to {saveFileDialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToCsv(string fileName)
        {
            using var writer = new StreamWriter(fileName);
            writer.WriteLine("Base,Quote,Price USD,Volume,Time");
            
            // Export current filtered pairs
            foreach (var pair in _filteredPairs)
            {
                writer.WriteLine($"{pair.Base},{pair.Quote},{pair.PriceUsd},{pair.Volume},{pair.FormattedTime}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings panel - Empty", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PairsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Could implement selection-based features here
        }

        private async void PriceAlertTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // Check if we have any active alerts and current pairs data
                if (_priceAlerts.Count == 0 || _allPairs.Count == 0) return;

                var triggeredAlerts = new List<PriceAlert>();

                // Create a copy of the alerts to avoid collection modification during enumeration
                var alertsCopy = _priceAlerts.ToList();

                foreach (var alert in alertsCopy)
                {
                    if (!alert.IsEnabled || alert == null) continue;

                    // Find the current price for this symbol
                    var currentPair = _allPairs.FirstOrDefault(p => $"{p.Base}/{p.Quote}" == alert.Symbol);
                    if (currentPair == null) continue;

                    bool triggered = false;
                    if (alert.Type == AlertType.Above && currentPair.PriceUsd >= alert.TargetPrice)
                    {
                        triggered = true;
                    }
                    else if (alert.Type == AlertType.Below && currentPair.PriceUsd <= alert.TargetPrice)
                    {
                        triggered = true;
                    }

                    if (triggered)
                    {
                        triggeredAlerts.Add(alert);
                        
                        // Delete the alert from database and remove from collection
                        await _dataService.DeletePriceAlertAsync(alert);
                    }
                }

                // Show triggered alerts on the UI thread and remove from collection
                if (triggeredAlerts.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var alert in triggeredAlerts)
                        {
                            var currentPair = _allPairs.FirstOrDefault(p => $"{p.Base}/{p.Quote}" == alert.Symbol);
                            var message = $"PRICE ALERT TRIGGERED!\n\n" +
                                         $"Symbol: {alert.Symbol}\n" +
                                         $"Target: ${alert.TargetPrice:N2} ({(alert.Type == AlertType.Above ? "Above" : "Below")})\n" +
                                         $"Current: ${currentPair?.PriceUsd:N2}\n\n" +
                                         $"Alert has been removed.";
                            
                            MessageBox.Show(message, "Price Alert", MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // Remove from the UI collection
                            _priceAlerts.Remove(alert);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in price alert monitoring: {ex.Message}");
            }
        }

        #region IDisposable Implementation
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _refreshTimer?.Stop();
                    _refreshTimer?.Dispose();
                    _priceAlertTimer?.Stop();
                    _priceAlertTimer?.Dispose();
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        ~MainWindow()
        {
            Dispose(false);
        }
        #endregion
    }
}

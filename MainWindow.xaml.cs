using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace cryptoview
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly Dictionary<string, string> _exchangeMap = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Loading exchanges...";
            LoadExchangeButton.IsEnabled = false; // Disable button during initial loading
              // Configure loading indicator
            LoadingGrid.Visibility = Visibility.Visible;
            LoadingStatusTextBlock.Text = "Loading and filtering exchanges with valid data...";
            LoadingProgressBar.Value = 0;
            
            // No popup message, rely on the status indicators instead
            
            await LoadExchangesAsync();
            
            LoadExchangeButton.IsEnabled = true; // Re-enable button once loading is complete
            LoadingGrid.Visibility = Visibility.Collapsed; // Ensure loading indicator is hidden
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

                                PairsDataGrid.ItemsSource = exchangeData.Pairs;
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
                        _exchangeMap.Clear();
                        ExchangesComboBox.Items.Clear();
                        
                        LoadingStatusTextBlock.Text = "Filtering exchanges with valid data...";
                        StatusTextBlock.Text = "Filtering exchanges with valid data...";
                        
                        int totalExchanges = exchanges.Count;
                        int validExchanges = 0;
                        int checkedExchanges = 0;
                        
                        // Create a list to hold exchanges that have valid data
                        var validExchangeNames = new List<string>();

                        foreach (var exchange in exchanges)
                        {
                            if (!string.IsNullOrEmpty(exchange.Value.Name))
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
                        
                        // Sort exchange names alphabetically for better UX
                        validExchangeNames.Sort();
                        
                        // Add the valid exchanges to the ComboBox
                        foreach (var name in validExchangeNames)
                        {
                            ExchangesComboBox.Items.Add(name);
                        }

                        if (ExchangesComboBox.Items.Count > 0)
                        {
                            ExchangesComboBox.SelectedIndex = 0;
                            StatusTextBlock.Text = $"Ready - {validExchanges} valid exchanges loaded out of {totalExchanges} total";
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
            {                string? exchangeName = ExchangesComboBox.SelectedItem?.ToString();
                if (exchangeName != null && _exchangeMap.TryGetValue(exchangeName, out string exchangeId))
                {
                    await LoadExchangeDataAsync(exchangeId);
                }
            }
            finally
            {
                LoadExchangeButton.IsEnabled = true;
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
    }

    // Data models
    public class Exchange
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
    }

    public class ExchangeInfo
    {
        public string? Name { get; set; }

        [JsonPropertyName("date_live")]
        public string? DateLive { get; set; }

        public string? Url { get; set; }
    }

    public class TradingPair
    {
        [JsonPropertyName("base")]
        public string? Base { get; set; }

        [JsonPropertyName("quote")]
        public string? Quote { get; set; }

        [JsonPropertyName("volume")]
        public decimal Volume { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("price_usd")]
        public decimal PriceUsd { get; set; }

        [JsonPropertyName("time")]
        public long Time { get; set; }

        public string? FormattedTime { get; set; }
    }

    public class ExchangeData
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        private Dictionary<string, ExchangeInfo?>? _infoCache;
        [JsonIgnore]
        public Dictionary<string, ExchangeInfo?> Info
        {
            get
            {
                if (_infoCache != null) return _infoCache;
                _infoCache = new Dictionary<string, ExchangeInfo?>();
                if (ExtensionData != null && ExtensionData.TryGetValue("0", out JsonElement infoElement))
                {
                    var info = JsonSerializer.Deserialize<ExchangeInfo>(infoElement.GetRawText());
                    _infoCache.Add("0", info);
                }
                return _infoCache;
            }
        }

        [JsonPropertyName("pairs")]
        public List<TradingPair> Pairs { get; set; } = new();
    }
}

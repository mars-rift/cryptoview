using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace cryptoview.Models
{
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

    public class TradingPair : INotifyPropertyChanged
    {
        private decimal _priceUsd;
        private decimal _volume;
        private string? _priceChangeIndicator;

        [JsonPropertyName("base")]
        public string? Base { get; set; }

        [JsonPropertyName("quote")]
        public string? Quote { get; set; }

        [JsonPropertyName("volume")]
        public decimal Volume 
        { 
            get => _volume;
            set
            {
                _volume = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("price_usd")]
        public decimal PriceUsd 
        { 
            get => _priceUsd;
            set
            {
                var oldPrice = _priceUsd;
                _priceUsd = value;
                
                // Update price change indicator
                if (oldPrice > 0)
                {
                    if (value > oldPrice)
                        PriceChangeIndicator = "▲";
                    else if (value < oldPrice)
                        PriceChangeIndicator = "▼";
                    else
                        PriceChangeIndicator = "=";
                }
                
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriceChangePercent));
            }
        }

        [JsonPropertyName("time")]
        public long Time { get; set; }

        public string? FormattedTime { get; set; }

        public string? PriceChangeIndicator
        {
            get => _priceChangeIndicator;
            set
            {
                _priceChangeIndicator = value;
                OnPropertyChanged();
            }
        }

        public decimal PriceChangePercent { get; set; }

        public bool IsFavorite { get; set; }

        public string Symbol => $"{Base}/{Quote}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ExchangeData
    {
        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }

        private Dictionary<string, ExchangeInfo?>? _infoCache;
        [JsonIgnore]
        public Dictionary<string, ExchangeInfo?> Info
        {
            get
            {
                if (_infoCache != null) return _infoCache;
                _infoCache = new Dictionary<string, ExchangeInfo?>();
                if (ExtensionData != null && ExtensionData.TryGetValue("0", out var infoElement))
                {
                    var info = System.Text.Json.JsonSerializer.Deserialize<ExchangeInfo>(infoElement.GetRawText());
                    _infoCache.Add("0", info);
                }
                return _infoCache;
            }
        }

        [JsonPropertyName("pairs")]
        public List<TradingPair> Pairs { get; set; } = new();
    }

    public class PriceAlert
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal TargetPrice { get; set; }
        public AlertType Type { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Message { get; set; }
    }

    public enum AlertType
    {
        Above,
        Below
    }

    public class HistoricalPrice
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class UserSettings
    {
        // Refresh interval and auto-refresh settings were removed; use manual refresh via 'LOAD DATA'
        public string Theme { get; set; } = "Cyberpunk";
        public bool SoundAlertsEnabled { get; set; } = true;
        public List<string> FavoriteSymbols { get; set; } = new();
        public bool SaveToDatabase { get; set; } = true;
    }
}

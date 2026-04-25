# CryptoView Code Changes - Detailed Reference

## 1. Database Schema Changes (DataService.cs)

### Added Columns to Favorites Table:
```csharp
// NEW: ExchangeId column - tracks source exchange
if (!existingColumns.Contains("exchangeid"))
{
    using var addExchangeId = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN ExchangeId TEXT", connection);
    addExchangeId.ExecuteNonQuery();
    System.Diagnostics.Debug.WriteLine("Added ExchangeId column to Favorites table");
}

// NEW: PriceTimestamp column - tracks when price was updated
if (!existingColumns.Contains("pricetimestamp"))
{
    using var addPriceTimestamp = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN PriceTimestamp DATETIME", connection);
    addPriceTimestamp.ExecuteNonQuery();
    System.Diagnostics.Debug.WriteLine("Added PriceTimestamp column to Favorites table");
}
```

### Updated Table Recreation Schema:
```csharp
var createNewFavorites = @"
    CREATE TABLE Favorites (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Symbol TEXT NOT NULL UNIQUE,
        Base TEXT,
        Quote TEXT,
        LastPrice DECIMAL,
        LastExchange TEXT,
        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
        ExchangeId TEXT,              -- NEW: Source exchange ID
        PriceTimestamp DATETIME       -- NEW: Last update timestamp
    );
";
```

---

## 2. DataService New Methods

### Get Exchange Info for Favorites:
```csharp
/// <summary>
/// Gets detailed favorite info including the exchange ID it was added from
/// </summary>
public async Task<Dictionary<string, (string? ExchangeId, DateTime? PriceTimestamp)>> GetFavoriteExchangeInfoAsync()
{
    using var connection = new SQLiteConnection(_connectionString);
    await connection.OpenAsync();

    var favoriteExchanges = new Dictionary<string, (string?, DateTime?)>();

    try
    {
        var sql = @"SELECT Symbol, ExchangeId, PriceTimestamp FROM Favorites";
        using var command = new SQLiteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var symbol = reader.GetString(0);
            var exchangeId = reader.IsDBNull(1) ? null : reader.GetString(1);
            var priceTimestamp = reader.IsDBNull(2) ? null : reader.GetDateTime(2) as DateTime?;
            
            favoriteExchanges[symbol] = (exchangeId, priceTimestamp);
        }

        System.Diagnostics.Debug.WriteLine($"Got exchange info for {favoriteExchanges.Count} favorites");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error getting favorite exchange info: {ex.Message}");
    }

    return favoriteExchanges;
}
```

### Primary Exchange Setting Methods:
```csharp
/// <summary>
/// Gets the primary exchange ID for favorite prices
/// </summary>
public async Task<string?> GetPrimaryExchangeForFavoritesAsync()
{
    return await GetSettingAsync("PrimaryExchangeForFavorites");
}

/// <summary>
/// Sets the primary exchange for favorite prices
/// </summary>
public async Task SetPrimaryExchangeForFavoritesAsync(string exchangeId)
{
    await SaveSettingAsync("PrimaryExchangeForFavorites", exchangeId);
    System.Diagnostics.Debug.WriteLine($"Set primary exchange for favorites to: {exchangeId}");
}
```

### Updated AddFavoriteAsync Signature:
```csharp
// BEFORE:
public async Task AddFavoriteAsync(string symbol, string? baseCurrency = null, 
    string? quoteCurrency = null, decimal? lastPrice = null, string? exchange = null)

// AFTER:
public async Task AddFavoriteAsync(string symbol, string? baseCurrency = null, 
    string? quoteCurrency = null, decimal? lastPrice = null, string? exchange = null, 
    string? exchangeId = null)  // NEW PARAMETER
```

---

## 3. MainWindow Changes - Smart Price Update Logic

### Refactored RefreshFavoritesTab Method:
```csharp
private async Task RefreshFavoritesTab()
{
    try
    {
        System.Diagnostics.Debug.WriteLine("RefreshFavoritesTab called");
        
        _favoritePairs.Clear();
        
        // Get current exchange from ComboBox
        string? currentExchangeName = ExchangesComboBox.SelectedItem?.ToString();
        string? currentExchangeId = null;
        if (currentExchangeName != null && _exchangeMap.TryGetValue(currentExchangeName, out var exchangeId))
        {
            currentExchangeId = exchangeId;
        }

        // Get all detailed favorites from database
        var detailedFavorites = await _dataService.GetDetailedFavoritesAsync();
        
        // Get exchange info for each favorite (NEW)
        var favoriteExchangeInfo = await _dataService.GetFavoriteExchangeInfoAsync();
        
        // Get primary exchange setting (NEW)
        var primaryExchangeId = await _dataService.GetPrimaryExchangeForFavoritesAsync();
        
        System.Diagnostics.Debug.WriteLine($"Got {detailedFavorites.Count} detailed favorites. Primary exchange: {primaryExchangeId}, Current exchange: {currentExchangeId}");
        
        foreach (var favorite in detailedFavorites)
        {
            // NEW LOGIC: Check if we should update price from current data
            bool shouldUpdateFromCurrent = false;
            string? storedExchangeId = null;
            DateTime? priceTimestamp = null;
            
            if (favoriteExchangeInfo.TryGetValue(favorite.Symbol, out var exchangeInfo))
            {
                storedExchangeId = exchangeInfo.Item1;
                priceTimestamp = exchangeInfo.Item2;
                
                // Only update if:
                // 1. Current exchange matches the stored exchange, OR
                // 2. Current exchange matches the primary exchange
                if (!string.IsNullOrEmpty(currentExchangeId) && 
                    (currentExchangeId == storedExchangeId || currentExchangeId == primaryExchangeId))
                {
                    shouldUpdateFromCurrent = true;
                }
            }
            else if (!string.IsNullOrEmpty(currentExchangeId) && currentExchangeId == primaryExchangeId)
            {
                // No stored exchange info, but current is primary - update it
                shouldUpdateFromCurrent = true;
            }
            
            // Update with current price data if conditions are met
            if (shouldUpdateFromCurrent)
            {
                var currentPair = _allPairs.FirstOrDefault(p => $"{p.Base}/{p.Quote}" == favorite.Symbol);
                if (currentPair != null)
                {
                    favorite.Price = currentPair.Price;
                    favorite.PriceUsd = currentPair.PriceUsd;
                    favorite.Volume = currentPair.Volume;
                    favorite.Time = currentPair.Time;
                    favorite.FormattedTime = currentPair.FormattedTime;
                    System.Diagnostics.Debug.WriteLine($"Updated favorite {favorite.Symbol} from current exchange ({currentExchangeName})");
                }
            }
            else
            {
                // Add note about why price wasn't updated
                if (storedExchangeId != null && favoriteExchangeInfo.TryGetValue(favorite.Symbol, out _))
                {
                    var storedExchangeName = "Unknown";
                    foreach (var kvp in _exchangeMap)
                    {
                        if (kvp.Value == storedExchangeId)
                        {
                            storedExchangeName = kvp.Key;
                            break;
                        }
                    }
                    favorite.FormattedTime = $"{favorite.FormattedTime} (from {storedExchangeName})";
                }
                System.Diagnostics.Debug.WriteLine($"Favorite {favorite.Symbol} price NOT updated - stored from different exchange");
            }
            
            _favoritePairs.Add(favorite);
            System.Diagnostics.Debug.WriteLine($"Added favorite to UI: {favorite.Symbol} - Price: ${favorite.PriceUsd:N2}");
        }
        
        // Rest of method continues...
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error in RefreshFavoritesTab: {ex.Message}");
    }
}
```

### Updated FavoriteButton_Click Method:
```csharp
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
                
                // Get current exchange info (NEW)
                string? currentExchange = ExchangesComboBox?.SelectedItem?.ToString() ?? "Unknown";
                string? currentExchangeId = null;
                if (currentExchange != "Unknown" && _exchangeMap.TryGetValue(currentExchange, out var exchangeId))
                {
                    currentExchangeId = exchangeId;
                }
                
                System.Diagnostics.Debug.WriteLine($"Adding {symbol} to favorites. Current pair found: {currentPair != null}, Exchange: {currentExchange} (ID: {currentExchangeId})");
                
                // Pass exchange info when adding favorite (NEW)
                await _dataService.AddFavoriteAsync(
                    symbol, 
                    currentPair?.Base, 
                    currentPair?.Quote, 
                    currentPair?.PriceUsd, 
                    currentExchange,
                    currentExchangeId);  // NEW: Pass exchange ID
                
                button.Content = "★";
                System.Diagnostics.Debug.WriteLine($"Added {symbol} to favorites with exchange: {currentExchange}");
            }
        }
        catch (Exception ex)
        {
            // Error handling...
        }
    }
}
```

### Price Staleness Validation Methods:
```csharp
/// <summary>
/// Checks if a price timestamp is too old (> 1 hour)
/// </summary>
private bool IsPriceTimestampStale(DateTime? priceTimestamp)
{
    if (priceTimestamp == null)
        return true;

    TimeSpan age = DateTime.UtcNow - priceTimestamp.Value;
    return age.TotalSeconds > 3600; // 1 hour
}

/// <summary>
/// Formats price with staleness indicator if needed
/// </summary>
private string FormatPriceWithStalenessIndicator(string basePrice, DateTime? priceTimestamp)
{
    if (IsPriceTimestampStale(priceTimestamp))
    {
        return $"{basePrice} ⚠"; // Warning indicator for stale prices
    }
    return basePrice;
}
```

---

## 4. Key Algorithm: Exchange Update Decision Tree

```
User switches to Exchange X
    ↓
Is Exchange X in system?
    ├─ NO → Keep all favorite prices (show last known)
    └─ YES ↓
        
For each favorite:
    ├─ Does it have stored ExchangeId?
    │   ├─ NO → Check if X = Primary Exchange
    │   │       ├─ YES → Update price ✓
    │   │       └─ NO  → Keep price (show "(from Unknown)")
    │   └─ YES ↓
    │       Does X == Stored ExchangeId?
    │       ├─ YES → Update price ✓
    │       └─ NO ↓
    │           Does X == Primary Exchange?
    │           ├─ YES → Update price ✓
    │           └─ NO  → Keep price ✓ (show "from Binance")
    └─ Add to UI
```

---

## 5. Example Scenario

```
SETUP:
- Add BTC/USD favorite from Binance (ID: 2)
  Stored: Symbol=BTC/USD, ExchangeId=2, PriceUsd=$45,000

USER ACTIONS:
1. Switch to Kraken (ID: 29)
   → Check: 29 == 2? NO → Keep $45,000 ✓
   
2. Switch to Coinbase (ID: 37)
   → Check: 37 == 2? NO → Keep $45,000 ✓
   
3. Switch back to Binance (ID: 2)
   → Check: 2 == 2? YES → Update to new Binance price ✓
   
4. Set Primary Exchange = Kraken (ID: 29)
   → Switch to Kraken
   → Check: 29 == 2? NO, but 29 == Primary? YES → Update ✓
```

---

## Database Query Examples

### View All Favorites with Exchange Info:
```sql
SELECT 
    Symbol,
    LastPrice,
    LastExchange,
    ExchangeId,
    PriceTimestamp,
    CreatedAt
FROM Favorites
ORDER BY CreatedAt DESC;
```

### Set Primary Exchange:
```sql
INSERT OR REPLACE INTO Settings (Key, Value)
VALUES ('PrimaryExchangeForFavorites', '2');  -- Binance
```

### Check for Stale Prices:
```sql
SELECT 
    Symbol,
    PriceTimestamp,
    CASE 
        WHEN PriceTimestamp < datetime('now', '-1 hour') THEN 'STALE'
        ELSE 'FRESH'
    END as Status
FROM Favorites;
```

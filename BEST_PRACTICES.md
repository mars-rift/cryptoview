# Best Practices for Exchange Price Data Management

## Overview
This document outlines best practices for handling cryptocurrency prices from multiple exchanges, particularly for "favorite" or "watchlist" items.

---

## Problem Domain
When working with multiple cryptocurrency exchanges:
- Different exchanges have different prices for the same asset
- Some exchanges are more reliable/up-to-date than others
- Users often want consistent price tracking across exchanges
- Price volatility varies by exchange liquidity

---

## Best Practice #1: Source Exchange Pinning

### Definition
Associate each tracked asset with the exchange it was added from, and prefer to source future price data from that same exchange.

### Why It Works
- **Consistency**: User sees stable prices from a trusted source
- **Reliability**: Eliminates sudden jumps from unreliable exchanges
- **User Control**: User chooses their preferred price source
- **Auditability**: Can track which exchange was the source

### Implementation Pattern
```csharp
// Store
public class FavoritePrice
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public string SourceExchangeId { get; set; }  // Pin to source
    public DateTime LastUpdated { get; set; }
}

// Update Logic
if (currentExchange.Id == favorite.SourceExchangeId)
{
    favorite.Price = freshPrice;  // Safe to update
}
```

---

## Best Practice #2: Primary Exchange Strategy

### Definition
Allow users to set a single "primary" or "preferred" exchange that overrides individual source exchanges.

### Use Cases
1. **Institutional traders** who prefer one exchange's price feed
2. **Arbitrage tracking** between exchanges
3. **Risk management** - use most conservative prices

### Implementation Pattern
```csharp
// Configuration
public class ExchangeSettings
{
    public string? PrimaryExchangeId { get; set; }  // Global override
}

// Update Logic
if (exchange.Id == favorite.SourceExchange || 
    exchange.Id == settings.PrimaryExchangeId)
{
    favorite.Price = freshPrice;
}
```

---

## Best Practice #3: Timestamp Tracking & Staleness Detection

### Definition
Track when data was last updated and flag/exclude stale data.

### Staleness Thresholds
| Data Age | Status | Action |
|----------|--------|--------|
| < 5 min  | Fresh  | Use immediately |
| 5-60 min | Recent | Use with confidence |
| 1-24 hrs | Stale  | Flag with ⚠ indicator |
| > 24 hrs | Old    | Don't use automatically |

### Implementation Pattern
```csharp
private bool IsDataStale(DateTime lastUpdate, TimeSpan threshold)
{
    return DateTime.UtcNow - lastUpdate > threshold;
}

private void UpdatePrice(Favorite fav, Exchange exchange, FreshPrice price)
{
    if (ShouldUpdateFromExchange(exchange.Id))
    {
        if (IsDataStale(price.Timestamp, TimeSpan.FromHours(1)))
        {
            fav.StalenessIndicator = "⚠ (1+ hours old)";
        }
        fav.Price = price.Value;
        fav.LastUpdated = price.Timestamp;
    }
}
```

---

## Best Practice #4: Graceful Degradation

### Definition
When unable to source fresh price, show last-known price rather than failing.

### Benefits
- **No data loss** - User sees something rather than nothing
- **Transparency** - Indicates data source and age
- **Resilience** - Handles temporary exchange outages

### UI Patterns
```
Price Display Options:
├─ Fresh Price
│  └─ Display: "$45,123.45"
├─ Recent Price
│  └─ Display: "$45,123.45 (5 mins ago)"
├─ Stale Price  
│  └─ Display: "$45,123.45 (4 hours ago) ⚠ - from Binance"
└─ No Data
   └─ Display: "-- (unavailable)" or historical price if available
```

---

## Best Practice #5: Audit Trail

### Definition
Maintain logs of what data came from where and when.

### Implementation Pattern
```csharp
public class PriceHistory
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public string ExchangeId { get; set; }
    public DateTime Timestamp { get; set; }
    public string DataQuality { get; set; }  // "Fresh", "Stale", "Cached"
}

// Log every update
await _logger.LogPriceUpdateAsync(new PriceHistory
{
    Symbol = "BTC/USD",
    Price = 45123.45m,
    ExchangeId = "binance",
    Timestamp = DateTime.UtcNow,
    DataQuality = freshness
});
```

---

## Best Practice #6: Exchange Reliability Scoring

### Advanced: Rank exchanges by reliability

```csharp
public class ExchangeReliability
{
    public string ExchangeId { get; set; }
    public double UpScore { get; set; }        // % time API works
    public double FreshDataScore { get; set; } // % time < 5min old
    public double VolatilityScore { get; set; } // Price stability
    
    public double OverallScore => 
        (UpScore * 0.4) + (FreshDataScore * 0.4) + (VolatilityScore * 0.2);
}

// Use highest-scoring exchange for favorites
var bestExchange = exchanges.OrderByDescending(e => e.OverallScore).First();
```

---

## Best Practice #7: Configurable Update Policies

### Definition
Let users define when/how favorite prices update.

### Policy Examples
```
Policy: "Conservative"
├─ Only update from source exchange
├─ Flag if stale > 1 hour
├─ Never use primary exchange override
└─ Manual refresh required

Policy: "Standard" (Default)
├─ Update from source OR primary exchange
├─ Auto-refresh daily
├─ Flag if stale > 6 hours
└─ Allow manual refresh

Policy: "Aggressive"
├─ Update from any exchange with fresh data
├─ Auto-refresh hourly
├─ Flag if stale > 1 hour
└─ Warn on price >5% deviation
```

---

## Best Practice #8: Price Deviation Detection

### Definition
Alert when favorite price changes dramatically.

```csharp
private bool IsPriceDeviation(decimal oldPrice, decimal newPrice, decimal threshold = 0.05m)
{
    var change = Math.Abs((newPrice - oldPrice) / oldPrice);
    return change > threshold; // >5% change
}

// Usage
if (IsPriceDeviation(favorite.LastPrice, freshPrice))
{
    favorite.Warning = $"⚠ Price changed {change:P} from different exchange";
}
```

---

## Implementation Roadmap for CryptoView

### Phase 1: MVP (✓ COMPLETED)
- [x] Store source exchange with each favorite
- [x] Timestamp tracking
- [x] Skip update if exchange mismatch
- [x] UI indicator showing source exchange

### Phase 2: Enhanced UX (NEXT)
- [ ] Settings UI for primary exchange
- [ ] Staleness visual indicators (color coding)
- [ ] Hover tooltips showing full price history
- [ ] "Last updated" timestamp display

### Phase 3: Advanced Features
- [ ] Exchange reliability scoring
- [ ] Configurable update policies
- [ ] Price deviation alerts
- [ ] Export price history CSV

### Phase 4: Professional Features
- [ ] Multi-source price aggregation
- [ ] Weighted average pricing
- [ ] Custom exchange blacklist/whitelist
- [ ] API webhook notifications

---

## Testing Strategy

### Unit Tests
```csharp
[TestMethod]
public void UpdatePrice_MatchingExchange_ShouldUpdate()
{
    var favorite = new Favorite 
    { 
        Symbol = "BTC/USD",
        SourceExchangeId = "2" // Binance
    };
    
    var result = UpdatePrice(favorite, currentExchange: "2", price: 45000);
    
    Assert.IsTrue(result, "Should update when exchange matches");
}

[TestMethod]
public void UpdatePrice_DifferentExchange_ShouldNotUpdate()
{
    var favorite = new Favorite 
    { 
        Symbol = "BTC/USD",
        SourceExchangeId = "2"  // Binance
    };
    
    var result = UpdatePrice(favorite, currentExchange: "29", price: 44500);
    
    Assert.IsFalse(result, "Should NOT update when exchange differs");
}
```

### Integration Tests
- Add favorite from Exchange A
- Switch to B, C, D - verify no update
- Switch back to A - verify update
- Set primary to B - verify updates when B selected
- Check staleness indicators at 5min, 1hr, 24hr marks

### User Acceptance Tests
- Can I add favorites from multiple exchanges?
- Do prices stay stable when switching exchanges?
- Can I set a preferred exchange?
- Are stale prices clearly marked?
- Can I manually refresh favorites?

---

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Always Update
```csharp
// BAD - Updates from ANY exchange
foreach (var favorite in favorites)
{
    favorite.Price = GetCurrentPrice(currentExchange);
}
```

### ❌ Anti-Pattern 2: No Source Tracking
```csharp
// BAD - No way to know where price came from
var favorite = new { Symbol, Price };
```

### ❌ Anti-Pattern 3: Ignore Staleness
```csharp
// BAD - No way to detect old data
price = FetchPrice();  // Could be from cache, from yesterday, etc.
```

### ✅ Good Pattern
```csharp
// GOOD - Explicit source, timestamp, staleness check
if (exchange.Id == favorite.SourceExchangeId)
{
    if (!IsStale(price.Timestamp))
    {
        favorite.Price = price.Value;
        favorite.LastUpdated = price.Timestamp;
    }
    else
    {
        favorite.Warning = "Price data is stale";
    }
}
```

---

## Configuration Examples

### appsettings.json
```json
{
  "FavoritesSettings": {
    "PrimaryExchangeId": null,
    "StaleDataThresholdMinutes": 60,
    "AutoRefreshIntervalMinutes": 30,
    "UpdatePolicy": "Standard",
    "AlertOnDeviation": {
      "Enabled": true,
      "ThresholdPercent": 5
    }
  }
}
```

---

## Monitoring & Alerting

### Metrics to Track
- % of favorites with fresh data
- Avg age of favorite prices
- Exchanges used as source
- Price deviation frequency
- Exchange uptime/reliability

### Alerts to Set
- "Favorite X has stale data (>24 hours)"
- "Exchange Y reliability dropped below 90%"
- "Price deviation detected for favorite X (+/-10%)"
- "Exchange Z has not provided updates in 2 hours"

---

## Conclusion

By implementing these best practices, CryptoView now provides:
1. ✅ Stable, reliable favorite prices
2. ✅ Transparent data sourcing
3. ✅ Protection against stale data
4. ✅ User control and override options
5. ✅ Clear UI indicators
6. ✅ Audit trail for debugging

This transforms favorites from a simple list into a **robust, enterprise-grade price tracking system**.

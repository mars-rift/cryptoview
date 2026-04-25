# CryptoView Favorites - User Guide

## What's New?

Your favorite cryptocurrency prices now stay **stable and reliable** across all exchanges.

---

## How It Works

### Before the Fix ❌
```
1. Add BTC/USD as favorite from Binance → Shows $45,000
2. Switch to Kraken → Price jumps to $44,500 (Kraken's data)
3. Switch to Huobi → Price drops to $42,000 (stale data!)
4. Switch back to Binance → Now shows $45,100
```
**Result**: Confusing price jumps, can't trust favorite prices

### After the Fix ✅
```
1. Add BTC/USD as favorite from Binance → Shows $45,000
2. Switch to Kraken → Price stays $45,000 ✓ (preserved)
3. Switch to Huobi → Price still $45,000 ✓ (preserved)
4. Switch back to Binance → Price updates to $45,100 ✓ (fresh)
```
**Result**: Stable prices, always from your preferred exchange

---

## Features

### 1. Exchange Pinning
- Each favorite "remembers" which exchange it was added from
- Prices only update when you switch to that exchange
- Prevents unreliable exchanges from affecting your data

### 2. Exchange Information Display
When you switch to a different exchange, favorites show:
```
BTC/USD
Price: $45,000
Last Updated: 2026-04-25 14:30:22 (from Binance)
```
This tells you the price is preserved from Binance and won't change until you switch back.

### 3. Primary Exchange Setting (Coming Soon)
You'll be able to set ONE trusted exchange as your default:
- All favorites update when that exchange is selected
- Other exchanges won't affect your favorite prices
- Perfect if you prefer Binance or another exchange

### 4. Staleness Warnings
Prices older than 1 hour show a ⚠ indicator:
```
BTC/USD: $45,000 ⚠ (4 hours old from Binance)
```
This helps you know when data might be outdated.

---

## Usage Tips

### Tip 1: Choose Your "Home" Exchange
Add your favorites from the exchange you trust most:
- Pick one with reliable data (Binance, Coinbase Pro, Kraken)
- Add all your important favorites from there
- Now you have a consistent price baseline

### Tip 2: Browse Other Exchanges Freely
Switch between exchanges without worrying about favorite prices:
```
✓ You can compare exchange-specific pairs
✓ Your favorite prices stay protected
✓ Switch back when you want to update them
```

### Tip 3: Notice the UI Indicators
```
$45,000                    ← Fresh price, from current exchange
$45,000 (from Binance)     ← Preserved price, not from current exchange
$45,000 ⚠                  ← Old price (> 1 hour), may need refresh
```

### Tip 4: Manual Refresh
Not yet supported, but coming soon:
- Button to manually refresh favorite prices
- Would let you update from current exchange on-demand
- Useful if you want to "migrate" favorites to a new exchange

---

## Troubleshooting

### "My favorite price won't update when I switch exchanges"
**This is intentional!** ✓
- Your favorite was added from Binance
- It only updates when you switch back to Binance
- This protects you from stale data
- Try switching back to the original exchange

### "The price says (from Binance) but I'm looking at Kraken"
**This is informational.** ✓
- Means your favorite was created from Binance
- Kraken doesn't have updated price
- Your favorite is preserved safely
- Switch to Binance to get fresh price

### "The price has a ⚠ warning"
**This is a heads-up.** ✓
- Price is more than 1 hour old
- Binance may not have been loaded recently
- Switch to Binance and reload to refresh
- Or try another exchange

### "I want my favorite to update from multiple exchanges"
**Coming soon** 🔄
- Set that exchange as your "Primary Exchange"
- Then it will update when you select that exchange too
- More flexible control planned for future versions

---

## FAQ

**Q: Will my favorite prices be lost?**
A: No! They're saved in the database. Every favorite remembers:
- The symbol (BTC/USD)
- The exchange it was added from
- The last known price
- When it was last updated

**Q: Can I add the same crypto from different exchanges?**
A: Yes! Add BTC/USD from Binance and BTC/USD from Kraken as separate favorites. They'll track independently.

**Q: What if an exchange goes down?**
A: Your favorite prices are cached locally. They'll show the last known price and indicate which exchange they're from. Once the exchange is back up, you can refresh them.

**Q: Can I switch an existing favorite to a different exchange?**
A: Not yet, but you can:
1. Remove the current favorite (☆)
2. Switch to the other exchange
3. Add it again as a new favorite
It will now use the new exchange as its source.

**Q: Why are some prices grayed out or marked differently?**
A: UI enhancements help you distinguish:
- Fresh prices (just updated)
- Preserved prices (from another exchange)
- Stale prices (old data)

**Q: How often do prices update?**
A: When you load an exchange, all favorites from that exchange get fresh prices. This happens when you:
- Click "Load" button
- Switch exchanges (automatic load)
- App starts up (default exchange loads)

---

## Examples

### Example 1: Basic Usage
```
Step 1: Switch to Binance
Step 2: Add BTC/USD, ETH/USD, DOGE/USD as favorites
Step 3: Favorites tab now shows all three with Binance prices

Step 4: Switch to Kraken
Step 5: Favorites still show Binance prices (protected!)

Step 6: Check Kraken pairs while favorites stay stable
Step 7: Switch back to Binance
Step 8: Favorites update with fresh Binance prices ✓
```

### Example 2: Comparing Exchanges
```
Goal: Find the best BTC price across exchanges

Step 1: Add BTC/USD as favorite from Binance (saves $45,000)
Step 2: Switch to Kraken
   - Check Kraken's BTC price ($44,800) - lower! ✓
   - Favorite still shows $45,000 (protected)
   
Step 3: Switch to Huobi  
   - Check Huobi's BTC price ($42,000) - seems wrong
   - Favorite still shows $45,000 (protected from stale data)
   
Step 4: Switch back to Binance
   - Favorite updates to $45,050 (fresh price)
   - Kraken was indeed cheaper
   
Conclusion: Kraken has best price for BTC right now
```

### Example 3: Multiple Favorites
```
Favorites Added:
├─ BTC/USD (from Binance) - $45,000
├─ ETH/USD (from Binance) - $2,800
├─ ADA/USD (from Kraken) - $0.98
└─ XRP/USD (from Coinbase) - $0.52

Current Exchange: Kraken
Favorites Display:
├─ BTC/USD - $45,000 (from Binance) - won't update
├─ ETH/USD - $2,800 (from Binance) - won't update  
├─ ADA/USD - $0.98 - WILL update! ✓ (from Kraken)
└─ XRP/USD - $0.52 (from Coinbase) - won't update

Switch to Binance:
├─ BTC/USD - $45,050 - WILL update! ✓
├─ ETH/USD - $2,810 - WILL update! ✓
├─ ADA/USD - $0.98 (from Kraken) - won't update
└─ XRP/USD - $0.52 (from Coinbase) - won't update
```

---

## Coming Soon 🚀

- **Primary Exchange Setting** - Set one exchange for all favorites
- **Manual Refresh Button** - Update a favorite on demand
- **Favorite Edit Dialog** - Change exchange source
- **Price History Graph** - See price changes over time
- **Exchange Comparison** - Compare same crypto across exchanges
- **Import/Export** - Backup and restore favorites
- **Price Alerts** - Notify when price reaches target

---

## Support

If you encounter issues:
1. Check the "Debug Output" panel (bottom of app)
2. Note the error message and timestamp
3. Try the action again
4. Report with details and your OS version

---

## Summary

✅ Your favorites are now **safe and stable**
✅ No more price confusion from switching exchanges
✅ Clear indicators show where data comes from
✅ Full control over which exchange to trust

**Happy tracking!** 📈

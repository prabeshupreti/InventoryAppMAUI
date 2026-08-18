namespace InventoryApp.Client.Services;

/// <summary>
/// Small shared state bag for cross-component signals, e.g. the nav badge showing
/// how many products need attention. Scoped per app instance.
/// </summary>
public sealed class AppState
{
    private int _lowStockCount;

    public event Action? Changed;

    public int LowStockCount
    {
        get => _lowStockCount;
        set
        {
            if (_lowStockCount == value)
            {
                return;
            }

            _lowStockCount = value;
            Changed?.Invoke();
        }
    }
}

namespace InventoryApp.Client;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "Inventory Manager",
            Width = 1400,
            Height = 900,
            MinimumWidth = 420,
            MinimumHeight = 640
        };
    }
}

namespace DeepSeekClock.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var context = new TrayAppContext(showPopup: args.Contains("--show"));
        Application.Run(context);
    }
}

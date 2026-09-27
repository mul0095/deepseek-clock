using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using DeepSeekClock.Core;
using DeepSeekClock.Windows;

namespace DeepSeekClock.Windows.Tests;

public sealed class PopupLayoutTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothModelsKeepAllRatesAndFooterInsideTheCard(bool light)
    {
        OnSta(() =>
        {
            using var window = new PopupWindow();
            Assert.True(window.AllowsTransparency);
            Assert.Equal(0, ((SolidColorBrush)window.Background).Color.A);
            Assert.Equal(SizeToContent.Height, window.SizeToContent);
            var view = Assert.IsType<PopupView>(window.Content);
            view.SetTheme(light);
            view.ShowState(new ClockState(PricingPhase.OffPeak, null, "59h 59m", "11:59 PM"));
            foreach (var model in new[] { "FlashTab", "ProTab" })
            {
                ((Button)view.FindName(model)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                view.Measure(new Size(window.Width, double.PositiveInfinity));
                view.Arrange(new Rect(view.DesiredSize));
                view.UpdateLayout();
                foreach (var name in new[] { "BrandIcon", "PinButton", "OptionsButton", "MinimizeButton", "CloseButton", "StatusPill", "Countdown", "Transition", "CacheHit", "CacheMiss", "Output", "ConsoleLink", "QuitLink" })
                    AssertFitsEveryAncestor((FrameworkElement)view.FindName(name), view);
                Assert.Equal(model == "FlashTab" ? "$0.60" : "$1.98", ((TextBlock)view.FindName("Output")).Text);
                var output = (TextBlock)view.FindName("Output");
                var quit = (Button)view.FindName("QuitLink");
                Assert.True(output.TranslatePoint(new Point(0, output.ActualHeight), view).Y < quit.TranslatePoint(new Point(), view).Y);
            }
        });
    }

    [Fact]
    public void ChangingThemeRefreshesTheCurrentPhaseImmediately()
    {
        OnSta(() =>
        {
            var view = new PopupView();
            view.SetTheme(false);
            view.ShowState(new ClockState(PricingPhase.Peak, null, "1h 00m", "12:00 PM"));
            var status = (TextBlock)view.FindName("StatusText");
            var darkColor = ((SolidColorBrush)status.Foreground).Color;
            view.SetTheme(true);
            Assert.Equal("Peak", status.Text);
            Assert.NotEqual(darkColor, ((SolidColorBrush)status.Foreground).Color);
            Assert.Equal("$1.20", ((TextBlock)view.FindName("Output")).Text);
        });
    }

    private static void AssertFitsEveryAncestor(FrameworkElement element, FrameworkElement root)
    {
        Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0, element.Name);
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not FrameworkElement ancestor)
                continue;
            var bounds = element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
            Assert.True(bounds.Left >= -0.5 && bounds.Top >= -0.5 &&
                bounds.Right <= ancestor.ActualWidth + 0.5 && bounds.Bottom <= ancestor.ActualHeight + 0.5,
                $"{element.Name} is clipped by {ancestor.GetType().Name}: {bounds} inside {ancestor.RenderSize}");
            if (ancestor == root)
                break;
        }
    }

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF layout did not finish.");
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}

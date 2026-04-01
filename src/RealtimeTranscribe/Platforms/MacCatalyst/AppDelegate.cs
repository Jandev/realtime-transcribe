using Foundation;
using ObjCRuntime;
using UIKit;

namespace RealtimeTranscribe;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Set tab bar item title font size to 13pt (readable, matches Apple HIG caption size)
        var font = UIFont.SystemFontOfSize(13);
        var attrs = new UIStringAttributes { Font = font };

        var appearance = new UITabBarAppearance();
        appearance.ConfigureWithDefaultBackground();
        appearance.StackedLayoutAppearance.Normal.TitleTextAttributes = attrs;
        appearance.StackedLayoutAppearance.Selected.TitleTextAttributes = attrs;

        UITabBar.Appearance.StandardAppearance = appearance;
        UITabBar.Appearance.ScrollEdgeAppearance = appearance;

        return result;
    }
}

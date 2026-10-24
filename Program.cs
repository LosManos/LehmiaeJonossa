using Avalonia;
using System;
using System.IO;
using System.Linq;

namespace LehmiaeJonossa;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        EnsureCliPathsInEnvironment();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// When packaged as a macOS app bundle (.app) and launched from Finder or LaunchServices,
    /// the application process does not inherit the user shell's PATH (~/.zshrc), receiving
    /// only a minimal system PATH (/usr/bin:/bin:/usr/sbin:/sbin).
    /// This augments the process-local PATH so that Azure CLI ('az' installed via Homebrew)
    /// and other CLI credentials can be located by Azure.Identity and Process.Start.
    /// This modification is strictly in-process and does not affect the system or other apps.
    /// </summary>
    internal static void EnsureCliPathsInEnvironment()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            return;

        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var candidateDirs = new[]
        {
            "/opt/homebrew/bin",
            "/opt/homebrew/sbin",
            "/usr/local/bin",
            "/usr/local/sbin",
            Path.Combine(userHome, ".local", "bin"),
            Path.Combine(userHome, "bin"),
            Path.Combine(userHome, ".azure", "bin")
        };

        var existingPaths = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var pathsToAdd = candidateDirs
            .Where(dir => Directory.Exists(dir) && !existingPaths.Contains(dir))
            .ToList();

        if (pathsToAdd.Count > 0)
        {
            var updatedPath = string.Join(Path.PathSeparator, pathsToAdd) + Path.PathSeparator + currentPath;
            Environment.SetEnvironmentVariable("PATH", updatedPath);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

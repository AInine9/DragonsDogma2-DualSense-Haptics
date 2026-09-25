
namespace DragonsDogma2DualSense;

static class AppHost
{
    public static void Initialize()
    {
        // The distributable has one bin directory and a user-editable root config.
        Files.Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        Directory.CreateDirectory(Files.Data(""));
    }
}

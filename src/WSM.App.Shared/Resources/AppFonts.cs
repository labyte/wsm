using System.Windows;
using System.Windows.Media;

namespace WSM.App.Shared.Resources;

/// <summary>
/// 应用内嵌字体资源键访问。
/// </summary>
public static class AppFonts
{
    public static FontFamily Light => Get("AppFontLight");
    public static FontFamily Regular => Get("AppFontRegular");
    public static FontFamily Medium => Get("AppFontMedium");
    public static FontFamily Bold => Get("AppFontBold");
    public static FontFamily Heavy => Get("AppFontHeavy");

    private static FontFamily Get(string key)
        => (FontFamily)Application.Current.FindResource(key);
}

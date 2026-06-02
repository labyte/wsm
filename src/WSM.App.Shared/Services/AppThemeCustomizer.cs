using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace WSM.App.Shared.Services;

/// <summary>
/// 浅色主题表面与文字色微调，提升背景与正文对比度。
/// </summary>
public static class AppThemeCustomizer
{
    private static readonly string[] OverriddenResourceKeys =
    {
        "MaterialDesignPaper",
        "MaterialDesignBackground",
        "MaterialDesign.Brush.Background",
        "MaterialDesignCardBackground",
        "MaterialDesignBody",
        "MaterialDesignBodyLight"
    };

    public static void ApplyIfLight(PaletteHelper paletteHelper)
    {
        if (paletteHelper.GetTheme().GetBaseTheme() != BaseTheme.Light)
        {
            return;
        }

        ApplyLightBrushes(Application.Current.Resources);
    }

    public static void ClearOverrides()
    {
        var resources = Application.Current.Resources;
        foreach (var key in OverriddenResourceKeys)
        {
            resources.Remove(key);
        }
    }

    private static void ApplyLightBrushes(ResourceDictionary resources)
    {
        SetBrush(resources, "MaterialDesignPaper", "#FFFFFF");
        SetBrush(resources, "MaterialDesignBackground", "#FFFFFF");
        SetBrush(resources, "MaterialDesign.Brush.Background", "#FFFFFF");
        SetBrush(resources, "MaterialDesignCardBackground", "#FFFFFF");
        SetBrush(resources, "MaterialDesignBody", "#212121");
        SetBrush(resources, "MaterialDesignBodyLight", "#616161");
    }

    private static void SetBrush(ResourceDictionary resources, string key, string colorHex)
    {
        resources[key] = CreateBrush(colorHex);
    }

    private static SolidColorBrush CreateBrush(string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex)!;
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}

using System.Windows;
using System.Windows.Media;

namespace SoftBoxLauncher.Services;

public static class ThemeService
{
    public static void ApplyTheme(bool isLightTheme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        if (isLightTheme)
        {
            ApplyLight(resources);
            return;
        }

        ApplyDark(resources);
    }

    private static void ApplyDark(ResourceDictionary resources)
    {
        Set(resources, "WindowBorderBrush", "#223247");
        Set(resources, "TitleBarBackgroundBrush", "#121C2B");
        Set(resources, "TitleBarForegroundBrush", "#F4F8FF");
        Set(resources, "TitleButtonHoverBrush", "#2E435F");
        Set(resources, "TitleButtonPressedBrush", "#3A587C");
        Set(resources, "TitleButtonCloseHoverBrush", "#D94A4A");

        Set(resources, "ThemeSwitchOffBrush", "#3A506A");
        Set(resources, "ThemeSwitchOnBrush", "#5DA7FF");
        Set(resources, "ThemeSwitchThumbBrush", "#F8FCFF");

        SetColor(resources, "WindowGradStartColor", "#0B1428");
        SetColor(resources, "WindowGradMidColor", "#15345C");
        SetColor(resources, "WindowGradEndColor", "#091425");
        Set(resources, "AuraLeftBrush", "#3088D1FF");
        Set(resources, "AuraRightBrush", "#2875F3D7");

        Set(resources, "ShellBackgroundBrush", "#33131F33");
        Set(resources, "ShellBorderBrush", "#2EFFFFFF");
        Set(resources, "ShellTitleBrush", "#FFFFFF");
        Set(resources, "ShellSubtitleBrush", "#D4E4FF");
        Set(resources, "PanelBackgroundBrush", "#2AFFFFFF");
        Set(resources, "ErrorBackgroundBrush", "#2AFFFFFF");
        Set(resources, "ErrorForegroundBrush", "#FFD2D2");

        Set(resources, "CardBackgroundBrush", "#22FFFFFF");
        Set(resources, "CardBorderBrush", "#35FFFFFF");
        Set(resources, "CardTitleBrush", "#FFFFFF");
        Set(resources, "CardDescriptionBrush", "#D8E9FF");
        Set(resources, "CardAssetBrush", "#A9C3E7");
        Set(resources, "CardIconBackgroundBrush", "#35FFFFFF");
        Set(resources, "CardExtensionBrush", "#D4E5FF");

        Set(resources, "ProgressTrackBrush", "#2CFFFFFF");
        Set(resources, "ProgressFillBrush", "#4DABFF");
        Set(resources, "ProgressTextBrush", "#CDE3FF");
        Set(resources, "ScrollBarTrackBrush", "#30435A72");
        Set(resources, "ScrollBarThumbBrush", "#8AAFD7FF");
        Set(resources, "ScrollBarThumbHoverBrush", "#B3CFF2FF");
        Set(resources, "ScrollBarThumbPressedBrush", "#D5E6FFFF");

        Set(resources, "ButtonPrimaryBrush", "#2A7DFF");
        Set(resources, "ButtonPrimaryHoverBrush", "#3A95FF");
        Set(resources, "ButtonPrimaryPressedBrush", "#1D62D3");
        Set(resources, "ButtonPrimaryDisabledBrush", "#557B8EAA");
        Set(resources, "ButtonSecondaryBrush", "#2DFFFFFF");
        Set(resources, "ButtonSecondaryHoverBrush", "#46FFFFFF");

        Set(resources, "FooterForegroundBrush", "#D8EBFF");
        Set(resources, "FooterSecondaryForegroundBrush", "#BCE5FF");
    }

    private static void ApplyLight(ResourceDictionary resources)
    {
        Set(resources, "WindowBorderBrush", "#B7C8D8");
        Set(resources, "TitleBarBackgroundBrush", "#ECF4FB");
        Set(resources, "TitleBarForegroundBrush", "#1E2B3A");
        Set(resources, "TitleButtonHoverBrush", "#D4E2EF");
        Set(resources, "TitleButtonPressedBrush", "#BCD0E4");
        Set(resources, "TitleButtonCloseHoverBrush", "#D94A4A");

        Set(resources, "ThemeSwitchOffBrush", "#A3B5C8");
        Set(resources, "ThemeSwitchOnBrush", "#3F8EF5");
        Set(resources, "ThemeSwitchThumbBrush", "#FFFFFF");

        SetColor(resources, "WindowGradStartColor", "#E8F4FF");
        SetColor(resources, "WindowGradMidColor", "#DDEEFF");
        SetColor(resources, "WindowGradEndColor", "#F5FAFF");
        Set(resources, "AuraLeftBrush", "#55A9E6FF");
        Set(resources, "AuraRightBrush", "#4D8FD5FF");

        Set(resources, "ShellBackgroundBrush", "#E8F5FFFF");
        Set(resources, "ShellBorderBrush", "#B9D3E8");
        Set(resources, "ShellTitleBrush", "#122338");
        Set(resources, "ShellSubtitleBrush", "#2A4A69");
        Set(resources, "PanelBackgroundBrush", "#DCEEFE");
        Set(resources, "ErrorBackgroundBrush", "#FFE4E4");
        Set(resources, "ErrorForegroundBrush", "#A22A2A");

        Set(resources, "CardBackgroundBrush", "#F6FBFFFF");
        Set(resources, "CardBorderBrush", "#BBD7ED");
        Set(resources, "CardTitleBrush", "#11243A");
        Set(resources, "CardDescriptionBrush", "#355877");
        Set(resources, "CardAssetBrush", "#4D7091");
        Set(resources, "CardIconBackgroundBrush", "#DCEEFF");
        Set(resources, "CardExtensionBrush", "#2F5679");

        Set(resources, "ProgressTrackBrush", "#CFE1EF");
        Set(resources, "ProgressFillBrush", "#3E90F6");
        Set(resources, "ProgressTextBrush", "#335978");
        Set(resources, "ScrollBarTrackBrush", "#98B7D3");
        Set(resources, "ScrollBarThumbBrush", "#5D8FBF");
        Set(resources, "ScrollBarThumbHoverBrush", "#4A7EAE");
        Set(resources, "ScrollBarThumbPressedBrush", "#3D709F");

        Set(resources, "ButtonPrimaryBrush", "#287AEF");
        Set(resources, "ButtonPrimaryHoverBrush", "#3D8FF7");
        Set(resources, "ButtonPrimaryPressedBrush", "#1F67C9");
        Set(resources, "ButtonPrimaryDisabledBrush", "#9DB8CF");
        Set(resources, "ButtonSecondaryBrush", "#DCEEFE");
        Set(resources, "ButtonSecondaryHoverBrush", "#CAE3FA");

        Set(resources, "FooterForegroundBrush", "#2C4E70");
        Set(resources, "FooterSecondaryForegroundBrush", "#406789");
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
    }

    private static void SetColor(ResourceDictionary resources, string key, string color)
    {
        resources[key] = (Color)ColorConverter.ConvertFromString(color)!;
    }
}

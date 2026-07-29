using MudBlazor;

namespace HTX586CONTRACT.Web.Themes;

public static class AppMudTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0B6B5D",
            PrimaryDarken = "#064D44",
            PrimaryLighten = "#D8F2EC",
            Secondary = "#2563EB",
            SecondaryDarken = "#1D4ED8",
            Success = "#16845B",
            Warning = "#D97706",
            Error = "#DC4C4C",
            Info = "#2563EB",
            Background = "#F4F7F9",
            Surface = "#FFFFFF",
            AppbarBackground = "rgba(255,255,255,.92)",
            AppbarText = "#17211F",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#344440",
            TextPrimary = "#17211F",
            TextSecondary = "#697975",
            Divider = "#E7ECEB"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px",
            DrawerWidthLeft = "292px",
            AppbarHeight = "72px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Manrope", "Inter", "Roboto", "Arial", "sans-serif"],
                FontSize = ".92rem",
                LineHeight = "1.55",
                FontWeight = "450"
            },
            H4 = new H4Typography
            {
                FontFamily = ["Manrope", "Inter", "sans-serif"],
                FontWeight = "750",
                FontSize = "1.8rem",
                LineHeight = "1.2"
            },
            H5 = new H5Typography
            {
                FontFamily = ["Manrope", "Inter", "sans-serif"],
                FontWeight = "720"
            },
            H6 = new H6Typography
            {
                FontFamily = ["Manrope", "Inter", "sans-serif"],
                FontWeight = "700"
            },
            Button = new ButtonTypography
            {
                TextTransform = "none",
                FontWeight = "700",
                LetterSpacing = "0"
            }
        }
    };
}

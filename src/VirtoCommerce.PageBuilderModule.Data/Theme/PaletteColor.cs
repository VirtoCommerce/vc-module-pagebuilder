using System.Globalization;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public readonly record struct PaletteColor(byte R, byte G, byte B)
{
    public static PaletteColor White { get; } = new(255, 255, 255);

    public static PaletteColor Black { get; } = new(0, 0, 0);

    public static bool TryParse(string value, out PaletteColor color)
    {
        color = default;

        var hex = value?.Trim();
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
        {
            return false;
        }

        hex = hex[1..];
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(x => new string(x, 2)));
        }

        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = new PaletteColor((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xff), (byte)(rgb & 0xff));
        return true;
    }

    /// <param name="hue">Degrees, 0–360.</param>
    /// <param name="saturation">0–1.</param>
    /// <param name="lightness">0–1.</param>
    public static PaletteColor FromHsl(double hue, double saturation, double lightness)
    {
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);

        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var sector = ((hue % 360) + 360) % 360 / 60;
        var second = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, second, 0d),
            1 => (second, chroma, 0d),
            2 => (0d, chroma, second),
            3 => (0d, second, chroma),
            4 => (second, 0d, chroma),
            _ => (chroma, 0d, second),
        };
        var offset = lightness - (chroma / 2);

        return new PaletteColor(ToByte(r + offset), ToByte(g + offset), ToByte(b + offset));
    }

    public (double Hue, double Saturation, double Lightness) ToHsl()
    {
        var r = R / 255d;
        var g = G / 255d;
        var b = B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var chroma = max - min;

        if (chroma == 0)
        {
            return (0, 0, lightness);
        }

        var saturation = chroma / (1 - Math.Abs((2 * lightness) - 1));
        double hue;
        if (max == r)
        {
            hue = 60 * ((g - b) / chroma % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / chroma) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / chroma) + 4);
        }

        return (hue < 0 ? hue + 360 : hue, saturation, lightness);
    }

    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}");

    public override string ToString() => ToHex();

    private static byte ToByte(double channel) => (byte)Math.Round(Math.Clamp(channel, 0, 1) * 255, MidpointRounding.AwayFromZero);
}

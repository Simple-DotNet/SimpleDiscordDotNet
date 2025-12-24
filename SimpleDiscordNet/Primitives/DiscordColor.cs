namespace SimpleDiscordNet;

/// <summary>
/// Represents a Discord embed color.
/// </summary>
public readonly struct DiscordColor(int value)
{
    public readonly int Value = value;

    public static DiscordColor FromRgb(byte r, byte g, byte b) => new((r << 16) | (g << 8) | b);

    /// <summary>
    /// Creates a color from RGB component values (0-255).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static DiscordColor FromRgb(int r, int g, int b)
    {
        if ((uint)r > 255u || (uint)g > 255u || (uint)b > 255u)
            throw new ArgumentOutOfRangeException("RGB components must be between 0 and 255.");
        return FromRgb((byte)r, (byte)g, (byte)b);
    }

    /// <summary>
    /// Creates a color from a hex string.
    /// Supports formats: "#RRGGBB", "0xRRGGBB", "RRGGBB", and shorthand "#RGB".
    /// Example: DiscordColor.FromHex("#FF5733")
    /// </summary>
    public static DiscordColor FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        return FromHex(hex.AsSpan());
    }

    /// <summary>
    /// Creates a color from a hex string span (zero-allocation).
    /// Supports formats: "#RRGGBB", "0xRRGGBB", "RRGGBB", and shorthand "#RGB".
    /// Example: DiscordColor.FromHex("#FF5733".AsSpan())
    /// </summary>
    public static DiscordColor FromHex(ReadOnlySpan<char> hex)
    {
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];
        else if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || hex.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        if (hex.Length == 3)
        {
            // Shorthand #RGB -> #RRGGBB
            Span<char> expanded = stackalloc char[2];
            expanded[0] = hex[0];
            expanded[1] = hex[0];
            int r = int.Parse(expanded, System.Globalization.NumberStyles.HexNumber);
            expanded[0] = hex[1];
            expanded[1] = hex[1];
            int g = int.Parse(expanded, System.Globalization.NumberStyles.HexNumber);
            expanded[0] = hex[2];
            expanded[1] = hex[2];
            int b = int.Parse(expanded, System.Globalization.NumberStyles.HexNumber);
            return FromRgb((byte)r, (byte)g, (byte)b);
        }

        if (hex.Length != 6)
            throw new FormatException("Hex color must have 3 or 6 hex digits.");

        int value = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        return new DiscordColor(value);
    }

    /// <summary>
    /// Attempts to parse a hex color string into a DiscordColor.
    /// </summary>
    public static bool TryFromHex(string? hex, out DiscordColor color)
    {
        try { color = FromHex(hex ?? string.Empty); return true; }
        catch { color = default; return false; }
    }

    /// <summary>
    /// Implicit conversion from <see cref="DiscordColor"/> to its integer value (RRGGBB).
    /// </summary>
    public static implicit operator int(DiscordColor color) => color.Value;

    /// <summary>
    /// Implicit conversion from an integer value (RRGGBB) to <see cref="DiscordColor"/>.
    /// </summary>
    public static implicit operator DiscordColor(int value) => new(value);

    // Commonly named colors
    public static DiscordColor Default => new(0x000000);
    public static DiscordColor Blue => new(0x3498DB);
    public static DiscordColor Green => new(0x2ECC71);
    public static DiscordColor Red => new(0xE74C3C);
    public static DiscordColor Yellow => new(0xF1C40F);
    public static DiscordColor Orange => new(0xE67E22);
    public static DiscordColor Purple => new(0x9B59B6);
    public static DiscordColor Teal => new(0x1ABC9C);
    public static DiscordColor Grey => new(0x95A5A6);

    // Discord brand and standard palette
    public static DiscordColor Blurple => new(0x5865F2);
    public static DiscordColor OldBlurple => new(0x7289DA);
    public static DiscordColor DiscordGreen => new(0x57F287);
    public static DiscordColor DiscordYellow => new(0xFEE75C);
    public static DiscordColor Fuchsia => new(0xEB459E);
    public static DiscordColor DiscordRed => new(0xED4245);
    public static DiscordColor NotQuiteBlack => new(0x23272A);
    public static DiscordColor DarkButNotBlack => new(0x2C2F33);
    public static DiscordColor White => new(0xFFFFFF);
    public static DiscordColor Black => new(0x000000);

    // Brand-prefixed alias for clarity where overlapping names exist
    public static DiscordColor DiscordBlue => Blurple;

    // Microsoft default color set (Fluent / Office palette) — flattened and prefixed
    public static DiscordColor MicrosoftBlue => new(0x0078D4);          // Communication Blue
    public static DiscordColor MicrosoftNavy => new(0x00188F);
    public static DiscordColor MicrosoftTeal => new(0x008272);
    public static DiscordColor MicrosoftGreen => new(0x107C10);
    public static DiscordColor MicrosoftLightGreen => new(0x7FBA00);
    public static DiscordColor MicrosoftLime => new(0xBAD80A);
    public static DiscordColor MicrosoftYellow => new(0xFFB900);
    public static DiscordColor MicrosoftOrange => new(0xF7630C);
    public static DiscordColor MicrosoftRed => new(0xD13438);
    public static DiscordColor MicrosoftMagenta => new(0xB4009E);
    public static DiscordColor MicrosoftPink => new(0xE3008C);
    public static DiscordColor MicrosoftPurple => new(0x5C2D91);
    public static DiscordColor MicrosoftCyan => new(0x0099BC);
    public static DiscordColor MicrosoftSteel => new(0x7A7574);         // Neutral
    public static DiscordColor MicrosoftGold => new(0xFFB900);
    public static DiscordColor MicrosoftBlack => new(0x000000);
    public static DiscordColor MicrosoftWhite => new(0xFFFFFF);
    public static DiscordColor MicrosoftDarkGray => new(0x605E5C);      // neutralSecondary
    public static DiscordColor MicrosoftGray => new(0xA19F9D);          // neutralTertiary
    public static DiscordColor MicrosoftLightGray => new(0xE1DFDD);     // neutralQuaternaryAlt

    // Comprehensive color palette - Standard web colors
    public static DiscordColor AliceBlue => new(0xF0F8FF);
    public static DiscordColor AntiqueWhite => new(0xFAEBD7);
    public static DiscordColor Aqua => new(0x00FFFF);
    public static DiscordColor Aquamarine => new(0x7FFFD4);
    public static DiscordColor Azure => new(0xF0FFFF);
    public static DiscordColor Beige => new(0xF5F5DC);
    public static DiscordColor Bisque => new(0xFFE4C4);
    public static DiscordColor BlanchedAlmond => new(0xFFEBCD);
    public static DiscordColor BlueViolet => new(0x8A2BE2);
    public static DiscordColor Brown => new(0xA52A2A);
    public static DiscordColor BurlyWood => new(0xDEB887);
    public static DiscordColor CadetBlue => new(0x5F9EA0);
    public static DiscordColor Chartreuse => new(0x7FFF00);
    public static DiscordColor Chocolate => new(0xD2691E);
    public static DiscordColor Coral => new(0xFF7F50);
    public static DiscordColor CornflowerBlue => new(0x6495ED);
    public static DiscordColor Cornsilk => new(0xFFF8DC);
    public static DiscordColor Crimson => new(0xDC143C);
    public static DiscordColor Cyan => new(0x00FFFF);
    public static DiscordColor DarkBlue => new(0x00008B);
    public static DiscordColor DarkCyan => new(0x008B8B);
    public static DiscordColor DarkGoldenrod => new(0xB8860B);
    public static DiscordColor DarkGray => new(0xA9A9A9);
    public static DiscordColor DarkGreen => new(0x006400);
    public static DiscordColor DarkKhaki => new(0xBDB76B);
    public static DiscordColor DarkMagenta => new(0x8B008B);
    public static DiscordColor DarkOliveGreen => new(0x556B2F);
    public static DiscordColor DarkOrange => new(0xFF8C00);
    public static DiscordColor DarkOrchid => new(0x9932CC);
    public static DiscordColor DarkRed => new(0x8B0000);
    public static DiscordColor DarkSalmon => new(0xE9967A);
    public static DiscordColor DarkSeaGreen => new(0x8FBC8F);
    public static DiscordColor DarkSlateBlue => new(0x483D8B);
    public static DiscordColor DarkSlateGray => new(0x2F4F4F);
    public static DiscordColor DarkTurquoise => new(0x00CED1);
    public static DiscordColor DarkViolet => new(0x9400D3);
    public static DiscordColor DeepPink => new(0xFF1493);
    public static DiscordColor DeepSkyBlue => new(0x00BFFF);
    public static DiscordColor DimGray => new(0x696969);
    public static DiscordColor DodgerBlue => new(0x1E90FF);
    public static DiscordColor Firebrick => new(0xB22222);
    public static DiscordColor FloralWhite => new(0xFFFAF0);
    public static DiscordColor ForestGreen => new(0x228B22);
    public static DiscordColor Gainsboro => new(0xDCDCDC);
    public static DiscordColor GhostWhite => new(0xF8F8FF);
    public static DiscordColor Gold => new(0xFFD700);
    public static DiscordColor Goldenrod => new(0xDAA520);
    public static DiscordColor GreenYellow => new(0xADFF2F);
    public static DiscordColor Honeydew => new(0xF0FFF0);
    public static DiscordColor HotPink => new(0xFF69B4);
    public static DiscordColor IndianRed => new(0xCD5C5C);
    public static DiscordColor Indigo => new(0x4B0082);
    public static DiscordColor Ivory => new(0xFFFFF0);
    public static DiscordColor Khaki => new(0xF0E68C);
    public static DiscordColor Lavender => new(0xE6E6FA);
    public static DiscordColor LavenderBlush => new(0xFFF0F5);
    public static DiscordColor LawnGreen => new(0x7CFC00);
    public static DiscordColor LemonChiffon => new(0xFFFACD);
    public static DiscordColor LightBlue => new(0xADD8E6);
    public static DiscordColor LightCoral => new(0xF08080);
    public static DiscordColor LightCyan => new(0xE0FFFF);
    public static DiscordColor LightGoldenrodYellow => new(0xFAFAD2);
    public static DiscordColor LightGray => new(0xD3D3D3);
    public static DiscordColor LightGreen => new(0x90EE90);
    public static DiscordColor LightPink => new(0xFFB6C1);
    public static DiscordColor LightSalmon => new(0xFFA07A);
    public static DiscordColor LightSeaGreen => new(0x20B2AA);
    public static DiscordColor LightSkyBlue => new(0x87CEFA);
    public static DiscordColor LightSlateGray => new(0x778899);
    public static DiscordColor LightSteelBlue => new(0xB0C4DE);
    public static DiscordColor LightYellow => new(0xFFFFE0);
    public static DiscordColor Lime => new(0x00FF00);
    public static DiscordColor LimeGreen => new(0x32CD32);
    public static DiscordColor Linen => new(0xFAF0E6);
    public static DiscordColor Magenta => new(0xFF00FF);
    public static DiscordColor Maroon => new(0x800000);
    public static DiscordColor MediumAquamarine => new(0x66CDAA);
    public static DiscordColor MediumBlue => new(0x0000CD);
    public static DiscordColor MediumOrchid => new(0xBA55D3);
    public static DiscordColor MediumPurple => new(0x9370DB);
    public static DiscordColor MediumSeaGreen => new(0x3CB371);
    public static DiscordColor MediumSlateBlue => new(0x7B68EE);
    public static DiscordColor MediumSpringGreen => new(0x00FA9A);
    public static DiscordColor MediumTurquoise => new(0x48D1CC);
    public static DiscordColor MediumVioletRed => new(0xC71585);
    public static DiscordColor MidnightBlue => new(0x191970);
    public static DiscordColor MintCream => new(0xF5FFFA);
    public static DiscordColor MistyRose => new(0xFFE4E1);
    public static DiscordColor Moccasin => new(0xFFE4B5);
    public static DiscordColor NavajoWhite => new(0xFFDEAD);
    public static DiscordColor Navy => new(0x000080);
    public static DiscordColor OldLace => new(0xFDF5E6);
    public static DiscordColor Olive => new(0x808000);
    public static DiscordColor OliveDrab => new(0x6B8E23);
    public static DiscordColor OrangeRed => new(0xFF4500);
    public static DiscordColor Orchid => new(0xDA70D6);
    public static DiscordColor PaleGoldenrod => new(0xEEE8AA);
    public static DiscordColor PaleGreen => new(0x98FB98);
    public static DiscordColor PaleTurquoise => new(0xAFEEEE);
    public static DiscordColor PaleVioletRed => new(0xDB7093);
    public static DiscordColor PapayaWhip => new(0xFFEFD5);
    public static DiscordColor PeachPuff => new(0xFFDAB9);
    public static DiscordColor Peru => new(0xCD853F);
    public static DiscordColor Pink => new(0xFFC0CB);
    public static DiscordColor Plum => new(0xDDA0DD);
    public static DiscordColor PowderBlue => new(0xB0E0E6);
    public static DiscordColor RosyBrown => new(0xBC8F8F);
    public static DiscordColor RoyalBlue => new(0x4169E1);
    public static DiscordColor SaddleBrown => new(0x8B4513);
    public static DiscordColor Salmon => new(0xFA8072);
    public static DiscordColor SandyBrown => new(0xF4A460);
    public static DiscordColor SeaGreen => new(0x2E8B57);
    public static DiscordColor SeaShell => new(0xFFF5EE);
    public static DiscordColor Sienna => new(0xA0522D);
    public static DiscordColor Silver => new(0xC0C0C0);
    public static DiscordColor SkyBlue => new(0x87CEEB);
    public static DiscordColor SlateBlue => new(0x6A5ACD);
    public static DiscordColor SlateGray => new(0x708090);
    public static DiscordColor Snow => new(0xFFFAFA);
    public static DiscordColor SpringGreen => new(0x00FF7F);
    public static DiscordColor SteelBlue => new(0x4682B4);
    public static DiscordColor Tan => new(0xD2B48C);
    public static DiscordColor Thistle => new(0xD8BFD8);
    public static DiscordColor Tomato => new(0xFF6347);
    public static DiscordColor Turquoise => new(0x40E0D0);
    public static DiscordColor Violet => new(0xEE82EE);
    public static DiscordColor Wheat => new(0xF5DEB3);
    public static DiscordColor WhiteSmoke => new(0xF5F5F5);
    public static DiscordColor YellowGreen => new(0x9ACD32);
}

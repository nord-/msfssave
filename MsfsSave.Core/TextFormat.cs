namespace MsfsSave.Core;

public static class TextFormat
{
    /// <summary>Kortar texten till exakt bredd och markerar avhuggning med ellips.</summary>
    public static string Truncate(string text, int width)
    {
        if (width <= 0) return "";
        if (text.Length <= width) return text;
        if (width == 1) return "…";
        return string.Concat(text.AsSpan(0, width - 1), "…");
    }
}

using System.Text.RegularExpressions;

namespace LyricNow.Lyrics;

/// <summary>
/// Spotify titles often carry suffixes like "- Remastered 2011" or "(feat. X)" that lyric
/// databases don't have. These helpers strip them so fuzzy searches have a better chance.
/// </summary>
public static partial class TitleCleaner
{
    [GeneratedRegex(@"\s+-\s+.*\b(remaster(ed)?|live|version|edit|mix|mono|stereo|acoustic|demo|radio|deluxe|bonus|from|recorded|session|single|original|anniversary)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex DashSuffix();

    [GeneratedRegex(@"\s*[\(\[](feat\.?|ft\.?|with|prod\.?)\s[^\)\]]*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturingTag();

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*\b(remaster(ed)?|live|version|edit|mix|mono|stereo|acoustic|demo|deluxe|bonus)\b[^\)\]]*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex VersionTag();

    [GeneratedRegex(@"\s*(,|&|\bfeat\.?|\bft\.?|\bx\b)\s.*$", RegexOptions.IgnoreCase)]
    private static partial Regex ExtraArtists();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonAlphanumeric();

    public static string CleanTitle(string title)
    {
        var cleaned = DashSuffix().Replace(title, "");
        cleaned = FeaturingTag().Replace(cleaned, "");
        cleaned = VersionTag().Replace(cleaned, "");
        cleaned = cleaned.Trim();
        return cleaned.Length > 0 ? cleaned : title.Trim();
    }

    /// <summary>"Artist A, Artist B" → "Artist A".</summary>
    public static string PrimaryArtist(string artist)
    {
        var primary = ExtraArtists().Replace(artist, "").Trim();
        return primary.Length > 0 ? primary : artist.Trim();
    }

    /// <summary>Lowercase letters and digits only, for loose comparisons.</summary>
    public static string Normalize(string s) => NonAlphanumeric().Replace(s.ToLowerInvariant(), "");

    public static bool LooselyMatches(string a, string b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return na.Length > 0 && nb.Length > 0 && (na.Contains(nb) || nb.Contains(na));
    }
}

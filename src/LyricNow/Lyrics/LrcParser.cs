using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricNow.Lyrics;

/// <summary>Parses LRC text, e.g. "[01:23.45]some line". A line may carry several time tags.</summary>
public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d+):(\d+(?:[.:]\d+)?)\]")]
    private static partial Regex TimeTag();

    public static List<LyricLine> Parse(string lrc)
    {
        var result = new List<LyricLine>();

        foreach (var rawLine in lrc.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var tags = TimeTag().Matches(line);
            if (tags.Count == 0)
                continue; // metadata such as [ar:...] or junk

            var text = TimeTag().Replace(line, "").Trim();
            foreach (Match tag in tags)
            {
                var minutes = int.Parse(tag.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = double.Parse(tag.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                result.Add(new LyricLine(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds), text));
            }
        }

        result.Sort((a, b) => a.Time.CompareTo(b.Time));
        return result;
    }
}

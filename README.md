# LyricNow
A small always-on-top widget that shows the lyric line currently playing in Spotify (or any other media player Windows knows about).

**New here? See the [step-by-step install & use guide](docs/GUIDE.md).**

## How it works
- **Now playing:** reads the Windows media session (the same info as the volume-key media flyout). No Spotify login or API key needed. Spotify's session is preferred; other players are used as a fallback.
- **Lyrics:** looked up independently of Spotify, so songs without lyrics in Spotify can still get them:
  1. [LRCLIB](https://lrclib.net): exact match, then fuzzy searches with cleaned-up titles (dropping "- Remastered", "(feat. …)", etc.), then the artist's songs matched by length (helps when a title is spelled differently, e.g. Persian script vs. Finglish)
  2. [Genius](https://genius.com): wide coverage including many Persian songs, plain text only. Genius's API doesn't provide lyrics, so they're read from the song page, which is outside Genius's API terms and may break if Genius changes its site. Can be turned off from the right-click menu.
  3. [lyrics.ovh](https://lyrics.ovh): plain-text fallback
  4. Otherwise: "No lyrics found"
- Persian/Arabic letter variants (ي/ی, ك/ک, …) are treated as equal when matching, and Persian lines are shown right-to-left.
- If only unsynced lyrics exist, lines are spread evenly over the song and marked "timing approximate".
- Results are cached in `%LOCALAPPDATA%\LyricNow\cache`.

## Styles
Right-click → **Style** to switch between five looks: **Classic**, **Frosted** (blurred glass with album cover and progress bar), **Floating** (text only, no panel), **Karaoke** (the current line fills as it's sung) and **Strip** (slim pill with current + next line and a progress line).

Fonts: [Bricolage Grotesque](https://fonts.google.com/specimen/Bricolage+Grotesque) and [Vazirmatn](https://fonts.google.com/specimen/Vazirmatn) are bundled (SIL Open Font License, see `src/LyricNow/Fonts`).

## Run
Requires the .NET 10 SDK on Windows 10 (2004) or later.

```
dotnet run --project src/LyricNow
```

- Drag the widget to move it (its position is remembered).
- Hover to reveal small **−/+** buttons that shift the lyric timing by 0.5 s (also under right-click → **Lyrics timing**). The adjustment is remembered per song.
- The widget is visible while Spotify runs and hides when Spotify closes.
- Right-click → **Start hidden with Windows (show when Spotify opens)** makes LyricNow start quietly at login and appear only alongside Spotify. Other menu items: **Style**, **Lyrics timing**, **Also search Genius for lyrics**, **Reset position**, **Exit**.
- Only one copy runs at a time.

To build a standalone exe:

```
dotnet publish src/LyricNow -c Release -r win-x64 --self-contained false -o publish
```

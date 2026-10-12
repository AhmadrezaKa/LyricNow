# LyricNow: Install & Use Guide

LyricNow is a small widget that sits in the corner of your screen and shows the lyric line currently playing in Spotify, together with the line before and the line after.

---

## 1. What you need

- **Windows 10 (version 2004 or newer) or Windows 11**
- **Spotify desktop app** (the installer from spotify.com or the Microsoft Store version both work)
- **.NET 10 SDK**, which builds the app (one-time install, see step 2)
- **Git** (optional), to download the code. You can also download a ZIP instead.
- An internet connection (lyrics are fetched online)

You do **not** need a Spotify developer account, an API key, or to log in anywhere.

---

## 2. Install the .NET 10 SDK

1. Go to <https://dotnet.microsoft.com/download/dotnet/10.0>.
2. Under **SDK**, download the **Windows x64** installer and run it.
3. Open a new **PowerShell** window and check it worked:
   ```powershell
   dotnet --list-sdks
   ```
   You should see a line starting with `10.`.

---

## 3. Download LyricNow

**With Git:**
```powershell
cd $HOME\Documents
git clone https://github.com/AhmadrezaKa/LyricNow.git
cd LyricNow
```

**Without Git:** on the GitHub page click **Code → Download ZIP**, extract it (e.g. to `Documents\LyricNow`), then open PowerShell in that folder.

---

## 4. Build the app

From the `LyricNow` folder, run:

```powershell
dotnet publish src/LyricNow -c Release -r win-x64 --self-contained false -o publish
```

When it finishes, the app is at `publish\LyricNow.exe`.

> **Tip:** Keep the `publish` folder where it is (or move it somewhere permanent, like `C:\Apps\LyricNow`) *before* turning on "Start with Windows" in step 7. That setting remembers the exact location of the exe.

---

## 5. First run

1. Open Spotify and play a song.
2. Double-click `publish\LyricNow.exe`.
3. The widget appears in the **bottom-right corner** of your screen:

   ```
   ┌──────────────────────────────────────────┐
   │ Song Title — Artist                      │
   │ previous line                            │
   │ CURRENT LINE                             │
   │ next line                                │
   │                                  LRCLIB  │
   └──────────────────────────────────────────┘
   ```

If Spotify isn't open yet, the widget says **"Waiting for Spotify…"** and picks up as soon as music starts.

---

## 6. Everyday use

| What you want | How |
|---|---|
| Move the widget | Click and drag it anywhere. The position is remembered. |
| Change the look | Right-click → **Style** → Classic, Frosted, Floating, Karaoke or Strip. Your choice is remembered. |
| Fix lyrics that are early/late | Hover over the widget. Small **−** and **+** buttons appear bottom-left. Each click shifts by 0.5 s. **+** shows lyrics sooner, **−** shows them later. Saved per song. (The Floating and Strip styles hide these buttons; use right-click → **Lyrics timing** instead.) |
| Undo a timing fix | Right-click → **Lyrics timing** → **Reset timing for this song** |
| Put it back in the corner | Right-click → **Reset position** |
| Close it | Right-click → **Exit** |

The widget doesn't appear in the taskbar. Right-click → **Exit** is how you close it.

### The five styles

| Style | What it looks like |
|---|---|
| **Classic** | The original solid dark panel with the Windows system font. |
| **Frosted** | Blurred glass panel with the album cover and a song-progress bar. |
| **Floating** | No panel, just large text with a soft shadow. Best on darker wallpapers. |
| **Karaoke** | The current line fills with color as it's sung (right to left for Persian). |
| **Strip** | A slim pill with the current and next line and a thin progress line at the bottom. |

All styles except Classic use Bricolage Grotesque for English and Vazirmatn for Persian. The fonts are built into the app. Persian lines are always aligned to the right.

### What the messages mean

| Message | Meaning |
|---|---|
| Searching for lyrics… | Looking the song up online (usually under a second). |
| No lyrics found | None of the lyric sources has this song. |
| ♪ Instrumental ♪ | The song has no vocals. |
| ♪ (as the current line) | An instrumental break within the song. |
| "unsynced, timing approximate" (bottom-right) | Only plain lyrics were found, so lines are spread evenly over the song. Expect them to drift; the previous/next lines help you follow along. |
| Couldn't load lyrics | Network problem. It retries when the song plays again. |

### Where lyrics come from

LyricNow does **not** depend on Spotify's lyrics. Even songs that show no lyrics in Spotify are looked up in:

1. **LRCLIB** (lrclib.net): free database with time-synced lyrics
2. **Genius** (genius.com): very wide coverage, including many Persian songs, but without timing
3. **lyrics.ovh**: plain-text fallback

Found lyrics are saved on your PC so each song is only looked up once.

**About Genius:** Genius doesn't offer lyrics through its official API, so LyricNow reads them from the song's web page. That's fine for personal use but isn't covered by Genius's API terms, and it may stop working if Genius changes its website. To turn it off, right-click → untick **Also search Genius for lyrics**.

**Persian songs:** Genius lists most Iranian artists in Latin letters. Songs whose Spotify title is in Latin letters (Finglish) are found most reliably there.

---

## 7. Show it automatically with Spotify (optional)

1. Right-click the widget.
2. Tick **Start hidden with Windows (show when Spotify opens)**.

From then on:
- LyricNow starts **invisibly** when you log in to Windows.
- The widget **appears when you open Spotify** and **hides when you close it**.
- It uses almost no resources while waiting.

To turn it off, right-click the widget and untick the same option.

---

## 8. Updating to a newer version

1. Right-click the widget → **Exit**.
2. Get the latest code: `git pull` (or download the ZIP again).
3. Run the build command from step 4 again. It overwrites `publish\LyricNow.exe`, so the startup setting keeps working.

---

## 9. Uninstalling

1. Right-click the widget and **untick** "Start hidden with Windows".
2. Right-click → **Exit**.
3. Delete the `LyricNow` folder.
4. (Optional) Delete saved settings and the lyrics cache: press **Win + R**, type `%LOCALAPPDATA%\LyricNow` and delete that folder.

---

## 10. Troubleshooting

**The widget says "Waiting for Spotify…" while music is playing.**
Press a volume key on your keyboard. Windows' media popup should show the Spotify song. If it doesn't, restart Spotify. LyricNow reads the same information as that popup.

**Lyrics are consistently a bit early or late on every song.**
Use the **−/+** buttons. If it's the same on every song, let the developer know, since a global offset could be added.

**I don't see the widget after logging in.**
That's expected: in startup mode it stays hidden until Spotify opens.

**Double-clicking the exe does nothing.**
LyricNow is probably already running hidden (from startup). Open Spotify and it will appear. Only one copy can run at a time.

**"Start with Windows" stopped working after moving the folder.**
Untick and re-tick the option from the exe's new location.

**Where are my settings stored?**
`%LOCALAPPDATA%\LyricNow\settings.json` (position and timing fixes) and `%LOCALAPPDATA%\LyricNow\cache\` (saved lyrics).

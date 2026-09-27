# DeepSeek Clock for Windows

A system-tray utility showing whether DeepSeek API pricing is currently peak or
off-peak, with the current Flash and V4 Pro rates and a countdown to the next
price change. The Windows counterpart to the macOS menu bar app in the parent
repository.

## Pricing schedule

- Peak: Monday-Friday, 01:00-04:00 and 06:00-10:00 UTC, excluding Chinese
  public holidays.
- Off-peak: all other times, at half price.

Times are shown in your local time zone; all calculation is done in UTC.
Chinese holiday dates use China Standard Time (UTC+08). Weekend make-up workdays
remain off-peak because DeepSeek specifies peak hours only Monday-Friday.

## Official data

The app reads model prices from [DeepSeek's public pricing page](https://api-docs.deepseek.com/quick_start/pricing/)
at startup and every six hours. No API key is needed. It also reads the
[2026 State Council holiday notice](https://www.beijing.gov.cn/zhengce/zhengcefagui/202511/t20251104_4258873.html)
from an official government site. Both pages are checked before their data is
used. The last verified copies are stored in
`%LOCALAPPDATA%\DeepSeekClock\pricing-cache.json` and `holidays-cache.json`.
If a page is unavailable or its format changes, the app keeps the verified
cached data. Bundled 2026 holiday dates and prices provide the offline fallback.
The rate card says whether its prices are currently fetched, saved, or bundled.

There is no documented public DeepSeek endpoint that reports the current
peak/off-peak state. The app calculates it from DeepSeek's published UTC rule.
The 2026 holiday notice is the only year currently included; until a later
official notice is added, the popup labels results for other years as estimates.

## Appearance

The popup follows the Windows app theme automatically. The Options menu can
also select Light or Dark independently. In system mode, the popup updates
when the Windows theme changes.

The popup uses a transparent WPF window with a content-sized height and a
420-unit width that scales with Windows DPI. All three rate rows and the footer
remain part of the layout; there is no fixed pixel-sized host around the card.

Drag the popup by its header. The pin keeps it open when focus changes;
the Options menu also controls whether it stays on top. Window position,
appearance, and these choices are saved under
`%LOCALAPPDATA%\DeepSeekClock\settings.json`.

Enable **Show timer on taskbar** in Options or the tray menu to display a
compact live countdown beside the notification area. Click the label to open
the popup or right-click it for the same menu. The label uses the primary
Windows taskbar and hides when a full-screen app covers it or the taskbar is
auto-hidden. It makes room for taskbar-owned labels such as Codex Usage Widget.

## Run

```powershell
dotnet run --project src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj
```

For visual inspection, append `-- --show` to open the popup immediately with a
taskbar entry and keep it open when focus changes. Press Escape to dismiss it.
Normal tray launches continue to dismiss the popup when it loses focus.

## Build a single executable

```powershell
dotnet publish src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

The result is `publish/DeepSeekClock.exe` — one self-contained file, no .NET
installation required on the target machine.

## Tests

```powershell
dotnet test DeepSeekClock.sln
```

## Credits

Derived from [jaibhasin/deepseek-clock](https://github.com/jaibhasin/deepseek-clock)
(MIT). The pricing rules and rate card mirror that project.

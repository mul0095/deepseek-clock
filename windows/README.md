# DeepSeek Clock for Windows

A system-tray utility showing whether DeepSeek API pricing is currently peak or
off-peak, with the current Flash and V4 Pro rates and a countdown to the next
price change. The Windows counterpart to the macOS menu bar app in the parent
repository.

## Pricing schedule

- Peak: Monday-Friday, 01:00-04:00 and 06:00-10:00 UTC.
- Off-peak: all other times, at half price.

Times are shown in your local time zone; all calculation is done in UTC.

## Appearance

The popup follows the Windows app theme automatically. Choose Light or Dark in
Windows Settings under Personalization > Colors; the popup updates when the
setting changes.

The popup uses a transparent WPF window with a content-sized height and a
420-unit width that scales with Windows DPI. All three rate rows and the footer
remain part of the layout; there is no fixed pixel-sized host around the card.

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
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
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

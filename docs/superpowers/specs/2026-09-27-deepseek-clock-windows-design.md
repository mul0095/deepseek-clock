# DeepSeek Clock for Windows — Design

Date: 2026-09-27

## Context

`jaibhasin/deepseek-clock` is a native macOS menu bar utility that shows whether
DeepSeek API pricing is currently peak or off-peak. This fork adds a Windows
version that shows the same information in the system tray (notification area).
The original Swift app and its build are left untouched.

The macOS app's business logic is already cleanly separated from its UI
(`DeepSeekSchedule.swift`, `DeepSeekPricing.swift`, `ClockModel.swift` are pure
Foundation with no AppKit/SwiftUI). This design mirrors that separation: a
dependency-free, unit-testable core library plus a thin WinForms shell.

## Goal

A personal-use Windows tray app, distributed as a single `.exe` via GitHub
Releases and described on the owner's blog.

## Scope (MVP)

In scope:

- Tray icon reflecting peak/off-peak state.
- A popup panel showing Flash and V4 Pro rates for the current phase.
- A live countdown to the next price change.
- The transition time rendered in the user's local time zone.

Out of scope for the MVP (possible later):

- Currency conversion and exchange-rate fetching.
- Notifications on phase change.
- Multiple icon designs / icon picker.
- A settings window.
- Launch at login.

## Decisions

| Decision | Choice |
| --- | --- |
| Language / runtime | C# / .NET 8 (LTS) |
| UI toolkit | WinForms (no third-party dependencies) |
| Repository layout | Fork of `jaibhasin/deepseek-clock`, Windows app under `windows/` |
| Original app | Unchanged |
| Distribution | Single-file, self-contained `.exe` from GitHub Releases |
| App name | DeepSeek Clock (`DeepSeekClock.exe`) |
| Panel content | Both models (Flash and V4 Pro) shown at once, no picker |
| Icon | Reuse the provided multi-resolution `deepseek_256.ico` as a resource |

## Repository layout

```text
windows/
  DeepSeekClock.sln
  src/
    DeepSeekClock.Core/          # net8.0 class library, no Windows/WinForms deps
    DeepSeekClock.Windows/       # net8.0-windows WinForms app
      Resources/deepseek_256.ico
  tests/
    DeepSeekClock.Core.Tests/    # xUnit, tests Core only
  README.md
```

`Core` targets `net8.0` and has no WinForms reference, so it is testable on any
runner (including Linux CI) and free of UI concerns.

## Core library (`DeepSeekClock.Core`)

A direct port of the macOS business logic. All math is in UTC; local time is
used only for display.

- `PricingPhase` — `enum { Peak, OffPeak }`.
- `DeepSeekSchedule`
  - Peak windows are `[1, 4)` and `[6, 10)` hours UTC, Monday–Friday only.
  - `IsPeak(DateTimeOffset instant) -> bool`.
  - `NextTransition(DateTimeOffset after) -> DateTimeOffset?` — the earliest
    window boundary strictly after `after`; scanning the next 9 days guarantees a
    result (e.g. after Friday 10:00 the next flip is Monday 01:00).
- `DeepSeekPricing`
  - `ModelPricing(decimal InputCacheHit, decimal InputCacheMiss, decimal Output)`.
  - `Peak(DeepSeekModel)` returns the published rates; `OffPeak` is derived as
    half of `Peak` so the two can never drift.
  - Rates use `decimal` to match the original's `Decimal` (exact base-10 money
    arithmetic).
- `DeepSeekModel` — `Flash`, `V4 Pro`, with a display name.
- `ClockState` — immutable snapshot: current phase, next transition instant,
  and a formatted countdown; built by a pure factory from a `DateTimeOffset`.

Display formatting uses `CultureInfo.CurrentCulture`. The transition time is
shown with `DateTimeOffset.ToLocalTime()`.

## Windows app (`DeepSeekClock.Windows`)

Thin UI layer over `Core`. Zero third-party packages.

- `Program.Main` — `[STAThread]`, starts a `TrayAppContext`.
- `TrayAppContext : ApplicationContext` — owns the `NotifyIcon` and the panel;
  keeps the app alive with no visible main window.
- `ClockTicker` — a `System.Windows.Forms.Timer` firing about once per second.
- `TrayIconRenderer` — composites the base `.ico` with a small phase badge
  (green for off-peak, amber for peak) drawn with GDI+, so the icon conveys
  state at a glance. Falls back to the plain `.ico` if compositing fails.
- `PopupForm` — borderless, non-resizable panel (no title bar,
  `ShowInTaskbar = false`) positioned next to the tray icon; closes when it
  loses focus.
- No persisted settings in the MVP.

### Data flow

`Timer.Tick` → `ClockState.From(DateTimeOffset.Now)` → update `NotifyIcon`
(image + tooltip) and, if open, the `PopupForm` labels. The icon and panel read
the same `ClockState`, so they cannot disagree.

### Error handling

The MVP has no network or persistence, so failure modes are minimal: guard
against a null `NextTransition` (show an empty countdown) and fall back to the
plain icon if badge compositing throws.

## Packaging and CI

- Publish: `dotnet publish -c Release -r win-x64 --self-contained
  -p:PublishSingleFile=true` produces one `.exe`.
- New workflow `.github/workflows/windows-ci.yml` on `windows-latest` runs
  `dotnet test` and `dotnet publish`. This is separate from the existing
  macOS `ci.yml`, which must keep passing unchanged.
- The app manifest requests per-monitor DPI awareness so the tray icon and panel
  render crisply.

## Testing

Port the relevant cases from `Tests/DeepSeekClockTests/` to xUnit:

- Peak/off-peak at each window boundary (start inclusive, end exclusive).
- Weekends are always off-peak.
- The Friday→Monday next-transition case.
- Off-peak is exactly half of peak.
- Countdown formatting.

Tests run in GitHub Actions, not locally, per the repository's workflow rules.

## Attribution

The Windows app is derived from `jaibhasin/deepseek-clock` (MIT). The fork keeps
the original license and credits the source project in `windows/README.md`.

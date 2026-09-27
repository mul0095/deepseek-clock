# DeepSeek Clock for Windows — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a dependency-free Windows tray app that shows DeepSeek peak/off-peak pricing, current rates, and a countdown to the next price change.

**Architecture:** A `net8.0` class library (`DeepSeekClock.Core`) holds all business logic as pure functions of a `DateTimeOffset`, mirroring the original Swift `DeepSeekSchedule`/`DeepSeekPricing`/`ClockModel` split. A thin `net8.0-windows` WinForms app (`DeepSeekClock.Windows`) owns a `NotifyIcon` plus a borderless popup panel and does no pricing math itself.

**Tech Stack:** C# 12, .NET 8, WinForms, GDI+, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-27-deepseek-clock-windows-design.md`

## Global Constraints

- Target framework: `net8.0` for Core and tests, `net8.0-windows` for the app.
- No third-party runtime dependencies. xUnit is the only test-only package.
- Money is always `decimal`, never `double`.
- All schedule math is in UTC. `TimeZoneInfo` is used only to format display text.
- App name and assembly name: `DeepSeekClock` (output `DeepSeekClock.exe`).
- Peak windows are `[1, 4)` and `[6, 10)` UTC, Monday–Friday; everything else is off-peak at half price.
- Tests run locally with `dotnet test` during development (explicitly approved) and in GitHub Actions as the final gate.
- Commit each task with the repository's `git-commiter` subagent (see `AGENTS.md`); stage only that task's files.
- Do not modify the existing Swift sources, `Package.swift`, or `.github/workflows/ci.yml`.

## File Structure

```text
windows/
  global.json
  DeepSeekClock.sln
  .gitignore                     (windows-local ignore rules; root .gitignore stays)
  src/
    DeepSeekClock.Core/
      DeepSeekClock.Core.csproj
      PricingPhase.cs            enum Peak/OffPeak
      DeepSeekModel.cs           enum Flash/Pro + DisplayName
      ModelPricing.cs            value record of three rates
      DeepSeekPricing.cs         peak/offPeak/pricing lookup
      UsdPriceFormatter.cs       decimal -> "$0.30" / "$0.006"
      DeepSeekSchedule.cs        IsPeak / NextTransition / PhaseAt
      ClockCountdown.cs          TimeSpan -> "2h 14m"
      ClockState.cs              snapshot + From(...) + FormatTransition(...)
    DeepSeekClock.Windows/
      DeepSeekClock.Windows.csproj
      Program.cs
      TrayAppContext.cs
      ClockTicker.cs
      TrayIconRenderer.cs
      PopupForm.cs
      Resources/deepseek_256.ico
  tests/
    DeepSeekClock.Core.Tests/
      DeepSeekClock.Core.Tests.csproj
      TestSupport.cs
      DeepSeekScheduleTests.cs
      DeepSeekPricingTests.cs
      ClockCountdownTests.cs
      ClockStateTests.cs
  README.md
.github/workflows/windows-ci.yml
```

## Review Focus

These are the input classes and failure modes the spec implies but that no task's unit tests fully exercise. Each is pinned by a test in its owning task; the rest are manual checks.

1. **DST and non-UTC display zones.** A transition instant must render correctly in any local zone, including zones with daylight saving. Pinned by `ClockStateTests.TransitionTextUsesRequestedTimeZone`.
2. **System clock skew / negative remaining time.** If the clock jumps or we stand on a boundary, the countdown must clamp to `0s` and never render a negative value. Pinned by `ClockCountdownTests.FormatsCountdown(-5) -> "0s"`.
3. **`NotifyIcon.Text` length limit.** Windows rejects tooltips longer than 127 characters; the tooltip must be truncated. Pinned by `TrayAppContext`'s `Tooltip` clamp (manual check in Task 5).
4. **GDI handle leak on icon repaints.** Every rendered tray icon owns a native `HICON`; replacing it without destroying the previous handle leaks. Handled by `RenderedTrayIcon.Dispose` and disposed in `TrayAppContext` (manual check in Task 6).
5. **Culture-dependent number formatting.** Prices must always render as `$` with `.` decimals regardless of the user's locale. Pinned by `DeepSeekPricingTests.PriceFormatting`.

---

### Task 1: Windows solution scaffold

**Files:**
- Create: `windows/global.json`
- Create: `windows/DeepSeekClock.sln`
- Create: `windows/.gitignore`
- Create: `windows/src/DeepSeekClock.Core/DeepSeekClock.Core.csproj`
- Create: `windows/src/DeepSeekClock.Core/PricingPhase.cs`
- Create: `windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
- Create: `windows/tests/DeepSeekClock.Core.Tests/SmokeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the build system; namespace `DeepSeekClock.Core` with `enum PricingPhase { Peak, OffPeak }`; namespace `DeepSeekClock.Core.Tests` for all test files.

- [ ] **Step 1: Scaffold the projects with the .NET CLI**

Run from the repository root:

```powershell
New-Item -ItemType Directory -Force -Path windows | Out-Null
Set-Content -Path windows/global.json -Value '{ "sdk": { "version": "8.0.425", "rollForward": "latestFeature" } }'
dotnet new sln -n DeepSeekClock -o windows
dotnet new classlib -n DeepSeekClock.Core -o windows/src/DeepSeekClock.Core -f net8.0
dotnet new xunit -n DeepSeekClock.Core.Tests -o windows/tests/DeepSeekClock.Core.Tests -f net8.0
Remove-Item windows/src/DeepSeekClock.Core/Class1.cs
Remove-Item windows/tests/DeepSeekClock.Core.Tests/UnitTest1.cs
dotnet sln windows/DeepSeekClock.sln add windows/src/DeepSeekClock.Core/DeepSeekClock.Core.csproj
dotnet sln windows/DeepSeekClock.sln add windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj
dotnet add windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj reference windows/src/DeepSeekClock.Core/DeepSeekClock.Core.csproj
```

- [ ] **Step 2: Pin the Core project properties**

Replace `windows/src/DeepSeekClock.Core/DeepSeekClock.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>DeepSeekClock.Core</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
```

- [ ] **Step 3: Add the `PricingPhase` enum**

Create `windows/src/DeepSeekClock.Core/PricingPhase.cs`:

```csharp
namespace DeepSeekClock.Core;

/// <summary>The two pricing phases DeepSeek can be in at any moment.</summary>
public enum PricingPhase
{
    /// <summary>Full price.</summary>
    Peak,

    /// <summary>50% discount.</summary>
    OffPeak,
}
```

- [ ] **Step 4: Verify the Core project builds**

Run: `dotnet build windows/src/DeepSeekClock.Core/DeepSeekClock.Core.csproj -c Release`
Expected: `Build succeeded` with 0 warnings, 0 errors.

- [ ] **Step 5: Write the failing smoke test**

Create `windows/tests/DeepSeekClock.Core.Tests/SmokeTests.cs`:

```csharp
using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void PricingPhaseHasExactlyTwoValues()
    {
        Assert.Equal(2, System.Enum.GetValues<PricingPhase>().Length);
    }
}
```

- [ ] **Step 6: Run the test to verify the harness works**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: PASS, 1 test.

- [ ] **Step 7: Add Windows-local ignore rules**

Create `windows/.gitignore`:

```gitignore
bin/
obj/
.vs/
*.user
TestResults/
```

- [ ] **Step 8: Commit**

Stage only the new files:

```powershell
git add windows
```

Then invoke the `git-commiter` subagent.
Suggested message: `chore(windows): scaffold .NET solution for Windows app`

---

### Task 2: Peak/off-peak schedule engine

**Files:**
- Create: `windows/src/DeepSeekClock.Core/DeepSeekSchedule.cs`
- Create: `windows/tests/DeepSeekClock.Core.Tests/TestSupport.cs`
- Create: `windows/tests/DeepSeekClock.Core.Tests/DeepSeekScheduleTests.cs`

**Interfaces:**
- Consumes: `PricingPhase` (Task 1).
- Produces: `static class DeepSeekSchedule` with
  `bool IsPeak(DateTimeOffset instant)`,
  `DateTimeOffset? NextTransition(DateTimeOffset after)`,
  `PricingPhase PhaseAt(DateTimeOffset instant)`;
  test helper `static DateTimeOffset TestSupport.Utc(int year, int month, int day, int hour, int minute = 0, int second = 0)`
  and `static DateTimeOffset TestSupport.InZone(string timeZoneId, int year, int month, int day, int hour, int minute = 0, int second = 0)`.

- [ ] **Step 1: Write the shared test helpers**

Create `windows/tests/DeepSeekClock.Core.Tests/TestSupport.cs`:

```csharp
using System;

namespace DeepSeekClock.Core.Tests;

/// <summary>Date-building helpers so each test reads as the behavior it checks.</summary>
internal static class TestSupport
{
    /// <summary>Builds an instant from components interpreted in UTC.</summary>
    public static DateTimeOffset Utc(int year, int month, int day,
                                     int hour, int minute = 0, int second = 0)
        => new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));

    /// <summary>Builds an instant from wall-clock components in a given IANA zone.</summary>
    public static DateTimeOffset InZone(string timeZoneId, int year, int month, int day,
                                        int hour, int minute = 0, int second = 0)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var wall = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }
}
```

- [ ] **Step 2: Write the failing schedule tests**

Create `windows/tests/DeepSeekClock.Core.Tests/DeepSeekScheduleTests.cs`:

```csharp
using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

// Reference dates (2026): Sep 21 = Mon, 25 = Fri, 26 = Sat, 27 = Sun, 28 = Mon.
public class DeepSeekScheduleTests
{
    [Fact]
    public void PeakWindowBoundariesOnMonday()
    {
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 0, 59)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 1, 0)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 3, 59)));
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 4, 0)));
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 5, 59)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 6, 0)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 9, 59)));
        Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, 10, 0)));
    }

    [Theory]
    [InlineData(21)] // Mon
    [InlineData(22)] // Tue
    [InlineData(23)] // Wed
    [InlineData(24)] // Thu
    [InlineData(25)] // Fri
    public void EveryWeekdayIsPeakInsideBothWindows(int day)
    {
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, day, 2, 0)));
        Assert.True(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, day, 7, 0)));
    }

    [Theory]
    [InlineData(26)] // Sat
    [InlineData(27)] // Sun
    public void WeekendsAreAlwaysOffPeak(int day)
    {
        foreach (var hour in new[] { 0, 2, 7, 12, 23 })
            Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, day, hour, 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(18)]
    [InlineData(23)]
    public void NonPeakWeekdayHoursAreOffPeak(int hour)
        => Assert.False(DeepSeekSchedule.IsPeak(TestSupport.Utc(2026, 9, 21, hour, 0)));

    [Fact]
    public void TransitionWithinFirstWindow()
        => Assert.Equal(TestSupport.Utc(2026, 9, 21, 4, 0),
                        DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 2, 0)));

    [Fact]
    public void TransitionBetweenWindows()
        => Assert.Equal(TestSupport.Utc(2026, 9, 21, 6, 0),
                        DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 4, 30)));

    [Fact]
    public void TransitionAfterLastWindowJumpsToNextDay()
        => Assert.Equal(TestSupport.Utc(2026, 9, 22, 1, 0),
                        DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 10, 30)));

    [Fact]
    public void TransitionFromFridayEveningJumpsToMonday()
        => Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0),
                        DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 25, 10, 30)));

    [Fact]
    public void TransitionAcrossWeekend()
    {
        Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0),
                     DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 26, 12, 0)));
        Assert.Equal(TestSupport.Utc(2026, 9, 28, 1, 0),
                     DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 27, 23, 0)));
    }

    [Fact]
    public void TransitionIsStrictlyAfterTheGivenDate()
        => Assert.Equal(TestSupport.Utc(2026, 9, 21, 4, 0),
                        DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, 1, 0)));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(21)]
    public void TransitionAlwaysFound(int hour)
        => Assert.NotNull(DeepSeekSchedule.NextTransition(TestSupport.Utc(2026, 9, 21, hour, 0)));

    [Fact]
    public void CalculationIsIndependentOfDescribingTimeZone()
    {
        // 2026-09-21 11:00 in Tokyo (UTC+9) is the same instant as 02:00 UTC.
        var peakInstant = TestSupport.InZone("Asia/Tokyo", 2026, 9, 21, 11, 0);
        Assert.Equal(TestSupport.Utc(2026, 9, 21, 2, 0), peakInstant);
        Assert.True(DeepSeekSchedule.IsPeak(peakInstant));

        var offPeakInstant = TestSupport.InZone("Asia/Tokyo", 2026, 9, 21, 14, 0);
        Assert.Equal(TestSupport.Utc(2026, 9, 21, 5, 0), offPeakInstant);
        Assert.False(DeepSeekSchedule.IsPeak(offPeakInstant));
    }

    [Fact]
    public void PhaseAtMapsIsPeakToTheEnum()
    {
        Assert.Equal(PricingPhase.Peak, DeepSeekSchedule.PhaseAt(TestSupport.Utc(2026, 9, 21, 2, 0)));
        Assert.Equal(PricingPhase.OffPeak, DeepSeekSchedule.PhaseAt(TestSupport.Utc(2026, 9, 21, 5, 0)));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: FAIL to compile, `The name 'DeepSeekSchedule' does not exist`.

- [ ] **Step 4: Implement the schedule**

Create `windows/src/DeepSeekClock.Core/DeepSeekSchedule.cs`:

```csharp
namespace DeepSeekClock.Core;

/// <summary>
/// Pure logic: is DeepSeek charging peak prices right now, and when does that
/// change? Every rule is defined in UTC, never in the local zone.
/// </summary>
public static class DeepSeekSchedule
{
    /// <summary>A peak window as [StartHour, EndHour) in 24-hour UTC.</summary>
    public readonly record struct Window(int StartHour, int EndHour)
    {
        public bool Contains(int hour) => hour >= StartHour && hour < EndHour;
    }

    /// <summary>Peak windows: 01:00-04:00 and 06:00-10:00 UTC.</summary>
    public static readonly IReadOnlyList<Window> PeakWindows =
        new[] { new Window(1, 4), new Window(6, 10) };

    /// <summary>The pricing phase in effect at <paramref name="instant"/>.</summary>
    public static PricingPhase PhaseAt(DateTimeOffset instant)
        => IsPeak(instant) ? PricingPhase.Peak : PricingPhase.OffPeak;

    /// <summary>True when <paramref name="instant"/> falls in a weekday peak window.</summary>
    public static bool IsPeak(DateTimeOffset instant)
    {
        var utc = instant.UtcDateTime;

        // Weekends are always off-peak.
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        foreach (var window in PeakWindows)
            if (window.Contains(utc.Hour))
                return true;

        return false;
    }

    /// <summary>
    /// The earliest window boundary strictly after <paramref name="after"/>.
    /// Scanning 9 days always finds one (e.g. after Friday 10:00 the next flip
    /// is Monday 01:00).
    /// </summary>
    public static DateTimeOffset? NextTransition(DateTimeOffset after)
    {
        var utc = after.UtcDateTime;
        var startOfToday = new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
        DateTimeOffset? best = null;

        for (var dayOffset = 0; dayOffset < 9; dayOffset++)
        {
            var day = startOfToday.AddDays(dayOffset);

            // No window starts or ends on a weekend day.
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            foreach (var window in PeakWindows)
            {
                foreach (var hour in new[] { window.StartHour, window.EndHour })
                {
                    var instant = new DateTimeOffset(day.AddHours(hour), TimeSpan.Zero);
                    if (instant > after && (best is null || instant < best.Value))
                        best = instant;
                }
            }
        }

        return best;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: PASS, 28 tests (1 smoke + schedule cases).

- [ ] **Step 6: Commit**

```powershell
git add windows/src/DeepSeekClock.Core/DeepSeekSchedule.cs windows/tests/DeepSeekClock.Core.Tests/TestSupport.cs windows/tests/DeepSeekClock.Core.Tests/DeepSeekScheduleTests.cs
```

Invoke the `git-commiter` subagent.
Suggested message: `feat(windows): add UTC peak/off-peak schedule engine`

---

### Task 3: Rate card and USD formatting

**Files:**
- Create: `windows/src/DeepSeekClock.Core/DeepSeekModel.cs`
- Create: `windows/src/DeepSeekClock.Core/ModelPricing.cs`
- Create: `windows/src/DeepSeekClock.Core/DeepSeekPricing.cs`
- Create: `windows/src/DeepSeekClock.Core/UsdPriceFormatter.cs`
- Create: `windows/tests/DeepSeekClock.Core.Tests/DeepSeekPricingTests.cs`

**Interfaces:**
- Consumes: `PricingPhase` (Task 1).
- Produces: `enum DeepSeekModel { Flash, Pro }`;
  `static string DeepSeekModelExtensions.DisplayName(this DeepSeekModel)`;
  `readonly record struct ModelPricing(decimal InputCacheHit, decimal InputCacheMiss, decimal Output)`;
  `static class DeepSeekPricing` with `ModelPricing Peak(DeepSeekModel)`, `ModelPricing OffPeak(DeepSeekModel)`, `ModelPricing Pricing(DeepSeekModel, PricingPhase)`;
  `static class UsdPriceFormatter` with `string Format(decimal)`.

- [ ] **Step 1: Write the failing pricing tests**

Create `windows/tests/DeepSeekClock.Core.Tests/DeepSeekPricingTests.cs`:

```csharp
using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class DeepSeekPricingTests
{
    public static TheoryData<DeepSeekModel> AllModels => new() { DeepSeekModel.Flash, DeepSeekModel.Pro };

    [Theory]
    [MemberData(nameof(AllModels))]
    public void OffPeakIsExactlyHalfOfPeak(DeepSeekModel model)
    {
        var peak = DeepSeekPricing.Peak(model);
        var offPeak = DeepSeekPricing.OffPeak(model);

        Assert.Equal(peak.InputCacheHit / 2m, offPeak.InputCacheHit);
        Assert.Equal(peak.InputCacheMiss / 2m, offPeak.InputCacheMiss);
        Assert.Equal(peak.Output / 2m, offPeak.Output);
    }

    [Fact]
    public void FlashPeakRates()
    {
        var pricing = DeepSeekPricing.Peak(DeepSeekModel.Flash);
        Assert.Equal(0.006m, pricing.InputCacheHit);
        Assert.Equal(0.30m, pricing.InputCacheMiss);
        Assert.Equal(1.20m, pricing.Output);
    }

    [Fact]
    public void ProPeakRates()
    {
        var pricing = DeepSeekPricing.Peak(DeepSeekModel.Pro);
        Assert.Equal(0.044m, pricing.InputCacheHit);
        Assert.Equal(1.32m, pricing.InputCacheMiss);
        Assert.Equal(3.96m, pricing.Output);
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void PricingResolvesByPhase(DeepSeekModel model)
    {
        Assert.Equal(DeepSeekPricing.Peak(model), DeepSeekPricing.Pricing(model, PricingPhase.Peak));
        Assert.Equal(DeepSeekPricing.OffPeak(model), DeepSeekPricing.Pricing(model, PricingPhase.OffPeak));
    }

    [Theory]
    [InlineData("0.30", "$0.30")]
    [InlineData("0.006", "$0.006")]
    [InlineData("3.96", "$3.96")]
    [InlineData("0.003", "$0.003")]
    [InlineData("0.15", "$0.15")]
    public void PriceFormatting(string value, string expected)
        => Assert.Equal(expected, UsdPriceFormatter.Format(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ModelDisplayNames()
    {
        Assert.Equal(new[] { DeepSeekModel.Flash, DeepSeekModel.Pro }, System.Enum.GetValues<DeepSeekModel>());
        Assert.Equal("Flash", DeepSeekModel.Flash.DisplayName());
        Assert.Equal("V4 Pro", DeepSeekModel.Pro.DisplayName());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: FAIL to compile, `The type or namespace name 'DeepSeekModel' could not be found`.

- [ ] **Step 3: Implement the model, rates, and formatter**

Create `windows/src/DeepSeekClock.Core/DeepSeekModel.cs`:

```csharp
namespace DeepSeekClock.Core;

/// <summary>The DeepSeek models this app can show prices for.</summary>
public enum DeepSeekModel
{
    Flash,
    Pro,
}

public static class DeepSeekModelExtensions
{
    /// <summary>Short, friendly name shown in the panel.</summary>
    public static string DisplayName(this DeepSeekModel model) => model switch
    {
        DeepSeekModel.Flash => "Flash",
        DeepSeekModel.Pro => "V4 Pro",
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };
}
```

Create `windows/src/DeepSeekClock.Core/ModelPricing.cs`:

```csharp
namespace DeepSeekClock.Core;

/// <summary>The three billable meters, in USD per 1M tokens.</summary>
public readonly record struct ModelPricing(decimal InputCacheHit, decimal InputCacheMiss, decimal Output);
```

Create `windows/src/DeepSeekClock.Core/DeepSeekPricing.cs`:

```csharp
using System.Globalization;

namespace DeepSeekClock.Core;

/// <summary>
/// The single source of truth for DeepSeek's published rates. Off-peak is
/// derived as half of peak so the two can never drift apart.
/// </summary>
public static class DeepSeekPricing
{
    /// <summary>Peak (full price) rates, in USD per 1M tokens.</summary>
    public static ModelPricing Peak(DeepSeekModel model) => model switch
    {
        DeepSeekModel.Flash => new ModelPricing(Usd("0.006"), Usd("0.30"), Usd("1.20")),
        DeepSeekModel.Pro   => new ModelPricing(Usd("0.044"), Usd("1.32"), Usd("3.96")),
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };

    /// <summary>Off-peak rates: exactly half of peak.</summary>
    public static ModelPricing OffPeak(DeepSeekModel model)
    {
        var peak = Peak(model);
        return new ModelPricing(peak.InputCacheHit / 2m,
                                peak.InputCacheMiss / 2m,
                                peak.Output / 2m);
    }

    /// <summary>Rates for <paramref name="model"/> during <paramref name="phase"/>.</summary>
    public static ModelPricing Pricing(DeepSeekModel model, PricingPhase phase) => phase switch
    {
        PricingPhase.Peak => Peak(model),
        PricingPhase.OffPeak => OffPeak(model),
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static decimal Usd(string value)
        => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
```

Create `windows/src/DeepSeekClock.Core/UsdPriceFormatter.cs`:

```csharp
using System.Globalization;

namespace DeepSeekClock.Core;

/// <summary>
/// Formats a USD amount for display: two decimals for normal prices, three for
/// sub-cent rates. Always "$0.30" / "$0.006", independent of the user's locale.
/// </summary>
public static class UsdPriceFormatter
{
    public static string Format(decimal value)
    {
        var format = value == decimal.Round(value, 2) ? "F2" : "F3";
        return "$" + value.ToString(format, CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add windows/src/DeepSeekClock.Core/DeepSeekModel.cs windows/src/DeepSeekClock.Core/ModelPricing.cs windows/src/DeepSeekClock.Core/DeepSeekPricing.cs windows/src/DeepSeekClock.Core/UsdPriceFormatter.cs windows/tests/DeepSeekClock.Core.Tests/DeepSeekPricingTests.cs
```

Invoke the `git-commiter` subagent.
Suggested message: `feat(windows): add DeepSeek rate card and USD formatting`

---

### Task 4: Countdown and clock state

**Files:**
- Create: `windows/src/DeepSeekClock.Core/ClockCountdown.cs`
- Create: `windows/src/DeepSeekClock.Core/ClockState.cs`
- Create: `windows/tests/DeepSeekClock.Core.Tests/ClockCountdownTests.cs`
- Create: `windows/tests/DeepSeekClock.Core.Tests/ClockStateTests.cs`

**Interfaces:**
- Consumes: `DeepSeekSchedule` (Task 2), `PricingPhase` (Task 1).
- Produces: `static class ClockCountdown` with `string Format(TimeSpan)`;
  `sealed record ClockState(PricingPhase Phase, DateTimeOffset? NextTransition, string Countdown, string? TransitionText)`
  with `static ClockState From(DateTimeOffset now, TimeZoneInfo displayZone)` and
  `static string FormatTransition(DateTimeOffset instant, TimeZoneInfo zone)`.

- [ ] **Step 1: Write the failing countdown tests**

Create `windows/tests/DeepSeekClock.Core.Tests/ClockCountdownTests.cs`:

```csharp
using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class ClockCountdownTests
{
    [Theory]
    [InlineData(8040, "2h 14m")]
    [InlineData(3600, "1h 0m")]
    [InlineData(125, "2m 5s")]
    [InlineData(60, "1m 0s")]
    [InlineData(12, "12s")]
    [InlineData(59, "59s")]
    [InlineData(-5, "0s")]
    public void FormatsCountdown(double seconds, string expected)
        => Assert.Equal(expected, ClockCountdown.Format(TimeSpan.FromSeconds(seconds)));
}
```

- [ ] **Step 2: Write the failing clock-state tests**

Create `windows/tests/DeepSeekClock.Core.Tests/ClockStateTests.cs`:

```csharp
using DeepSeekClock.Core;
using Xunit;

namespace DeepSeekClock.Core.Tests;

public class ClockStateTests
{
    [Fact]
    public void PhaseMatchesTheSchedule()
    {
        Assert.Equal(PricingPhase.Peak,
            ClockState.From(TestSupport.Utc(2026, 9, 21, 2, 0), TimeZoneInfo.Utc).Phase);
        Assert.Equal(PricingPhase.OffPeak,
            ClockState.From(TestSupport.Utc(2026, 9, 21, 5, 0), TimeZoneInfo.Utc).Phase);
    }

    [Fact]
    public void CountdownMatchesTheNextTransition()
    {
        var state = ClockState.From(TestSupport.Utc(2026, 9, 21, 2, 0), TimeZoneInfo.Utc);
        Assert.Equal(TestSupport.Utc(2026, 9, 21, 4, 0), state.NextTransition);
        Assert.Equal("2h 0m", state.Countdown);
    }

    [Fact]
    public void TransitionTextUsesRequestedTimeZone()
    {
        var transition = TestSupport.Utc(2026, 9, 21, 4, 0);
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

        var kolkataText = ClockState.FormatTransition(transition, kolkata);
        var losAngelesText = ClockState.FormatTransition(transition, losAngeles);

        Assert.False(string.IsNullOrEmpty(kolkataText));
        Assert.NotEqual(kolkataText, losAngelesText);
    }

    [Fact]
    public void TransitionTextIsNullWhenThereIsNoTransition()
    {
        // Drive the record directly: a null transition must produce null text.
        var state = new ClockState(PricingPhase.OffPeak, null, "0s", null);
        Assert.Null(state.TransitionText);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: FAIL to compile, `The name 'ClockCountdown' does not exist`.

- [ ] **Step 4: Implement the countdown formatter**

Create `windows/src/DeepSeekClock.Core/ClockCountdown.cs`:

```csharp
namespace DeepSeekClock.Core;

/// <summary>Turns a duration into a short string that fits the panel.</summary>
public static class ClockCountdown
{
    /// <summary>8040s -> "2h 14m", 125s -> "2m 5s", 12s -> "12s".</summary>
    public static string Format(TimeSpan interval)
    {
        var total = (int)Math.Max(0, Math.Round(interval.TotalSeconds, MidpointRounding.AwayFromZero));
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var seconds = total % 60;

        if (hours > 0) return $"{hours}h {minutes}m";
        if (minutes > 0) return $"{minutes}m {seconds}s";
        return $"{seconds}s";
    }
}
```

- [ ] **Step 5: Implement the clock state**

Create `windows/src/DeepSeekClock.Core/ClockState.cs`:

```csharp
using System.Globalization;

namespace DeepSeekClock.Core;

/// <summary>
/// An immutable snapshot of everything the tray icon and panel display: the
/// current phase, the next transition instant, a formatted countdown, and the
/// transition rendered in the display zone. Pure data plus a pure factory.
/// </summary>
public sealed record ClockState(
    PricingPhase Phase,
    DateTimeOffset? NextTransition,
    string Countdown,
    string? TransitionText)
{
    /// <summary>Builds the state for the instant <paramref name="now"/>.</summary>
    public static ClockState From(DateTimeOffset now, TimeZoneInfo displayZone)
    {
        var phase = DeepSeekSchedule.PhaseAt(now);
        var next = DeepSeekSchedule.NextTransition(now);
        var remaining = next is { } n ? n - now : TimeSpan.Zero;

        return new ClockState(
            phase,
            next,
            ClockCountdown.Format(remaining),
            next is { } t ? FormatTransition(t, displayZone) : null);
    }

    /// <summary>Formats an instant as short wall-clock time in <paramref name="zone"/>.</summary>
    public static string FormatTransition(DateTimeOffset instant, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(instant, zone).ToString("t", CultureInfo.CurrentCulture);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test windows/tests/DeepSeekClock.Core.Tests/DeepSeekClock.Core.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add windows/src/DeepSeekClock.Core/ClockCountdown.cs windows/src/DeepSeekClock.Core/ClockState.cs windows/tests/DeepSeekClock.Core.Tests/ClockCountdownTests.cs windows/tests/DeepSeekClock.Core.Tests/ClockStateTests.cs
```

Invoke the `git-commiter` subagent.
Suggested message: `feat(windows): add countdown formatting and clock state`

---

### Task 5: Tray application shell

**Files:**
- Create: `windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj` (via CLI, then replace)
- Create: `windows/src/DeepSeekClock.Windows/Program.cs`
- Create: `windows/src/DeepSeekClock.Windows/ClockTicker.cs`
- Create: `windows/src/DeepSeekClock.Windows/TrayIconRenderer.cs`
- Create: `windows/src/DeepSeekClock.Windows/RenderedTrayIcon.cs`
- Create: `windows/src/DeepSeekClock.Windows/TrayAppContext.cs`
- Create: `windows/src/DeepSeekClock.Windows/Resources/deepseek_256.ico` (copy)
- Modify: `windows/DeepSeekClock.sln`

**Interfaces:**
- Consumes: `ClockState`, `PricingPhase` (Tasks 2–4).
- Produces: a running app with a tray icon and a phase/countdown tooltip; `ClockTicker` exposing `event Action<ClockState> Tick` and `void Start()`;
  `TrayAppContext.Tooltip(ClockState) -> string` (internal static, clamped to 127 chars).

- [ ] **Step 1: Create the app project and reference Core**

Run from the repository root:

```powershell
dotnet new winforms -n DeepSeekClock.Windows -o windows/src/DeepSeekClock.Windows -f net8.0
Remove-Item windows/src/DeepSeekClock.Windows/Form1.cs
Remove-Item windows/src/DeepSeekClock.Windows/Form1.Designer.cs
Remove-Item windows/src/DeepSeekClock.Windows/Form1.resx
Remove-Item windows/src/DeepSeekClock.Windows/Program.cs
dotnet sln windows/DeepSeekClock.sln add windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj
dotnet add windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj reference windows/src/DeepSeekClock.Core/DeepSeekClock.Core.csproj
```

- [ ] **Step 2: Copy the icon into the project**

```powershell
New-Item -ItemType Directory -Force -Path windows/src/DeepSeekClock.Windows/Resources | Out-Null
Copy-Item -LiteralPath "C:\Users\mul0\Pictures\Application icon\deepseek_256.ico" -Destination windows/src/DeepSeekClock.Windows/Resources/deepseek_256.ico -Force
```

- [ ] **Step 3: Configure the app project**

Replace `windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RootNamespace>DeepSeekClock.Windows</RootNamespace>
    <AssemblyName>DeepSeekClock</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <ApplicationIcon>Resources\deepseek_256.ico</ApplicationIcon>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <EmbeddedResource Include="Resources\deepseek_256.ico">
      <LogicalName>deepseek_256.ico</LogicalName>
    </EmbeddedResource>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DeepSeekClock.Core\DeepSeekClock.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Write the ticker**

Create `windows/src/DeepSeekClock.Windows/ClockTicker.cs`:

```csharp
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>
/// Fires about once a second with a freshly computed <see cref="ClockState"/>.
/// All pricing math lives in Core; this only decides *when* to recompute.
/// </summary>
internal sealed class ClockTicker : IDisposable
{
    // Fully qualified on purpose: WinForms implicit usings also import
    // System.Threading, which would make a bare `Timer` ambiguous.
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly TimeZoneInfo _displayZone = TimeZoneInfo.Local;

    public event Action<ClockState>? Tick;

    public ClockTicker()
    {
        _timer.Tick += (_, _) => Raise();
    }

    /// <summary>Paints once immediately, then starts the one-second cadence.</summary>
    public void Start()
    {
        Raise();
        _timer.Start();
    }

    private void Raise() => Tick?.Invoke(ClockState.From(DateTimeOffset.Now, _displayZone));

    public void Dispose() => _timer.Dispose();
}
```

- [ ] **Step 5: Write the tray context**

Create `windows/src/DeepSeekClock.Windows/TrayAppContext.cs`:

```csharp
using System.Windows.Forms;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>
/// Owns the tray icon and keeps the app alive without a main window. The panel
/// is added in a later task; here the icon shows the phase and a tooltip.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private const int MaxTooltipLength = 127;

    private readonly NotifyIcon _notifyIcon;
    private readonly ClockTicker _ticker = new();
    private RenderedTrayIcon? _currentIcon;
    private PricingPhase? _paintedPhase;

    public TrayAppContext()
    {
        _currentIcon = TrayIconRenderer.Render(PricingPhase.OffPeak);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon.Icon,
            Text = "DeepSeek Clock",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _ticker.Tick += (_, state) => UpdateUi(state);
        _ticker.Start();
    }

    /// <summary>Formats the tray tooltip, clamped to Windows' length limit.</summary>
    internal static string Tooltip(ClockState state)
    {
        var phase = state.Phase == PricingPhase.Peak ? "Peak pricing" : "Off-peak · 50% off";
        var text = $"{phase}\nChanges in {state.Countdown}";
        if (state.TransitionText is { } at)
            text += $" at {at}";
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    private void UpdateUi(ClockState state)
    {
        if (_paintedPhase != state.Phase)
        {
            // Swap the icon, then release the previous native handle.
            var rendered = TrayIconRenderer.Render(state.Phase);
            _notifyIcon.Icon = rendered.Icon;
            _currentIcon?.Dispose();
            _currentIcon = rendered;
            _paintedPhase = state.Phase;
        }

        _notifyIcon.Text = Tooltip(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ticker.Dispose();
            _currentIcon?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
```

- [ ] **Step 6: Write the icon renderer (plain icon for now)**

Create `windows/src/DeepSeekClock.Windows/TrayIconRenderer.cs`:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Builds the tray icon for a given pricing phase.</summary>
internal static class TrayIconRenderer
{
    private static readonly Lazy<Icon> BaseIcon = new(LoadBaseIcon);

    public static RenderedTrayIcon Render(PricingPhase phase)
    {
        var size = SystemInformation.SmallIconSize;
        var bitmap = new Bitmap(size.Width, size.Height);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawIcon(BaseIcon.Value, new Rectangle(0, 0, size.Width, size.Height));
        }

        return new RenderedTrayIcon(bitmap);
    }

    private static Icon LoadBaseIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("deepseek_256.ico")
            ?? throw new InvalidOperationException("Embedded resource 'deepseek_256.ico' was not found.");
        return new Icon(stream);
    }
}
```

- [ ] **Step 7: Write the owned-icon wrapper**

Create `windows/src/DeepSeekClock.Windows/RenderedTrayIcon.cs`:

```csharp
using System.Drawing;
using System.Runtime.InteropServices;

namespace DeepSeekClock.Windows;

/// <summary>
/// An <see cref="Icon"/> whose native HICON must be destroyed when replaced,
/// otherwise every repaint leaks a GDI handle.
/// </summary>
internal sealed class RenderedTrayIcon : IDisposable
{
    private readonly nint _handle;

    public RenderedTrayIcon(Bitmap bitmap)
    {
        _handle = bitmap.GetHicon();
        Icon = Icon.FromHandle(_handle);
        bitmap.Dispose();
    }

    public Icon Icon { get; }

    public void Dispose()
    {
        Icon.Dispose();
        DestroyIcon(_handle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint handle);
}
```

- [ ] **Step 8: Write the entry point**

Create `windows/src/DeepSeekClock.Windows/Program.cs`:

```csharp
namespace DeepSeekClock.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}
```

- [ ] **Step 9: Build and run**

Run: `dotnet build windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj -c Release`
Expected: `Build succeeded`.

Then run: `dotnet run --project windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj -c Release`

Manual verification:
- A tray icon appears in the notification area.
- Hovering it shows a tooltip with "Peak pricing" or "Off-peak · 50% off" and "Changes in ...".
- Right-click → Exit closes the app.

- [ ] **Step 10: Commit**

```powershell
git add windows/src/DeepSeekClock.Windows windows/DeepSeekClock.sln
```

Invoke the `git-commiter` subagent.
Suggested message: `feat(windows): add tray app shell with phase tooltip`

Note: `RenderedTrayIcon` lives in its own file so Task 6 can extend rendering without touching the tray context.

---

### Task 6: Phase badge on the tray icon

**Files:**
- Modify: `windows/src/DeepSeekClock.Windows/TrayIconRenderer.cs`

**Interfaces:**
- Consumes: everything from Task 5.
- Produces: `TrayIconRenderer.Render` draws a coloured badge (amber for peak, green for off-peak) composited onto the base icon, falling back to a plain copy on drawing failure.

- [ ] **Step 1: Add badge drawing to the renderer**

Replace `windows/src/DeepSeekClock.Windows/TrayIconRenderer.cs`:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>Builds the tray icon for a given pricing phase.</summary>
internal static class TrayIconRenderer
{
    private static readonly Lazy<Icon> BaseIcon = new(LoadBaseIcon);

    /// <summary>
    /// Composites the base icon with a small phase badge so the state is
    /// readable at a glance. Falls back to a plain copy if drawing fails.
    /// </summary>
    public static RenderedTrayIcon Render(PricingPhase phase)
    {
        var size = SystemInformation.SmallIconSize;
        var bitmap = new Bitmap(size.Width, size.Height);

        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawIcon(BaseIcon.Value, new Rectangle(0, 0, size.Width, size.Height));
            DrawBadge(graphics, phase, size);
        }
        catch
        {
            // Worst case: the caller still gets a usable (unbadged) icon.
            using var graphics = Graphics.FromImage(bitmap);
            graphics.DrawIcon(BaseIcon.Value, new Rectangle(0, 0, size.Width, size.Height));
        }

        return new RenderedTrayIcon(bitmap);
    }

    private static void DrawBadge(Graphics graphics, PricingPhase phase, Size size)
    {
        var color = phase == PricingPhase.Peak
            ? Color.FromArgb(255, 176, 32)   // amber
            : Color.FromArgb(42, 178, 110);  // green

        var diameter = Math.Max(6, size.Width / 2);
        var rect = new Rectangle(size.Width - diameter, size.Height - diameter,
                                 diameter - 1, diameter - 1);

        using var fill = new SolidBrush(color);
        using var outline = new Pen(Color.FromArgb(200, 20, 20, 20), 1f);
        graphics.FillEllipse(fill, rect);
        graphics.DrawEllipse(outline, rect);
    }

    private static Icon LoadBaseIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("deepseek_256.ico")
            ?? throw new InvalidOperationException("Embedded resource 'deepseek_256.ico' was not found.");
        return new Icon(stream);
    }
}
```

- [ ] **Step 2: Build and run**

Run: `dotnet run --project windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj -c Release`

Manual verification:
- The tray icon shows a small coloured dot: green while off-peak, amber during a peak window.
- Because "today" is a Sunday (2026-09-27), the badge should be green. To see amber, temporarily replace `DateTimeOffset.Now` in `ClockTicker.Raise` with a fixed peak instant, e.g. `new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero)`. Revert after checking.

- [ ] **Step 3: Commit**

```powershell
git add windows/src/DeepSeekClock.Windows/TrayIconRenderer.cs
```

Invoke the `git-commiter` subagent.
Suggested message: `feat(windows): show pricing phase as a tray icon badge`

---

### Task 7: Popup panel with rates and countdown

**Files:**
- Create: `windows/src/DeepSeekClock.Windows/PopupForm.cs`
- Modify: `windows/src/DeepSeekClock.Windows/TrayAppContext.cs`

**Interfaces:**
- Consumes: `ClockState`, `DeepSeekPricing`, `UsdPriceFormatter`, `DeepSeekModel` (Tasks 3–4).
- Produces: `PopupForm` with `void ShowState(ClockState)` and `void ShowNearCursor()`; `TrayAppContext` toggles it on left-click.

- [ ] **Step 1: Write the panel**

Create `windows/src/DeepSeekClock.Windows/PopupForm.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;
using DeepSeekClock.Core;

namespace DeepSeekClock.Windows;

/// <summary>
/// The borderless panel shown when the tray icon is clicked: current phase,
/// a live countdown, and the Flash / V4 Pro rate cards.
/// </summary>
internal sealed class PopupForm : Form
{
    private const int RowWidth = 236;

    private readonly Label _phase = new()
    {
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
    };

    private readonly Label _countdown = new() { AutoSize = true };

    private readonly Label _transition = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
    };

    private readonly FlowLayoutPanel _content = new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };

    private readonly List<(Label Label, DeepSeekModel Model, Meter Kind)> _priceLabels = new();

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        BackColor = SystemColors.Window;

        Controls.Add(_content);
        _content.Controls.Add(_phase);
        _content.Controls.Add(_countdown);
        _content.Controls.Add(_transition);

        foreach (var model in new[] { DeepSeekModel.Flash, DeepSeekModel.Pro })
            AddModel(model);
    }

    /// <summary>Updates every label from a fresh state.</summary>
    public void ShowState(ClockState state)
    {
        var peak = state.Phase == PricingPhase.Peak;
        _phase.Text = peak ? "Peak pricing" : "Off-peak · 50% off";
        _phase.ForeColor = peak ? Color.FromArgb(176, 112, 0) : Color.FromArgb(30, 140, 84);
        _countdown.Text = $"Price changes in {state.Countdown}";
        _transition.Text = state.TransitionText is { } at ? $"at {at}" : " ";

        foreach (var (label, model, kind) in _priceLabels)
        {
            var pricing = DeepSeekPricing.Pricing(model, state.Phase);
            label.Text = UsdPriceFormatter.Format(kind switch
            {
                Meter.CacheHit => pricing.InputCacheHit,
                Meter.CacheMiss => pricing.InputCacheMiss,
                _ => pricing.Output,
            });
        }
    }

    /// <summary>Shows the panel near the mouse, clamped to the working area.</summary>
    public void ShowNearCursor()
    {
        // Show first so the auto-sized layout has produced a real Width/Height.
        Show();
        PerformLayout();

        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).WorkingArea;
        Left = Math.Max(screen.Left, Math.Min(cursor.X, screen.Right - Width));
        Top = Math.Max(screen.Top, Math.Min(cursor.Y, screen.Bottom - Height));

        BringToFront();
        Activate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    private void AddModel(DeepSeekModel model)
    {
        _content.Controls.Add(new Label
        {
            Text = model.DisplayName(),
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            Margin = new Padding(3, 10, 3, 2),
        });

        foreach (var meter in Meters)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = RowWidth,
                Margin = new Padding(3, 0, 3, 0),
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            var price = new Label { Text = string.Empty, AutoSize = true, Anchor = AnchorStyles.Right };
            row.Controls.Add(new Label { Text = meter.Label, AutoSize = true }, 0, 0);
            row.Controls.Add(price, 1, 0);

            _content.Controls.Add(row);
            _priceLabels.Add((price, model, meter.Kind));
        }
    }

    private enum Meter { CacheHit, CacheMiss, Output }

    private static readonly (string Label, Meter Kind)[] Meters =
    {
        ("Input · cache hit", Meter.CacheHit),
        ("Input · cache miss", Meter.CacheMiss),
        ("Output", Meter.Output),
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(200, 200, 200));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
```

- [ ] **Step 2: Wire the panel into the tray context**

In `windows/src/DeepSeekClock.Windows/TrayAppContext.cs`:

Add a field:

```csharp
    private readonly PopupForm _popup = new();
```

Add a left-click and Open-menu handler in the constructor, after the `_notifyIcon` assignment:

```csharp
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                TogglePopup();
        };
```

Add `"Open"` as the first menu item (before `"Exit"`):

```csharp
        menu.Items.Add("Open", null, (_, _) => TogglePopup());
        menu.Items.Add(new ToolStripSeparator());
```

Update `UpdateUi` to refresh an open panel:

```csharp
        if (_popup.Visible)
            _popup.ShowState(state);
```

Add the toggle method:

```csharp
    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
        }
        else
        {
            _popup.ShowNearCursor();
            _popup.ShowState(ClockState.From(DateTimeOffset.Now, TimeZoneInfo.Local));
        }
    }
```

Dispose the panel in `Dispose`:

```csharp
            _popup.Dispose();
```

- [ ] **Step 3: Build and run**

Run: `dotnet build windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj -c Release`
Expected: `Build succeeded`.

Then: `dotnet run --project windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj -c Release`

Manual verification:
- Left-clicking the tray icon opens the panel near the cursor.
- The panel shows the phase, "Price changes in ...", the transition time, and both rate cards.
- The countdown decreases every second.
- Clicking elsewhere hides the panel.

- [ ] **Step 4: Commit**

```powershell
git add windows/src/DeepSeekClock.Windows/PopupForm.cs windows/src/DeepSeekClock.Windows/TrayAppContext.cs
```

Invoke the `git-commiter` subagent.
Suggested message: `feat(windows): add popup panel with rates and countdown`

---

### Task 8: CI, README, and single-file packaging

**Files:**
- Create: `.github/workflows/windows-ci.yml`
- Create: `windows/README.md`
- Modify: root `README.md` (append a short link to the Windows version)

**Interfaces:**
- Consumes: the whole solution.
- Produces: an automated `windows-latest` job and documented publish commands.

- [ ] **Step 1: Add the CI workflow**

Create `.github/workflows/windows-ci.yml`:

```yaml
name: Windows CI

# Build the Windows app and run the .NET unit tests on every push and PR.
# Kept separate from the macOS ci.yml so the Swift build is unaffected.
on:
  push:
    branches: ["**"]
  pull_request:

jobs:
  test:
    runs-on: windows-latest
    defaults:
      run:
        working-directory: windows
    steps:
      - name: Check out
        uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.4.0

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Test
        run: dotnet test DeepSeekClock.sln -c Release

      - name: Publish single-file executable
        run: >
          dotnet publish src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj
          -c Release -r win-x64 --self-contained true
          -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
          -o publish
```

- [ ] **Step 2: Ignore publish output**

Append to `windows/.gitignore`:

```gitignore
publish/
```

- [ ] **Step 3: Verify the publish command locally**

Run from the repository root:

```powershell
dotnet publish windows/src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o windows/publish
```

Expected: `windows/publish/DeepSeekClock.exe` exists and `git status` does not list `windows/publish/`. Run the executable to confirm the tray icon appears.

- [ ] **Step 4: Write the Windows README**

Create `windows/README.md`:

```markdown
# DeepSeek Clock for Windows

A system-tray utility showing whether DeepSeek API pricing is currently peak or
off-peak, with the current Flash and V4 Pro rates and a countdown to the next
price change. The Windows counterpart to the macOS menu bar app in the parent
repository.

## Pricing schedule

- Peak: Monday–Friday, 01:00–04:00 and 06:00–10:00 UTC.
- Off-peak: all other times, at half price.

Times are shown in your local time zone; all calculation is done in UTC.

## Run

```powershell
dotnet run --project src/DeepSeekClock.Windows/DeepSeekClock.Windows.csproj
```

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
```

- [ ] **Step 5: Link the Windows version from the root README**

Append to the root `README.md`:

```markdown

## Windows

A Windows system-tray version lives in [`windows/`](windows/), built with
C# / .NET and distributed as a single `.exe`.
```

- [ ] **Step 6: Verify the full suite once more**

Run: `dotnet test windows/DeepSeekClock.sln -c Release`
Expected: PASS, all tests.

- [ ] **Step 7: Commit**

```powershell
git add .github/workflows/windows-ci.yml windows/README.md windows/.gitignore README.md
```

Invoke the `git-commiter` subagent.
Suggested message: `ci(windows): add Windows CI workflow, README, and packaging docs`

---

## Verification

After all tasks:

1. `dotnet test windows/DeepSeekClock.sln -c Release` passes locally.
2. `dotnet publish ...` produces a runnable `DeepSeekClock.exe`.
3. Push and confirm both `.github/workflows/ci.yml` (macOS, must stay green) and `.github/workflows/windows-ci.yml` succeed.
4. The Swift sources are untouched: `git log --oneline -- Package.swift Sources Tests` shows no commits from this work, and `git status` is clean for those paths.

# Active Session Time — design (client + server)

> Status: **design approved, ready to implement** · Author: design session 2026-07-20
> Scope: two repos — **Pix2d** (client) and **AppStatServer** (`c:\Projects\AppStatServer`, server + dashboard).
> Implementer notes are inline; the checklist at the end is the authoritative task list.

## 1. Problem

The dashboard's **"AVG. SESSION"** (e.g. *6h 36m*) is a plain average of the `duration` field of
Sentry release-health session envelopes:

- Server: `AvgSessionSeconds = winSessions.Average(s => s.Duration)` —
  `AppStatServer/LiteDbEventStorage.cs:336`, fed from the LiteDB `"sessions"` collection
  (`AppSession`, upserted by sid in `SaveSessionsAsync`).
- Client: the duration is produced by the Sentry SDK (`AutoSessionTracking = true`,
  `IsGlobalModeEnabled = true`) — wall-clock from `SentrySdk.Init()` to `SentrySdk.Close()` /
  `ProcessExit` (`Sources/Heads/Pix2d.Desktop/Services/DesktopSentryCrashTelemetrySink.cs:40-52`,
  `Sources/Heads/Pix2d.Droid/Services/AndroidSentryCrashTelemetrySink.cs:37-50`).
- Nothing pauses on minimize / focus loss / Android backgrounding (deliberate for release
  health — see the comment at `Sources/Heads/Pix2d.Droid/MainActivity.cs:247-251`).

So the metric measures **"how long the process was alive"**, not **"how long the user actually
worked"**. A parked desktop window inflates it to hours.

## 2. Goals / non-goals

**Goals**

1. Measure **active session time** on the client: time the app is foreground **and** the user has
   given input within an idle timeout.
2. Deliver it to AppStatServer over the **existing `/api/track` transport** (batching, offline
   handling, consent gating all come for free).
3. Server: store one record per client session and surface genuinely useful session analytics:
   active vs wall-clock averages **and medians**, engagement ratio, a per-session table, and a
   per-session event timeline.
4. Zero new consent surface — same strict opt-in as existing analytics.

**Non-goals**

- Do **not** touch the Sentry session (`AutoSessionTracking` stays as-is; it drives crash-free
  rate and remains the wall-clock source for old clients).
- No per-action time attribution ("time in Draw tool") — future work.
- No WASM foreground detection in v1 (no `visibilitychange` interop; the idle timeout alone
  covers it acceptably).

## 3. Architecture overview

```
┌───────────────────────────── Pix2d client ─────────────────────────────┐
│  input events ─┐                                                       │
│  (pointer/key) ├─► ActiveTimeTracker ◄── foreground on/off             │
│                │   (pure accumulator,     (ActiveSessionLifecycleHost: │
│                │    Infrastructure)        Window.(De)Activated /      │
│                │                           IActivatableLifetime)       │
│                ▼                                                       │
│  SessionStatsReporter ── every 5 min + on shutdown/background ──┐      │
│                                                                 ▼      │
│            AppStatLoggerTarget.TrackSessionStats ─► AppStatTrackingClient
│                                    "@session" event, cumulative counters
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ POST /api/track (existing endpoint)
┌───────────────────────────────────▼─────────────── AppStatServer ──────┐
│  TrackEventParser: name == "@session"  →  NOT stored as TrackEvent     │
│      → ClientSession upsert (LiteDB "clientsessions", max-merge)       │
│  Analytics: active avg/median, wall median, engagement, buckets        │
│  Dashboard: KPI tiles + Sessions table + session event timeline        │
└─────────────────────────────────────────────────────────────────────────┘
```

Join model: `ClientSession.Id` == the `AppStatTrackingClient.SessionId` GUID that already stamps
every track-event envelope (`AppStatTrackingClient.cs:108,234`), so **session ↔ its track events
join exactly** by `SessionId`. (Sentry `AppSession.Id` is a different id — Sentry sessions stay a
separate, wall-clock-only dataset; do not try to join them per-session.)

---

## 4. Client design (Pix2d repo)

### 4.1 `ActiveTimeTracker` — pure accumulator

**New file:** `Sources/Core/Pix2d.Infrastructure/AppStat/ActiveTimeTracker.cs`
(next to `AppStatTrackingClient.cs`; BCL-only, no Avalonia dependency, AOT/WASM-safe).

Semantics — active time accrues for any instant where **both** hold:

- the app is foreground (`NotifyForeground(true)` was the last foreground signal), and
- the last input was less than `IdleTimeout` ago (default **5 minutes**; `TimeSpan` ctor
  parameter). Rationale: drawing produces near-continuous input; 5 min tolerates "thinking with
  the stylus down" without counting a lunch break.

```csharp
public sealed class ActiveTimeTracker
{
    public ActiveTimeTracker(TimeSpan? idleTimeout = null, Func<long>? clockMs = null);

    public void NotifyForeground(bool isForeground);  // window/activity focus signal
    public void NotifyInput();                        // any pointer/key/wheel event
    public Snapshot GetSnapshot();                    // settles + returns totals

    public readonly record struct Snapshot(long ActiveSeconds, long WallSeconds, DateTime StartedUtc);
}
```

Implementation notes:

- Monotonic clock: `Environment.TickCount64` by default; `clockMs` injectable for sanity checks.
  `StartedUtc = DateTime.UtcNow` captured once in the ctor. **Never** use wall-clock deltas for
  accumulation (system clock changes / sleep would corrupt them).
- State: `_accumulatedMs`, `_isForeground` (starts `true` — the app launches foreground),
  `_openedAtMs` (nullable interval start), `_lastInputMs`.
- **Settle-on-signal** model: every public call first *settles* — if an interval is open, add
  `min(nowMs, _lastInputMs + idleTimeoutMs) - _openedAtMs` (clamped ≥ 0) to `_accumulatedMs`,
  then close it. Then, if (foreground && within idle lease) an interval re-opens at `nowMs`.
  This makes idle expiry retroactively correct without any timer inside the tracker.
- Thread safety: one private `lock` around every public method. Signals come from the UI thread,
  snapshots from a timer thread; contention is negligible.
- `NotifyInput()` must be allocation-free and cheap (a lock + few longs) — it is called from
  pointer-move handlers.
- `WallSeconds` = `(nowMs - createdAtMs) / 1000`.

### 4.2 `ActiveSessionLifecycleHost` — Avalonia signal wiring

**New file:** `Sources/Core/Pix2d.Core/Services/Telemetry/ActiveSessionLifecycleHost.cs`.
Mirror the structure of `AutoSaveLifecycleHost`
(`Sources/Core/Pix2d.Core/Services/AutoSave/AutoSaveLifecycleHost.cs`) — same Bind()/Dispose()
shape, same desktop-vs-mobile branching. Per platform:

| Signal | Desktop | Android | WASM |
|---|---|---|---|
| Foreground on | `Window.Activated` on `desktop.MainWindow` (subscribe like `AutoSaveLifecycleHost.BindDesktop` does, incl. the lazy-MainWindow case via `desktop.Startup`) | `IActivatableLifetime.Activated` (kind `Background` counterpart) | none — always foreground; idle timeout does the work |
| Foreground off | `Window.Deactivated` | `IActivatableLifetime.Deactivated` with `ActivationKind.Background` (same filter as `AutoSaveLifecycleHost.OnDeactivated`) | none |
| Input | `TopLevel`/MainWindow `AddHandler` for `PointerPressedEvent`, `PointerMovedEvent`, `PointerWheelChangedEvent`, `KeyDownEvent` — `RoutingStrategies.Tunnel`, `handledEventsToo: true`. Handler body is just `tracker.NotifyInput()` | same handlers on the main view's TopLevel | same |

Notes:

- Desktop `Deactivated` fires on plain focus loss (alt-tab) — that is **exactly** what we want to
  stop counting, unlike autosave which only cares about `ActivationKind.Background`.
- Also flush a final ping from the mobile `Deactivated(Background)` handler (see 4.3) — on
  Android the process may be killed without any further callback.
- No throttling needed on `PointerMoved`: `NotifyInput` is a field write under a lock.

### 4.3 Reporting — the `@session` ping

**Event name: `"@session"`** — the `@` prefix marks it server-reserved; the server intercepts it
at ingest and it never appears in product-event reports.

Extend `AppStatLoggerTarget` (`Sources/Core/Pix2d.Core/Logging/AppStatLoggerTarget.cs`) with a
direct method (bypasses `Logger` so pings don't spam the log file and other targets):

```csharp
public void TrackSessionStats(long activeSeconds, long wallSeconds, DateTime startedUtc, string? platform)
    => _client.Track("@session", new Dictionary<string, object>
    {
        ["activeSeconds"] = activeSeconds,   // cumulative since process start
        ["wallSeconds"]   = wallSeconds,     // cumulative since process start
        ["startedUtc"]    = startedUtc,      // ISO-8601 via existing WriteValue DateTime case
        ["platform"]      = platform ?? "",
    });
```

**New file:** `Sources/Core/Pix2d.Core/Services/Telemetry/SessionStatsReporter.cs` — owns a
`System.Threading.Timer` (period **5 min**) plus a `ReportNow(bool force)` method:

- On each tick: `GetSnapshot()`; **skip** the ping if `ActiveSeconds` hasn't grown since the last
  sent ping (an idle/backgrounded app stops pinging — that's intentional: its wall-clock freezes
  near the last activity instead of inflating).
- `ReportNow(force: true)` sends regardless of delta and then calls the target's `Flush()`.
  Wire it to:
  - **Desktop shutdown** — alongside the existing clean-exit hook
    (`DesktopPix2dBootstrapperDI.cs:85`, `OnAppClosing` / `MarkCleanExit` path).
  - **Mobile backgrounding** — from `ActiveSessionLifecycleHost.OnDeactivated(Background)`.
    Fire-and-forget is fine here (the AppStat flush is already async best-effort; do NOT block
    the UI thread like autosave does — losing one ping is acceptable).
  - **Android real exit** — `MainActivity` exit path next to `CloseTelemetrySinkSafely()`
    (`Sources/Heads/Pix2d.Droid/BackPress.cs:35` → `MainActivity.cs`).
- Consent: the reporter holds `Func<AppStatLoggerTarget?>` (or is created/disposed together with
  the target). When analytics is disabled (`DisableAnalytics`,
  `Sources/Core/Pix2d.Core/Pix2dBootstrapperDI.cs:462-483`) the reporter must stop sending.
  Simplest correct wiring: **bootstrapper owns everything** — `EnableAnalytics` creates
  tracker-reporter pair if absent; `DisableAnalytics` disposes the reporter (tracker may keep
  accumulating; it's inert and consent-irrelevant until reported).

### 4.4 Wiring (`Pix2dBootstrapperDI` + heads)

- `ActiveTimeTracker` is created **unconditionally** at startup (cheap, no I/O, no consent
  implications — data leaves the device only via the reporter). Register as singleton or hold a
  field next to `_appStatTarget`.
- `ActiveSessionLifecycleHost.Bind()` — call where `AutoSaveLifecycleHost` is bound (find its
  `Bind()` call site and mirror it).
- `EnableAnalytics` (`Pix2dBootstrapperDI.cs:423`): after registering the target, create and
  start `SessionStatsReporter` with the tracker + target + platform string
  (`IPlatformStuffService.CurrentPlatform`, same source as the `App launched` event at
  `Pix2dBootstrapperDI.cs:445-448`).
- `DisableAnalytics`: dispose/stop the reporter before unregistering the target.
- Platform string values are whatever `CurrentPlatform.ToString()` yields today — keep
  consistent with `App launched` for server-side grouping.

---

## 5. Server design (AppStatServer repo)

### 5.1 Data model + ingestion

**New entity** `src/AppStatServer/Data/ClientSession.cs`:

```csharp
// One app-run session as reported by the client's "@session" pings (cumulative counters,
// upsert-by-id like AppSession). Id == the AppStat envelope sessionId, so TrackEvents join
// exactly on SessionId.
public class ClientSession
{
    public string Id { get; set; } = string.Empty;      // envelope sessionId (GUID "N")
    public string UserId { get; set; } = string.Empty;  // install id
    public string Release { get; set; } = string.Empty;
    public string? Os { get; set; }
    public string? Platform { get; set; }               // Desktop / Android / Web (head platform)
    public DateTime StartedUtc { get; set; }             // from ping prop, first ping wins
    public DateTime LastSeen { get; set; }               // timestamp of the latest ping
    public long ActiveSeconds { get; set; }              // max over pings (cumulative counter)
    public long WallSeconds { get; set; }                // max over pings
}
```

**Ingestion:** in the `/api/track` path (`Tracking/TrackEventHandler.cs` + `TrackEventParser`):
after parsing, partition events by `Name == "@session"`. Session pings are **not** saved to
`"trackevents"`; instead map each to a `ClientSession` and call a new
`IEventStorage.SaveClientSessionsAsync(...)` (`IEventStorage.cs` + `LiteDbEventStorage.cs`):

- Collection `"clientsessions"`, upsert by `Id` with **max-merge**: fetch existing (if any),
  keep `ActiveSeconds = max(old, new)`, `WallSeconds = max(old, new)`,
  `LastSeen = max`, `StartedUtc` = existing value if present (first ping wins). Max-merge makes
  out-of-order batch delivery harmless (counters are cumulative/monotonic).
- Parse `startedUtc` / `activeSeconds` / `wallSeconds` / `platform` from the event properties
  (parser already yields long/double/string scalars); tolerate missing props (skip the ping).
- Follow the existing LiteDB local-time convention (`GetAnalyticsAsync` comment at
  `LiteDbEventStorage.cs:293`) — store `StartedUtc`/`LastSeen` the same way `TrackEvent.Timestamp`
  is stored today so window filters behave identically.
- **Defensive:** also exclude `Name.StartsWith("@")` from `GetEventsReportAsync` /
  `GetRecentTrackEventsAsync` / funnels, so reserved events never pollute product reports even
  if a future client stores one.
- Purge/compact maintenance (`/api/maintenance/*`) must include `"clientsessions"`.

### 5.2 Analytics additions

`Data/Analytics.cs` → `AnalyticsData` gains:

```csharp
// Client-reported active-time sessions (null/empty when no new-client data in the window).
public int ClientSessions { get; set; }                  // count of ClientSession in window
public double AvgActiveSessionSeconds { get; set; }
public double MedianActiveSessionSeconds { get; set; }
public double AvgClientWallSeconds { get; set; }         // wall-clock of the SAME sessions
public double MedianSessionSeconds { get; set; }         // median of Sentry wall sessions (robust vs the 6h outliers)
public double EngagementRatio { get; set; }              // sum(active)/sum(wall) over client sessions, 0..1
public List<CountByKey> ActiveDurationBuckets { get; set; } = [];
```

In `GetAnalyticsAsync` (`LiteDbEventStorage.cs:287`): load `"clientsessions"` with
`StartedUtc >= start`, compute the above; reuse `BuildDurationBuckets` bucket edges
(`LiteDbEventStorage.cs:777-792`) for `ActiveDurationBuckets` (extract the bucketing into a
shared helper taking a `Func<T,int>` seconds selector). Keep `AvgSessionSeconds` (Sentry)
untouched for continuity.

### 5.3 New endpoints (in `Program.cs`, next to the existing `api.MapGet("/sessions", ...)` at line 180)

- `GET /api/client-sessions?days=&limit=` → recent `ClientSession` rows (default days 14,
  limit 50, newest first) each enriched with `eventCount` = count of `TrackEvent` with the same
  `SessionId` (computed at query time; LiteDB scale is fine).
- `GET /api/client-sessions/{id}` → `{ session, events: [...] }` — the session plus its track
  events ordered by timestamp (name, timestamp, properties). This is the per-session timeline.

### 5.4 Dashboard (wwwroot)

All changes follow existing patterns (`kpiTile`/`statTile`, `fmtDuration` in `charts.js:23-28`,
bar-chart helpers around `app.js:413-417`).

1. **Overview tile** (`app.js:134`): replace
   `kpiTile("Avg. session", fmtDuration(a.avgSessionSeconds))` with an active-first tile:
   value = `fmtDuration(a.medianActiveSessionSeconds)` labeled **"Median active session"**, sub =
   `"avg " + fmtDuration(a.avgActiveSessionSeconds) + " · wall " + fmtDuration(a.avgSessionSeconds)`.
   When `a.clientSessions === 0` (no new-client data yet), fall back to the old wall tile with
   sub `"wall-clock — process lifetime"` so the change is honest during rollout.
2. **Analytics page** (`app.js:380` area): a "Sessions" KPI row — Sessions (Sentry count, as now),
   Median active, Avg active, Median wall (`medianSessionSeconds`), Engagement
   (`Math.round(engagementRatio*100) + "%"`, sub "active / wall-clock").
3. **Histogram**: render `activeDurationBuckets` alongside the existing `durationBuckets`
   (two bar charts side by side titled "Session length (wall-clock)" / "Active time per session";
   hide the active one when empty).
4. **Sessions table** (new block on the Analytics page, fed by `/api/client-sessions`):
   columns Started · User (first 8 chars of install id, monospace) · Platform · Release ·
   Wall · Active · Activity % (`active/wall`) · Events. Row click → expandable/inline timeline
   from `/api/client-sessions/{id}` (event name + time offset from session start). This is the
   "more useful session info" the dashboard currently lacks: *what a session actually consisted of*.

### 5.5 Why median + engagement, not just a better average

Averages of session length are dominated by parked-window outliers even for *active* time
(one 8-hour workday vs fifty 5-minute doodles). The median answers "how long does a typical
session last"; the engagement ratio answers "how much of the open time is real work" — both are
stable, comparable release-to-release numbers. Keep averages as secondary context.

---

## 6. Decisions & edge cases (resolved — do not re-litigate)

| Topic | Decision |
|---|---|
| Sentry `AutoSessionTracking` | Unchanged. Crash-free rate must not shift semantics; old-client wall data keeps flowing. |
| Idle timeout | 5 min, client-side constant. Not configurable remotely in v1. |
| Ping cadence | 5 min, only when active time grew; forced ping + flush on shutdown / mobile background / Android exit. |
| Counters | Cumulative per process; server max-merges → duplicates and out-of-order delivery are harmless (idempotent). |
| Event name | `"@session"`; `@` prefix reserved for infrastructure events, filtered from all product-event reports. |
| WASM | Pings work (client is WASM-safe); no foreground signal — idle timeout only. Acceptable v1. |
| Sleep/hibernate (desktop) | `TickCount64` doesn't advance during sleep on Windows → sleep time is naturally excluded from both counters. Bonus, not a bug. |
| Privacy/consent | No new data classes — durations only; gated by the exact same `TelemetryConsent.Allowed` path as every track event. No consent-text change needed. |
| Old clients | Produce no pings → active metrics computed over `clientsessions` only; dashboard falls back gracefully when the window has none. |
| Joining Sentry sessions ↔ client sessions | Don't (different ids). They are two datasets: release-health (Sentry) vs engagement (AppStat). |

## 7. Implementation checklist

**Pix2d (client)** — target `Sources/`:

1. [x] `Pix2d.Infrastructure/AppStat/ActiveTimeTracker.cs` — accumulator per §4.1.
2. [x] `Pix2d.Core/Services/Telemetry/ActiveSessionLifecycleHost.cs` — signals per §4.2
       (model on `AutoSaveLifecycleHost`).
3. [x] `AppStatLoggerTarget.TrackSessionStats(...)` per §4.3.
4. [x] `Pix2d.Core/Services/Telemetry/SessionStatsReporter.cs` — timer + `ReportNow(force)`.
5. [x] Bootstrapper wiring per §4.4 (`EnableAnalytics`/`DisableAnalytics`, lifecycle bind).
6. [x] Desktop shutdown hook (`DesktopPix2dBootstrapperDI.OnAppClosing` area) + Android exit
       hook (`MainActivity`, next to `CloseTelemetrySinkSafely`) call `ReportNow(force:true)`.
7. [x] Update `docs/ROADMAP.md` Track B item (added 2026-07-20) — tick when shipped.

**AppStatServer (server)** — target `src/AppStatServer/`:

8. [x] `Data/ClientSession.cs` per §5.1.
9. [x] Ingestion split in `Tracking/` + `IEventStorage.SaveClientSessionsAsync` +
       LiteDB max-merge upsert; `@`-prefix exclusion in event reports; purge coverage.
10. [x] `AnalyticsData` fields + `GetAnalyticsAsync` computation per §5.2 (shared bucket helper).
11. [x] `GET /api/client-sessions` + `GET /api/client-sessions/{id}` per §5.3.
12. [x] Dashboard per §5.4 (tiles, dual histogram, sessions table + timeline).
13. [x] Maintenance/test page (`app.js` ~1157): add a "send test @session ping" button mirroring
        the existing test-event button, so ingestion is verifiable without a client build.

**Verification (manual QA):**

- Desktop Debug run: draw for ~1 min, alt-tab away 2 min, come back — first ping's
  `activeSeconds` ≈ 60–90, `wallSeconds` ≈ 180+. Check via the server's recent client-sessions
  endpoint (or LiteDB) that max-merge and `eventCount` behave.
- Minimize overnight test: `WallSeconds` must stop growing ≤ 5 min after last input (pings stop).
- Consent off mid-session (Settings toggle): no further pings.
- Android: background the app → a forced ping arrives; double-back exit → final ping.
- Dashboard: with zero client sessions in window the overview tile falls back to wall-clock.

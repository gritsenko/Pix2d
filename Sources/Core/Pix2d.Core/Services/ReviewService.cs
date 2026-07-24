#nullable enable
using Pix2d.Messages;
using System.Diagnostics;

namespace Pix2d.Services;

public abstract class ReviewService : IReviewService, IDisposable
{
    protected ISettingsService SettingsService { get; }
    protected IMessenger Messenger { get; }
    public AppState AppState { get; }

    private static readonly string[] PromptMessages =
    [
        "Please, Rate Pix2D!",
        "Enjoy app? please review it!",
        "Review pix2D, please!",
        "Have any feedback?",
        "Leave review, please!",
        "Do you like this app, you review will help to improve it!",
        "Can you give us several stars? :-)",
        "Your art is really nice! Do you like the app?",
        "You made such cool art! May be you want to review pix2d?",
        "Amazing work! Can you rate pix2d? It won't take long!",
        "Well done! Can you rate pix2d? Say us what do you like and what you don't?",
        "Amazing art! Like Pix2d?",
        "You can support this app by review!",
        "You can support this app by several stars ⭐",
        "Write what do you like in this app and what you don't, please",
        "Get +10 to art skills by reviewing this app 😎"
    ];

    private static readonly string[] PromptButtonText =
    {
        "Rate",
        "Review",
        "OK",
        "Yes",
        "  👌  ",
        "  👍  "
    };

    private Dictionary<string, string> _lastReviewArgs = null!;

    protected ReviewService(ISettingsService settingsService, IMessenger messenger, AppState appState)
    {
        SettingsService = settingsService;
        Messenger = messenger;
        AppState = appState;

        messenger.Register<ProjectSavedMessage>(this, m => TrySuggestRate("Save"));
        messenger.Register<ProjectExportedMessage>(this, m => TrySuggestRate("Export"));

        //InitRatePromptMessage
        var random = new Random();
        var start2 = random.Next(0, PromptMessages.Length);
        var msg = PromptMessages[start2];

        RatePromptMessage = msg;

        var start = random.Next(0, PromptButtonText.Length);
        RatePromptButtonText = PromptButtonText[start];

        // Fold the previous session's elapsed wall-clock time into the stored total now, at the start of
        // this one — Dispose() (the old flush point for this) is never reached in practice: Android
        // backgrounds/kills the process via OnPause/OnStop/OnDestroy without disposing this service, and
        // desktop's OnAppClosing doesn't touch it either. Doing it here instead makes the 2-hour gate in
        // TrySuggestRate cumulative across app launches rather than resetting (and never firing) every time.
        if (SettingsService.TryGet<DateTime>(nameof(AppSettings.LaunchTime), out var previousLaunchTime)
            && previousLaunchTime != default)
        {
            var previousSession = DateTime.Now - previousLaunchTime;
            // Wall-clock arithmetic: a backwards clock/timezone change (or a restored settings file from
            // the future) yields a negative span, which must not eat into the accumulated total.
            if (previousSession > TimeSpan.Zero)
            {
                SettingsService.TryGet<long>(nameof(AppSettings.TotalWorkTimeTicks), out var accumulatedTicks);
                SettingsService.Set(nameof(AppSettings.TotalWorkTimeTicks), accumulatedTicks + previousSession.Ticks);
            }
        }

        SettingsService.Set(nameof(AppSettings.LaunchTime), DateTime.Now);
    }

    public void SaveTotalWorkTime()
    {
        SettingsService.Set(nameof(AppSettings.TotalWorkTimeTicks), GetTotalWorkTime().Ticks);
        // Reset the session clock so a later Dispose() (if it ever fires) doesn't double-count
        // the span already folded into the total.
        SettingsService.Set(nameof(AppSettings.LaunchTime), DateTime.Now);
    }

    // The total is only persisted in the ctor and in SaveTotalWorkTime(), but Dispose() (the only
    // caller of the latter) is never reached in the real app lifecycle — Android backgrounds/kills
    // the process via OnPause/OnStop/OnDestroy and desktop's OnAppClosing never touches this service.
    // Without folding in the current session's elapsed time, the stored value stays 0 forever and
    // TrySuggestRate's 2-hour gate can never pass. Compute it live instead of relying on a flush point.
    private TimeSpan GetTotalWorkTime()
    {
        SettingsService.TryGet<long>(nameof(AppSettings.TotalWorkTimeTicks), out var totalTicks);
        return TimeSpan.FromTicks(Math.Max(0, totalTicks)) + GetSessionTime();
    }

    /// <summary>
    /// Elapsed time in the current session. The ctor writes <c>LaunchTime</c>, but it can still read back
    /// as <c>default(DateTime)</c> — a wiped/unwritable settings file, or (as shipped in 3.11.1) a key with
    /// no backing <see cref="AppSettings"/> property, which made every write a silent no-op. Left unguarded,
    /// <c>DateTime.Now - DateTime.MinValue</c> is ~739820 days, which sails through the 2-hour work-time gate
    /// on a brand-new install and reports "workTime=739820+days" in the funnel events. Treat a missing
    /// launch time as "the session just started" so the gate stays closed instead of wide open.
    /// </summary>
    private TimeSpan GetSessionTime()
    {
        if (!SettingsService.TryGet<DateTime>(nameof(AppSettings.LaunchTime), out var launchTime)
            || launchTime == default)
            return TimeSpan.Zero;

        var elapsed = DateTime.Now - launchTime;
        return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
    }
    public string RatePromptButtonText { get; set; }

    public string RatePromptMessage { get; set; }

    // Master switch for the whole in-app rate funnel. Heads override this to gate the prompt by
    // distribution channel — e.g. desktop only prompts in the MS Store build, portable/itch/Gumroad/
    // Linux/macOS stay silent. Kept above the DEBUG bypass below so a disabled channel never prompts,
    // even in DEBUG. Default: always enabled.
    protected virtual bool IsReviewPromptEnabled => true;

    /// <summary>
    /// Cooldown applied to an impression the user never answered. Matches the first tier of the
    /// escalating "Not now" schedule in <see cref="DefferNextReviewPrompt"/>.
    /// </summary>
    private const int IgnoredPromptDeferDays = 3;

    public bool TrySuggestRate(string? contextTitle)
    {
        if (!IsReviewPromptEnabled)
            return false;

        // The banner is already on screen — another save/export while it's up would log a duplicate
        // impression and bump the counter without showing the user anything new.
        if (AppState.UiState.ShowRatePrompt)
            return false;

        SettingsService.TryGet<bool>(nameof(AppSettings.IsAppReviewed), out var isReviewed);
        SettingsService.TryGet<DateTime>(nameof(AppSettings.NextPromptTime), out var nextPromptTime);
        var totalWorkTime = GetTotalWorkTime();

        if (isReviewed || nextPromptTime > DateTime.Now || totalWorkTime.TotalHours < 2)
        {
            Debug.WriteLine("Not ready for review prompt");
#if !DEBUG
            return false;
#endif
        }

        SettingsService.TryGet<int>(nameof(AppSettings.AppReviewPromptsCount), out var promptsCount);
        promptsCount++;
        SettingsService.Set(nameof(AppSettings.AppReviewPromptsCount), promptsCount);

        // Cool the funnel down on the impression itself. Both explicit answers set their own terminal state
        // (Accepted → IsAppReviewed, "Not now" → escalating defer), but a banner the user simply ignores
        // used to leave nothing behind — so the very next save re-prompted immediately.
        SettingsService.Set(nameof(AppSettings.NextPromptTime), DateTime.Now.AddDays(IgnoredPromptDeferDays));

        // Logged after the increment so promptsCount identifies *this* impression (1 on the first one)
        // rather than always trailing by one.
        LogReview("Showing prompt", contextTitle);

        AppState.UiState.ShowRatePrompt = true;
        return true;
    }

    public void DefferNextReviewPrompt()
    {
        // Reached only from the "Not now" button (CloseRatePromptCommand) — funnel: banner dismissed.
        LogReview("Dismissed");

        SettingsService.TryGet<int>(nameof(AppSettings.AppReviewPromptsCount), out var promptsCount);
        var defferDays = 0;

        switch (promptsCount)
        {
            case 0:
            case 1:
                defferDays = 3;
                break;
            case 2:
                defferDays = 7;
                break;
            case 3:
                defferDays = 14;
                break;
            case 4:
                defferDays = 30;
                break;
            default:
                defferDays = 90;
                break;
        }

        var nextPromptTime = DateTime.Now.AddDays(defferDays);
        SettingsService.Set(nameof(AppSettings.NextPromptTime), nextPromptTime);
    }

    private bool _isRatingInProgress;

    // Template method: the base owns the funnel logging (the user accepted the banner), heads implement
    // the channel-specific rating in RateAppCore and log their own destination/outcome detail
    // ("Store dialog" + result, "Opened store page", "Opened review hub", "In-app review requested").
    public async Task<bool> RateApp()
    {
        // RateAppCore awaits a platform dialog (Google's in-app review, the MS Store sheet) that can take
        // seconds to appear, so a second tap on the still-live banner button re-entered here and logged a
        // duplicate "Accepted" (observed twice a second apart in production funnel data).
        if (_isRatingInProgress)
            return false;

        _isRatingInProgress = true;
        try
        {
            // The funnel ends here regardless of what the platform reports back: none of the channels tell
            // us whether a review was actually left, and re-asking someone who already agreed is the worse
            // failure. Owned by the service (not the command) so every caller terminates the funnel.
            SettingsService.Set(nameof(AppSettings.IsAppReviewed), true);

            LogReview("Accepted");
            return await RateAppCore();
        }
        finally
        {
            _isRatingInProgress = false;
        }
    }

    protected abstract Task<bool> RateAppCore();

    public string GetPromptMessage()
    {
        return RatePromptMessage;
    }

    public string GetPromptButtonText()
    {
        return RatePromptButtonText;
    }

    public void LogReview(string action, string? context = default, IReadOnlyDictionary<string, string>? extra = null)
    {
        var promptMessage = RatePromptMessage;

        var sessionTime = GetSessionTime();

        SettingsService.TryGet<int>(nameof(AppSettings.AppReviewPromptsCount), out var promptsCount);
        var totalWorkTime = GetTotalWorkTime();

        Dictionary<string, string> args;

        if (context == default)
        {
            // Response/outcome events (Accepted/Dismissed/Store dialog/…) reuse the args captured when the
            // prompt was shown, so the whole funnel shares one context/promptMsg/counters set.
            args = _lastReviewArgs ?? new Dictionary<string, string>();
        }
        else
        {
            args = new Dictionary<string, string>
            {
                ["context"] = context,
                ["promptMsg"] = promptMessage,
                ["promptsCount"] = promptsCount.ToString(),
                ["workTime"] = FormatTimespan(totalWorkTime),
                ["sessionTime"] = FormatTimespan(sessionTime),
                ["buttonText"] = RatePromptButtonText,
            };
            _lastReviewArgs = args;
        }

        // Merge per-event extras (result/dest) into a copy so the cached _lastReviewArgs is never mutated.
        if (extra != null)
        {
            args = new Dictionary<string, string>(args);
            foreach (var (key, value) in extra)
                args[key] = value;
        }

        Logger.LogEventWithParams("*Review: " + action, args.ToDictionary(x => x.Key, x => (string?)x.Value));
    }

    private static string FormatTimespan(TimeSpan period)
    {
        if (period.TotalSeconds <= 10)
            return "0-10s";

        if (period.TotalSeconds <= 30)
            return "10-30s";

        if (period.TotalSeconds <= 60)
            return "30s-1m";

        if (period.TotalMinutes <= 30)
            return "1-30m";

        if (period.TotalMinutes <= 60)
            return "30-1h";

        if (period.TotalHours <= 5)
            return "1-5h";

        if (period.TotalHours <= 24)
            return "5-24h";

        return Math.Round(period.TotalDays / 10) * 10 + "+days";
    }

    public void Dispose()
    {
        SaveTotalWorkTime();
    }
}
using Pix2d.Messages;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Pix2d.Services;

public abstract class ReviewService : IReviewService
{
    protected ISettingsService SettingsService { get; }
    protected IMessenger Messenger { get; }

    private static readonly string[] PromptMessages =
    {
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
        "Get +10 to art skills by reviewing this app 😎",
    };

    private static readonly string[] PromptButtonText =
    {
        "Rate",
        "Review",
        "OK",
        "Yes",
        "  👌  ",
        "  👍  "
    };

    private Dictionary<string, string> _lastReviewArgs;

    protected ReviewService(ISettingsService settingsService, IMessenger messenger)
    {
        SettingsService = settingsService;
        Messenger = messenger;

        messenger.Register<ProjectSavedMessage>(this, m => TrySuggestRate("Save"));
        messenger.Register<ProjectExportedMessage>(this, m => TrySuggestRate("Export"));

        //InitRatePromptMessage
        var random = new Random();
        var start2 = random.Next(0, PromptMessages.Length);
        var msg = PromptMessages[start2];

        RatePromptMessage = msg;

        var start = random.Next(0, PromptButtonText.Length);
        RatePromptButtonText = PromptButtonText[start];
    }

    public string RatePromptButtonText { get; set; }

    public string RatePromptMessage { get; set; }

    public bool TrySuggestRate(string contextTitle)
    {
        SettingsService.TryGet<bool>("IsAppReviewed", out var isReviewed);
        SettingsService.TryGet<DateTime>("NextPromptTime", out var nextPromptTime);
        SettingsService.TryGet<TimeSpan>("TotalWorkTime", out var totalWorkTime);

        if (isReviewed || nextPromptTime > DateTime.Now || totalWorkTime.TotalHours < 2)
        {
            Debug.WriteLine("Not ready for review prompt");
#if !DEBUG
            return false;
#endif
        }
        SettingsService.TryGet<int>("AppReviewPromptsCount", out var promptsCount);

        LogReview("Showing prompt", contextTitle);

        promptsCount++;
        SettingsService.Set("AppReviewPromptsCount", promptsCount);
        return true;
    }

    public void DefferNextReviewPrompt()
    {
        SettingsService.TryGet<int>("AppReviewPromptsCount", out var promptsCount);
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
        SettingsService.Set("NextPromptTime", nextPromptTime);
    }

    public abstract Task<bool> RateApp();

    public string GetPromptMessage()
    {
        return RatePromptMessage;
    }

    public string GetPromptButtonText()
    {
        return RatePromptButtonText;
    }

    public void LogReview(string action, string context = default)
    {
        var promptMessage = RatePromptMessage;

        var launchTime = CoreServices.SettingsService.Get<DateTime>("LaunchTime");
        var sessionTime = DateTime.Now - launchTime;

        SettingsService.TryGet<int>("AppReviewPromptsCount", out var promptsCount);
        SettingsService.TryGet<TimeSpan>("TotalWorkTime", out var totalWorkTime);

        var args = new Dictionary<string, string>();

        if (context == default)
        {
            args = _lastReviewArgs;
        }
        else
        {
            args["context"] = context;
            args["promptMsg"] = promptMessage;
            args["promptsCount"] = promptsCount.ToString();
            args["workTime"] = FormatTimespan(totalWorkTime);
            args["sessionTime"] = FormatTimespan(sessionTime);
            args["buttonText"] = RatePromptButtonText;
            _lastReviewArgs = args;
        }
        Logger.LogEventWithParams("*Review: " + action, args);
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

}
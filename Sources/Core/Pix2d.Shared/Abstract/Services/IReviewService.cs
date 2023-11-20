using System.Threading.Tasks;

namespace Pix2d.Abstract.Services;

public interface IReviewService
{
    Task<bool> RateApp();
    void LogReview(string action, string context = default);
    void DefferNextReviewPrompt();
    string GetPromptMessage();
    string GetPromptButtonText();
    bool TrySuggestRate(string contextTitle);
}
#nullable enable
using System.Threading.Tasks;

namespace Pix2d.Abstract.Services;

public interface IReviewService
{
    Task<bool> RateApp();
    void DefferNextReviewPrompt();
    string GetPromptMessage();
    string GetPromptButtonText();
    bool TrySuggestRate(string? contextTitle);
}
namespace Pix2d.Abstract.Services;

public interface IReviewService
{
    void LogReview(string action, string context = default);
    void DefferNextReviewPrompt();
}
namespace Pix2d.Infrastructure.Tasks;

public interface ILongTask
{
    Task ExecuteAsync(CancellationToken cancellationToken);
    string Name { get; }
}
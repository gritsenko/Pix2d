namespace Pix2d.Infrastructure.Tasks;

public class SimpleLongTask(string name, Func<Task> task) : ILongTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken) => await task();

    public string Name => name;
}
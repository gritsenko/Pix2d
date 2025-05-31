using System.Threading.Tasks;

namespace Pix2d.Abstract.Tools;

public interface ITool
{
    bool IsActive { get; }

    Task Activate();

    void Deactivate();
}
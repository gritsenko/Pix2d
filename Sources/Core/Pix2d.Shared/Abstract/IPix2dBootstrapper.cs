using System.Threading.Tasks;

namespace Pix2d.Abstract;

public interface IPix2dBootstrapper
{
    Task InitializeAsync();
}
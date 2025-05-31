namespace Pix2d.Abstract;

public interface IPix2dBootstrapper
{
    void Initialize();
    IServiceProvider GetServiceProvider();
    
    bool OnAppClosing();
}
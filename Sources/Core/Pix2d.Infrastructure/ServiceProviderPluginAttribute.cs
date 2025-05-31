namespace Pix2d.Infrastructure;

public class ServiceProviderPluginAttribute<TInterface, TImplementation> : ServiceProviderPluginAttribute
{
    public override Type InterfaceType => typeof(TInterface);
    public override Type InstanceType => typeof(TImplementation);
}

public abstract class ServiceProviderPluginAttribute : Attribute
{
    public abstract Type InstanceType { get; }
    public abstract Type InterfaceType { get; }
}
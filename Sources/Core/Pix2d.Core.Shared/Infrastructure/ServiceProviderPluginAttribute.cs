using System;

namespace Pix2d.Infrastructure
{
    public class ServiceProviderPluginAttribute : Attribute
    {
        public Type InterfaceType { get; }
        public Type InstanceType { get; }

        public ServiceProviderPluginAttribute(Type interfaceType, Type instanceType)
        {
            InterfaceType = interfaceType;
            InstanceType = instanceType;
        }
    }
}
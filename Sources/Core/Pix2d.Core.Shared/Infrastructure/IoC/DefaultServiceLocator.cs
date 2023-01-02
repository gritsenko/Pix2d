using System;
using System.Collections.Generic;
using System.Linq;
using CommonServiceLocator;

namespace System
{
    public class DefaultServiceLocator : IServiceLocator
    {
        private static IServiceLocator _locator;
        public static IServiceLocator ServiceLocatorProvider()
        {
            return _locator ?? (_locator = new DefaultServiceLocator());
        }

        public object GetService(Type serviceType)
        {
            return System.IoC.GetInstance(serviceType, null);
        }

        public object GetInstance(Type serviceType)
        {
            return System.IoC.GetInstance(serviceType, null);
        }

        public object GetInstance(Type serviceType, string key)
        {
            return System.IoC.GetInstance(serviceType, key);
        }

        public IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return System.IoC.GetAllInstances(serviceType);
        }

        public TService GetInstance<TService>()
        {
            return System.IoC.Get<TService>();
        }

        public TService GetInstance<TService>(string key)
        {
            return System.IoC.Get<TService>(key);
        }

        public IEnumerable<TService> GetAllInstances<TService>()
        {
            return System.IoC.GetAllInstances(typeof(TService)).Cast<TService>();
        }
    }
}
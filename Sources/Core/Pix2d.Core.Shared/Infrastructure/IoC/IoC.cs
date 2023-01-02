using System.Collections.Generic;
using System.Linq;

namespace System
{
    public static class IoC
    {
        private static readonly SimpleContainer Container = new SimpleContainer();


        static IoC()
        {
            Container.RegisterInstance<SimpleContainer>(Container);
        }

        /// <summary>
        /// Gets an instance by type and key.
        /// </summary>
        public static Func<Type, string, object> GetInstance = (service, key) => Container.GetInstance(service, key);

        /// <summary>
        /// Gets all instances of a particular type.
        /// </summary>
        public static Func<Type, IEnumerable<object>> GetAllInstances = service => Container.GetAllInstances(service);

        /// <summary>
        /// Passes an existing instance to the IoC container to enable dependencies to be injected.
        /// </summary>
        public static Action<object> BuildUp = instance =>
        {
            Container.BuildUp(instance);
        };

        /// <summary>
        /// Gets an instance from the container.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <returns>The resolved instance.</returns>
        public static T Get<T>(string key = null)
        {
            return (T) GetInstance(typeof(T), key);
        }

        /// <summary>
        /// Gets all instances of a particular type.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>The resolved instances.</returns>
        public static IEnumerable<T> GetAll<T>()
        {
            return GetAllInstances(typeof(T)).Cast<T>();
        }

        public static T Create<T>()
        {
            var result = Get<SimpleContainer>().BuildInstance(typeof(T));
            BuildUp(result);
            return (T) result;
        }
        public static TCast Create<TCast>(Type typeToCreate)
        {
            var result = Get<SimpleContainer>().BuildInstance(typeToCreate);
            BuildUp(result);
            return (TCast)result;
        }
    }
}
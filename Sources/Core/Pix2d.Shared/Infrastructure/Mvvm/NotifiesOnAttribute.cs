using System;

namespace Mvvm
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class NotifiesOnAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotifiesOnAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the independent property.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        public NotifiesOnAttribute(string name)
        {
            if (name == null) throw new ArgumentNullException("name");

            Name = name;
        }

        /// <summary>
        /// The name of the independent property.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// A unique identifier for this attribute.
        /// </summary>
        public object TypeId => this;
    }
}
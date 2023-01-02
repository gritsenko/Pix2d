using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SkiaNodes.Serialization
{
    public class WriteOnlyPropertiesContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            property.ShouldSerialize = _ => ShouldSerialize(member);

            return property;
        }

        internal static bool ShouldSerialize(MemberInfo memberInfo)
        {
            var propertyInfo = memberInfo as PropertyInfo;

            if (propertyInfo == null)
            {
                return false;
            }

            if (propertyInfo.SetMethod != null || propertyInfo.Name == "Children")
            {
                return true;
            }

            var getMethod = propertyInfo.GetMethod;

            return getMethod.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) != null; //очень много долгих вызовов 
        }
    }
}
using System;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace SkiaNodes.Serialization;

public sealed class TypeNameAssemblyExcludingSerializationBinder(Assembly[] targetAssemblies) : ISerializationBinder
{
    public void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        assemblyName = null;
        typeName = serializedType.FullName;
    }

    public Type BindToType(string assemblyName, string typeName)
    {
        var type = targetAssemblies
            .Select(x => x.GetType(typeName))
            .FirstOrDefault(x => x != null);

        if (type != null)
            return type;

        // Note: Some additional work may be required here if the assembly name has been removed
        // and you are not loading a type from the current assembly or one of the core libraries
        return Type.GetType(typeName);
    }
}
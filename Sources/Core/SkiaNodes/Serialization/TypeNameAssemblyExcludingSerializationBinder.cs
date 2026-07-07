using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace SkiaNodes.Serialization;

/// <summary>
/// Serialization binder for the node tree. On write it emits a <b>stable key</b> from
/// <see cref="NodeTypeRegistry"/> when the type is registered (refactor-proof <c>$type</c>),
/// falling back to the CLR <see cref="Type.FullName"/> for unregistered types. On read it resolves,
/// in order: stable keys and legacy aliases (<see cref="NodeTypeRegistry.TryResolve"/>), then the
/// full-name against the known assemblies (backward-compatible with files written before stable keys,
/// including assembly-qualified discriminators from very old builds — the assembly hint is ignored so
/// a type that has since moved assemblies still resolves), then a plain <see cref="Type.GetType(string)"/>.
/// An unresolved discriminator throws <see cref="UnknownNodeTypeException"/> instead of the historical
/// <c>null</c>-return that surfaced as a <see cref="NullReferenceException"/> inside Newtonsoft.
///
/// The assembly name is always dropped on write (<c>$type</c> holds only the key/full-name), which is
/// why moving a registered type between assemblies is a no-op for the format.
/// </summary>
public sealed class TypeNameAssemblyExcludingSerializationBinder(Assembly[] targetAssemblies) : ISerializationBinder
{
    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = null;

        if (NodeTypeRegistry.TryGetKey(serializedType, out var stableKey))
        {
            typeName = stableKey;
            return;
        }

        // No stable key registered: still serializable via full-name, but not refactor-proof — a later
        // rename of this type will break these files. Register a key (see ProjectFormat.EnsureInitialized).
        typeName = serializedType.FullName;
        NodeTypeRegistry.Warn(
            $"[NodeSerializer] Serializing '{serializedType.FullName}' by full-name — no stable $type key registered. " +
            "Renaming/moving this type will break older files. Register a key in ProjectFormat.EnsureInitialized.");
    }

    public Type BindToType(string? assemblyName, string typeName)
    {
        // 1. Stable key or declared legacy alias.
        if (NodeTypeRegistry.TryResolve(typeName, out var resolved))
            return resolved;

        // 2. Current full-name in a known assembly (files written before stable keys). The assembly
        //    hint is intentionally ignored so a type that moved — or was stamped with the wrong
        //    assembly by an old writer (e.g. "SKBitmapRef, Pix2d.Shared" for a SkiaNodes type) —
        //    still resolves by full-name. The SkiaNodes assembly is always scanned because it defines
        //    the base node/value types (SKBitmapRef, RootNode, …) yet is rarely in targetAssemblies.
        var type = targetAssemblies.Append(typeof(NodeTypeRegistry).Assembly)
            .Select(a => a.GetType(typeName))
            .FirstOrDefault(t => t != null);
        if (type != null)
            return type;

        // 3. BCL / assembly-qualified fallback.
        var qualified = string.IsNullOrEmpty(assemblyName) ? typeName : $"{typeName}, {assemblyName}";
        type = Type.GetType(qualified);
        if (type != null)
            return type;

        // 4. Genuinely unknown: fail with a typed, catchable exception (was a NullReferenceException).
        throw new UnknownNodeTypeException(typeName);
    }
}

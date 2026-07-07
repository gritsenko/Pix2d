using System;

namespace SkiaNodes.Serialization;

/// <summary>
/// Thrown by the serialization binder when an on-disk <c>$type</c> discriminator cannot be resolved
/// to any known runtime type (stable key, current full-name, or declared legacy alias). Previously
/// this situation produced a <see cref="NullReferenceException"/> deep inside Newtonsoft; a typed
/// exception lets the deserializer's error handler recognise it and skip the unknown node instead of
/// failing the whole load, so a file authored by a newer build or a third-party tool degrades
/// gracefully rather than refusing to open.
/// </summary>
public sealed class UnknownNodeTypeException(string typeDiscriminator)
    : Exception($"Unknown node type discriminator '{typeDiscriminator}'. It maps to no stable key, known full-name, or legacy alias.")
{
    public string TypeDiscriminator { get; } = typeDiscriminator;
}

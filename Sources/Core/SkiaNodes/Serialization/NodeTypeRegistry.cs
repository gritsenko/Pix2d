using System;
using System.Collections.Generic;
using System.Linq;

namespace SkiaNodes.Serialization;

/// <summary>
/// Maps runtime node/value types to <b>stable, refactor-proof string keys</b> used as the
/// <c>$type</c> discriminator in serialized project JSON, and resolves incoming discriminators
/// (stable keys, current type full-names, and legacy names of renamed/removed types) back to a
/// runtime type.
///
/// <para>Why this exists: before this registry the <c>$type</c> written to disk was the CLR
/// <see cref="Type.FullName"/> (namespace + type name). Renaming a class, moving it to another
/// namespace/assembly, or removing it silently broke every older <c>.pix2d</c> file — the format
/// was coupled to code internals. A concrete casualty is <c>Pix2d.CommonNodes.ArtboardNode</c>,
/// the former name of <see cref="!:Pix2dSprite"/>: files that reference it fail to load today.</para>
///
/// <para>With this registry the on-disk discriminator is decoupled from the CLR type name. New
/// files are written with short stable keys (e.g. <c>"Sprite"</c>); refactoring the backing class
/// no longer changes what is on disk. Reading stays backward-compatible: full-names of older files
/// still resolve via <see cref="TypeNameAssemblyExcludingSerializationBinder"/>'s assembly scan, and
/// deliberate renames are declared via <see cref="RegisterLegacyName"/>.</para>
///
/// Registration is process-global and idempotent. Types defined in the SkiaNodes assembly
/// self-register in the static constructor; product node types (Pix2dSprite, Layer, effects, …)
/// are registered from the product layer (see <c>Pix2d.Project.ProjectFormat.EnsureInitialized</c>).
/// </summary>
public static class NodeTypeRegistry
{
    private static readonly object _gate = new();
    private static readonly Dictionary<Type, string> _keyByType = new();
    private static readonly Dictionary<string, Type> _typeByKey = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Type> _typeByLegacyName = new(StringComparer.Ordinal);

    /// <summary>
    /// Optional sink for diagnostic warnings (e.g. a node type serialized without a registered
    /// stable key). Defaults to the console to match the rest of the serialization diagnostics.
    /// </summary>
    public static Action<string> OnWarning { get; set; } = Console.WriteLine;

    static NodeTypeRegistry()
    {
        // SkiaNodes' own persisted types self-register so they stay frozen even when the library
        // is used without the product layer. SKBitmapRef is by far the most common ($type per layer).
        Register("BitmapRef", typeof(SKBitmapRef));
        Register("Root", typeof(RootNode));
        Register("Group", typeof(GroupNode));
    }

    /// <summary>
    /// Binds a runtime <paramref name="type"/> to a <paramref name="stableKey"/> used as its
    /// on-disk <c>$type</c>. Idempotent for identical mappings; throws on a conflicting remap so
    /// duplicate keys or copy-paste typos surface at startup rather than corrupting files.
    /// </summary>
    public static void Register(string stableKey, Type type)
    {
        if (string.IsNullOrEmpty(stableKey)) throw new ArgumentException("Stable key must be non-empty.", nameof(stableKey));
        if (type == null) throw new ArgumentNullException(nameof(type));

        lock (_gate)
        {
            if (_typeByKey.TryGetValue(stableKey, out var existingType))
            {
                if (existingType != type)
                    throw new InvalidOperationException(
                        $"Node type key '{stableKey}' already maps to {existingType.FullName}; cannot remap to {type.FullName}.");
                return; // identical mapping — idempotent
            }

            if (_keyByType.TryGetValue(type, out var existingKey) && existingKey != stableKey)
                throw new InvalidOperationException(
                    $"Type {type.FullName} is already registered with key '{existingKey}'; cannot add key '{stableKey}'.");

            _typeByKey[stableKey] = type;
            _keyByType[type] = stableKey;
        }
    }

    /// <summary>
    /// Declares that a legacy on-disk type discriminator (typically the CLR full-name a type used to
    /// have before it was renamed or removed) should resolve to <paramref name="currentType"/> when
    /// reading old files. Write path is unaffected — new files always use the current stable key.
    /// </summary>
    public static void RegisterLegacyName(string legacyTypeName, Type currentType)
    {
        if (string.IsNullOrEmpty(legacyTypeName)) throw new ArgumentException("Legacy type name must be non-empty.", nameof(legacyTypeName));
        if (currentType == null) throw new ArgumentNullException(nameof(currentType));

        lock (_gate)
        {
            if (_typeByLegacyName.TryGetValue(legacyTypeName, out var existing) && existing != currentType)
                throw new InvalidOperationException(
                    $"Legacy type name '{legacyTypeName}' is already mapped to {existing.FullName}; cannot remap to {currentType.FullName}.");

            _typeByLegacyName[legacyTypeName] = currentType;
        }
    }

    /// <summary>Returns the stable key for a runtime type, if one is registered.</summary>
    public static bool TryGetKey(Type type, out string key)
    {
        lock (_gate)
            return _keyByType.TryGetValue(type, out key!);
    }

    /// <summary>
    /// Snapshot of all stable-key registrations (key → type), for tooling such as the serialization
    /// contract check. Legacy aliases are intentionally excluded — they are read-only mappings.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, Type>> Registrations
    {
        get
        {
            lock (_gate)
                return _typeByKey.ToArray();
        }
    }

    /// <summary>
    /// Resolves an incoming <c>$type</c> discriminator to a runtime type via, in order,
    /// the stable-key map then the legacy-name map. Current full-names are intentionally not
    /// resolved here — the binder handles those through its assembly scan.
    /// </summary>
    public static bool TryResolve(string typeDiscriminator, out Type type)
    {
        lock (_gate)
        {
            if (_typeByKey.TryGetValue(typeDiscriminator, out type!)) return true;
            if (_typeByLegacyName.TryGetValue(typeDiscriminator, out type!)) return true;
        }

        type = null!;
        return false;
    }

    /// <summary>Routes a diagnostic message to <see cref="OnWarning"/>.</summary>
    public static void Warn(string message) => OnWarning?.Invoke(message);
}

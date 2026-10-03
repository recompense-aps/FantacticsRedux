using System.Collections.Concurrent;
using System.Reflection;
using Godot;

namespace Fantactics.Client.App;

/// <summary>
/// Finds <c>[Export]</c> fields a scene left unassigned: fields of this project's node scripts that are declared
/// non-nullable (<c>private Label _title = null!;</c>) but are still <c>null</c> when the node enters the tree.
/// An export declared nullable (<c>Label? _x</c>) is optional and never reported.
/// </summary>
public static class ExportCheck
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _required = new();

    /// <summary>The unassigned required exports of <paramref name="node"/>, as <c>Type._field</c>.</summary>
    public static IEnumerable<string> Missing(Node node)
    {
        Type type = node.GetType();
        if (type.Assembly != typeof(ExportCheck).Assembly)
        {
            return [];
        }

        return _required
            .GetOrAdd(type, RequiredExports)
            .Where(field => field.GetValue(node) is null)
            .Select(field => $"{type.Name}.{field.Name} ({node.GetPath()})");
    }

    private static FieldInfo[] RequiredExports(Type type)
    {
        NullabilityInfoContext nullability = new();
        return
        [
            .. type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.GetCustomAttribute<ExportAttribute>() is not null)
                .Where(field => !field.FieldType.IsValueType)
                .Where(field => nullability.Create(field).WriteState == NullabilityState.NotNull),
        ];
    }
}

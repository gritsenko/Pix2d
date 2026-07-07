using Newtonsoft.Json.Linq;

namespace Pix2d.Project;

/// <summary>
/// One step of the project-format migration pipeline: transforms a scene JSON document from
/// <see cref="FromVersion"/> to <c>FromVersion + 1</c>. Migrations operate on the parsed
/// <see cref="JObject"/> before it is deserialized into runtime nodes, and must never mutate the
/// original file in place — the runner feeds the output of one step into the next.
///
/// Register implementations in <see cref="ProjectFormat"/> (there are none yet — the pipeline ships
/// dormant so the first real schema change has a home). Keep each migration pure and self-contained:
/// it must not depend on the current runtime node classes, only on the JSON shape of its own version.
/// </summary>
public interface ISceneJsonMigration
{
    /// <summary>Version this migration upgrades <b>from</b>; it produces <c>FromVersion + 1</c>.</summary>
    int FromVersion { get; }

    /// <summary>Returns the upgraded document. May mutate and return <paramref name="sceneRoot"/>.</summary>
    JObject Migrate(JObject sceneRoot);
}

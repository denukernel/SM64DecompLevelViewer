using YamlDotNet.Serialization;

namespace Sm64DecompLevelViewer.Models;

/// <summary>
/// Represents the metadata from a level.yaml file in the SM64 decomp project.
/// </summary>
public class LevelMetadata
{
    [YamlMember(Alias = "short-name")]
    public string ShortName { get; set; } = string.Empty;

    [YamlMember(Alias = "full-name")]
    public string FullName { get; set; } = string.Empty;

    [YamlMember(Alias = "texture-file")]
    public List<string> TextureFiles { get; set; } = new();

    [YamlMember(Alias = "area-count")]
    public int AreaCount { get; set; }

    [YamlMember(Alias = "objects")]
    public List<string> Objects { get; set; } = new();

    [YamlMember(Alias = "shared-path")]
    public List<string> SharedPath { get; set; } = new();

    [YamlMember(Alias = "skybox-bin")]
    public string? SkyboxBin { get; set; }

    [YamlMember(Alias = "texture-bin")]
    public string TextureBin { get; set; } = string.Empty;

    [YamlMember(Alias = "effects")]
    public bool Effects { get; set; }

    [YamlMember(Alias = "actor-bins")]
    public List<string> ActorBins { get; set; } = new();

    [YamlMember(Alias = "common-bin")]
    public List<string> CommonBin { get; set; } = new();

    /// <summary>
    /// Full path to the level directory
    /// </summary>
    [YamlIgnore]
    public string LevelPath { get; set; } = string.Empty;

    /// <summary>
    /// Category of the level (Course, Bowser, Castle, Special, Menu)
    /// </summary>
    [YamlIgnore]
    public string Category { get; set; } = "Unknown";

    public override string ToString() => FullName;
}

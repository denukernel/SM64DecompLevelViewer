using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Sm64DecompLevelViewer.Models;

namespace Sm64DecompLevelViewer.Services;

/// <summary>
/// Service for parsing level.yaml files from the SM64 decomp project.
/// </summary>
public class YamlLevelParser
{
    private readonly IDeserializer _deserializer;

    public YamlLevelParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses a level.yaml file and returns the level metadata.
    /// </summary>
    /// <param name="yamlFilePath">Full path to the level.yaml file</param>
    /// <returns>LevelMetadata object or null if parsing fails</returns>
    public LevelMetadata? ParseLevelYaml(string yamlFilePath)
    {
        try
        {
            if (!File.Exists(yamlFilePath))
            {
                Console.WriteLine($"YAML file not found: {yamlFilePath}");
                return null;
            }

            var yamlContent = File.ReadAllText(yamlFilePath);
            var levelMetadata = _deserializer.Deserialize<LevelMetadata>(yamlContent);

            if (levelMetadata != null)
            {
                // Set the level path (parent directory of the yaml file)
                levelMetadata.LevelPath = Path.GetDirectoryName(yamlFilePath) ?? string.Empty;
            }

            return levelMetadata;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing YAML file {yamlFilePath}: {ex.Message}");
            return null;
        }
    }
}

using System.IO;
using System.Text.RegularExpressions;
using Sm64DecompLevelViewer.Models;

namespace Sm64DecompLevelViewer.Services;

public class ObjectParser
{
    private static readonly Regex ObjectPattern = new Regex(
        @"OBJECT(?:_WITH_ACTS)?\s*\(\s*(?:/\*[^/]+\*/\s*)?([^,]+),\s*(?:/\*[^/]+\*/\s*)?(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(?:/\*[^/]+\*/\s*)?(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(?:/\*[^/]+\*/\s*)?([^,]+),\s*(?:/\*[^/]+\*/\s*)?([^,)]+)(?:,\s*([^)]+))?\)",
        RegexOptions.Compiled | RegexOptions.Multiline
    );

    private static readonly Regex MarioPosPattern = new Regex(
        @"MARIO_POS\s*\(\s*([^,]+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\s*\)",
        RegexOptions.Compiled
    );

    private static readonly Regex LoadModelPattern = new Regex(
        @"LOAD_MODEL_FROM_GEO\s*\(\s*([^,]+),\s*([^)]+)\)",
        RegexOptions.Compiled
    );

    public List<LevelObject> ParseScriptFile(string filePath)
    {
        var objects = new List<LevelObject>();

        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Script file not found: {filePath}");
                return objects;
            }

            string content = File.ReadAllText(filePath);
            var matches = ObjectPattern.Matches(content);

            Console.WriteLine($"Found {matches.Count} objects in {Path.GetFileName(filePath)}");

            foreach (Match match in matches)
            {
                try
                {
                    var obj = new LevelObject
                    {
                        ModelName = match.Groups[1].Value.Trim(),
                        X = int.Parse(match.Groups[2].Value),
                        Y = int.Parse(match.Groups[3].Value),
                        Z = int.Parse(match.Groups[4].Value),
                        RX = int.Parse(match.Groups[5].Value),
                        RY = int.Parse(match.Groups[6].Value),
                        RZ = int.Parse(match.Groups[7].Value),
                        Behavior = match.Groups[9].Value.Trim()
                    };

                    string paramsStr = match.Groups[8].Value.Trim();
                    if (paramsStr.StartsWith("0x"))
                    {
                        obj.Params = Convert.ToUInt32(paramsStr, 16);
                    }
                    else if (uint.TryParse(paramsStr, out uint paramValue))
                    {
                        obj.Params = paramValue;
                    }
                    else
                    {
                        obj.Params = 0;
                    }

                    objects.Add(obj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse object: {ex.Message}");
                }
            }

            var marioMatches = MarioPosPattern.Matches(content);
            foreach (Match match in marioMatches)
            {
                objects.Add(new LevelObject
                {
                    ModelName = "MODEL_MARIO",
                    X = int.Parse(match.Groups[3].Value),
                    Y = int.Parse(match.Groups[4].Value),
                    Z = int.Parse(match.Groups[5].Value),
                    RY = int.Parse(match.Groups[2].Value),
                    Behavior = "bhvMario"
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing script file {filePath}: {ex.Message}");
        }

        return objects;
    }

    private static readonly Regex MacroObjectsPattern = new Regex(
        @"MACRO_OBJECTS\s*\(\s*/\*objList\*/\s*([^)]+)\)",
        RegexOptions.Compiled
    );

    public string? ParseMacroListName(string scriptFilePath)
    {
        if (!File.Exists(scriptFilePath)) return null;

        try
        {
            string content = File.ReadAllText(scriptFilePath);
            var match = MacroObjectsPattern.Match(content);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing MACRO_OBJECTS in script {scriptFilePath}: {ex.Message}");
        }

        return null;
    }

    public Dictionary<string, string> ParseLoadModels(string filePath)
    {
        var mapping = new Dictionary<string, string>();
        if (!File.Exists(filePath)) return mapping;

        try
        {
            string content = File.ReadAllText(filePath);
            var matches = LoadModelPattern.Matches(content);
            foreach (Match match in matches)
            {
                mapping[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing LOAD_MODEL_FROM_GEO: {ex.Message}");
        }

        return mapping;
    }
}

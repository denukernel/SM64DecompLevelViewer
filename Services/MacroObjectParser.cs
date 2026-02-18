using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Sm64DecompLevelViewer.Models;

namespace Sm64DecompLevelViewer.Services
{
    public class MacroPreset
    {
        public string Behavior { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Param { get; set; }
    }

    public class MacroObjectParser
    {
        // Regex to parse /* macro_name */ { behavior, model, param }
        private static readonly Regex PresetPattern = new Regex(
            @"\/\*\s*([a-zA-Z0-9_]+)\s*\*\/\s*\{\s*([a-zA-Z0-9_]+),\s*([a-zA-Z0-9_]+),\s*([^}]+)\}",
            RegexOptions.Compiled);

        // Regex to parse MACRO_OBJECT(/*preset*/ macro_name, /*yaw*/ yaw, /*pos*/ x, y, z)
        private static readonly Regex MacroObjectPattern = new Regex(
            @"MACRO_OBJECT\s*\(\s*/\*preset\*/\s*([^,]+),\s*/\*yaw\*/\s*(-?\d+),\s*/\*pos\*/\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\s*\)",
            RegexOptions.Compiled);

        // Regex to parse MACRO_OBJECT_WITH_BHV_PARAM(/*preset*/ macro_name, /*yaw*/ yaw, /*pos*/ x, y, z, /*bhvParam*/ param)
        private static readonly Regex MacroObjectWithParamPattern = new Regex(
            @"MACRO_OBJECT_WITH_BHV_PARAM\s*\(\s*/\*preset\*/\s*([^,]+),\s*/\*yaw\*/\s*(-?\d+),\s*/\*pos\*/\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*/\*bhvParam\*/\s*([^)]+)\)",
            RegexOptions.Compiled);

        public Dictionary<string, MacroPreset> ParsePresets(string filePath)
        {
            var presets = new Dictionary<string, MacroPreset>();
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Macro presets file not found: {filePath}");
                return presets;
            }

            try
            {
                string content = File.ReadAllText(filePath);
                var matches = PresetPattern.Matches(content);

                foreach (Match match in matches)
                {
                    string presetName = match.Groups[1].Value.Trim();
                    string behavior = match.Groups[2].Value.Trim();
                    string model = match.Groups[3].Value.Trim();
                    string paramStr = match.Groups[4].Value.Trim();

                    // Simplify param evaluation (basic ORs and literals)
                    int paramValue = 0;
                    if (paramStr.Contains("|"))
                    {
                        // Just extract numbers if it's a bitwise OR of constants for now
                        // Or handle common ones if needed
                    }
                    else if (int.TryParse(paramStr, out int p))
                    {
                        paramValue = p;
                    }

                    presets[presetName] = new MacroPreset
                    {
                        Behavior = behavior,
                        Model = model,
                        Param = paramValue
                    };
                }
                Console.WriteLine($"Parsed {presets.Count} macro presets from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing macro presets: {ex.Message}");
            }

            return presets;
        }

        public List<LevelObject> ParseMacroFile(string filePath, Dictionary<string, MacroPreset> presets)
        {
            var objects = new List<LevelObject>();
            if (!File.Exists(filePath)) return objects;

            try
            {
                string content = File.ReadAllText(filePath);
                
                // Process MACRO_OBJECT entries
                var macroMatches = MacroObjectPattern.Matches(content);
                foreach (Match match in macroMatches)
                {
                    string presetName = match.Groups[1].Value.Trim();
                    if (presets.TryGetValue(presetName, out var preset))
                    {
                        objects.Add(new LevelObject
                        {
                            ModelName = preset.Model,
                            Behavior = preset.Behavior,
                            X = int.Parse(match.Groups[3].Value),
                            Y = int.Parse(match.Groups[4].Value),
                            Z = int.Parse(match.Groups[5].Value),
                            RY = int.Parse(match.Groups[2].Value), // Already in degrees in C source
                            Params = (uint)preset.Param
                        });
                    }
                }

                // Process MACRO_OBJECT_WITH_BHV_PARAM entries
                var paramMatches = MacroObjectWithParamPattern.Matches(content);
                foreach (Match match in paramMatches)
                {
                    string presetName = match.Groups[1].Value.Trim();
                    if (presets.TryGetValue(presetName, out var preset))
                    {
                        string paramStr = match.Groups[6].Value.Trim();
                        uint paramValue = 0;
                        if (paramStr.StartsWith("0x")) paramValue = Convert.ToUInt32(paramStr, 16);
                        else uint.TryParse(paramStr, out paramValue);

                        objects.Add(new LevelObject
                        {
                            ModelName = preset.Model,
                            Behavior = preset.Behavior,
                            X = int.Parse(match.Groups[3].Value),
                            Y = int.Parse(match.Groups[4].Value),
                            Z = int.Parse(match.Groups[5].Value),
                            RY = int.Parse(match.Groups[2].Value), // Already in degrees in C source
                            Params = paramValue | (uint)preset.Param // Combine preset default and instance param
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing macro file {filePath}: {ex.Message}");
            }

            return objects;
        }

        /// <summary>
        /// No longer needed as we parse the C source directly which uses degrees.
        /// </summary>
        [Obsolete("The C source already uses degrees.")]
        public static float ConvertMacroRotation(int rawRotation)
        {
            return rawRotation;
        }
    }
}

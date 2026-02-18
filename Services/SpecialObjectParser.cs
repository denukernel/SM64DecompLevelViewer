using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Sm64DecompLevelViewer.Services
{
    public class SpecialObjectParser
    {
        private static readonly Regex PresetPattern = new Regex(
            @"\{\s*([a-zA-Z0-9_]+),\s*SPTYPE_[a-zA-Z0-9_]+,\s*(?:0x[a-fA-F0-9]+|\d+),\s*([a-zA-Z0-9_]+)",
            RegexOptions.Compiled);

        public Dictionary<string, string> ParsePresets(string filePath)
        {
            var mapping = new Dictionary<string, string>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Special presets file not found: {filePath}");
                return mapping;
            }

            try
            {
                string content = File.ReadAllText(filePath);
                var matches = PresetPattern.Matches(content);

                foreach (Match match in matches)
                {
                    string presetName = match.Groups[1].Value;
                    string modelId = match.Groups[2].Value;
                    
                    if (!mapping.ContainsKey(presetName))
                    {
                        mapping[presetName] = modelId;
                    }
                }
                
                Console.WriteLine($"Parsed {mapping.Count} special object presets from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing special presets: {ex.Message}");
            }

            return mapping;
        }
    }
}

using System.IO;
using System.Text.RegularExpressions;
using OpenTK.Mathematics;
using Sm64DecompLevelViewer.Models;

namespace Sm64DecompLevelViewer.Services;

/// <summary>
/// Parses SM64 geometry layout files (geo.inc.c) to extract transformation data.
/// Phase 4B: Full transformation support with rotation, scale, and hierarchical scene graphs.
/// </summary>
public class GeoLayoutParser
{
    // Regex patterns for GEO commands
    private static readonly Regex TranslatePattern = new(
        @"GEO_TRANSLATE\s*\(\s*\w+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex TranslateWithDlPattern = new(
        @"GEO_TRANSLATE_WITH_DL\s*\(\s*\w+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex RotatePattern = new(
        @"GEO_ROTATE\s*\(\s*\w+\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex RotateWithDlPattern = new(
        @"GEO_ROTATE_WITH_DL\s*\(\s*\w+\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex ScalePattern = new(
        @"GEO_SCALE\s*\(\s*\w+\s*,\s*(0x[0-9a-fA-F]+|\d+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex ScaleWithDlPattern = new(
        @"GEO_SCALE_WITH_DL\s*\(\s*\w+\s*,\s*(0x[0-9a-fA-F]+|\d+)\s*,\s*(\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex TranslateRotatePattern = new(
        @"GEO_TRANSLATE_ROTATE\s*\(\s*\w+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex TranslateRotateWithDlPattern = new(
        @"GEO_TRANSLATE_ROTATE_WITH_DL\s*\(\s*\w+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(-?(?:0x[0-9a-fA-F]+|\d+))\s*,\s*(\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex DisplayListPattern = new(
        @"GEO_DISPLAY_LIST\s*\(\s*\w+\s*,\s*(\w+)\s*\)",
        RegexOptions.Compiled);

    public GeoNode? ParseGeoLayout(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Geo layout file not found: {filePath}");
            return null;
        }

        var content = File.ReadAllText(filePath);
        var root = new GeoNode(GeoNodeType.Root);

        // Parse all transformation commands
        ParseAllCommands(content, root);

        return root;
    }

    private void ParseAllCommands(string content, GeoNode root)
    {
        // Parse GEO_TRANSLATE commands
        foreach (Match match in TranslatePattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.Translate);
            node.Translation = new Vector3(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_TRANSLATE: {node.Translation}");
        }

        // Parse GEO_TRANSLATE_WITH_DL commands
        foreach (Match match in TranslateWithDlPattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.Translate);
            node.Translation = new Vector3(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
            node.DisplayListName = match.Groups[4].Value;
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_TRANSLATE_WITH_DL: {node.Translation}, DL: {node.DisplayListName}");
        }

        // Parse GEO_ROTATE commands
        foreach (Match match in RotatePattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.Rotate);
            node.Rotation = new Vector3(
                ConvertAngleToDegrees(match.Groups[1].Value),
                ConvertAngleToDegrees(match.Groups[2].Value),
                ConvertAngleToDegrees(match.Groups[3].Value)
            );
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_ROTATE: {node.Rotation} degrees");
        }

        // Parse GEO_ROTATE_WITH_DL commands
        foreach (Match match in RotateWithDlPattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.Rotate);
            node.Rotation = new Vector3(
                ConvertAngleToDegrees(match.Groups[1].Value),
                ConvertAngleToDegrees(match.Groups[2].Value),
                ConvertAngleToDegrees(match.Groups[3].Value)
            );
            node.DisplayListName = match.Groups[4].Value;
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_ROTATE_WITH_DL: {node.Rotation} degrees, DL: {node.DisplayListName}");
        }

        // Parse GEO_SCALE commands
        foreach (Match match in ScalePattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.Scale);
            node.Scale = ConvertScale(match.Groups[1].Value);
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_SCALE: {node.Scale}");
        }

        // Parse GEO_SCALE_WITH_DL commands
        foreach (Match match in ScaleWithDlPattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.Scale);
            node.Scale = ConvertScale(match.Groups[1].Value);
            node.DisplayListName = match.Groups[2].Value;
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_SCALE_WITH_DL: {node.Scale}, DL: {node.DisplayListName}");
        }

        // Parse GEO_TRANSLATE_ROTATE commands
        foreach (Match match in TranslateRotatePattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.TranslateRotate);
            node.Translation = new Vector3(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
            node.Rotation = new Vector3(
                ConvertAngleToDegrees(match.Groups[4].Value),
                ConvertAngleToDegrees(match.Groups[5].Value),
                ConvertAngleToDegrees(match.Groups[6].Value)
            );
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_TRANSLATE_ROTATE: T:{node.Translation}, R:{node.Rotation} degrees");
        }

        // Parse GEO_TRANSLATE_ROTATE_WITH_DL commands
        foreach (Match match in TranslateRotateWithDlPattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.TranslateRotate);
            node.Translation = new Vector3(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
            node.Rotation = new Vector3(
                ConvertAngleToDegrees(match.Groups[4].Value),
                ConvertAngleToDegrees(match.Groups[5].Value),
                ConvertAngleToDegrees(match.Groups[6].Value)
            );
            node.DisplayListName = match.Groups[7].Value;
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_TRANSLATE_ROTATE_WITH_DL: T:{node.Translation}, R:{node.Rotation} degrees, DL: {node.DisplayListName}");
        }

        // Parse GEO_DISPLAY_LIST commands (for reference)
        foreach (Match match in DisplayListPattern.Matches(content))
        {
            var node = new GeoNode(GeoNodeType.DisplayList);
            node.DisplayListName = match.Groups[1].Value;
            root.Children.Add(node);
            Console.WriteLine($"Found GEO_DISPLAY_LIST: {node.DisplayListName}");
        }
    }

    /// <summary>
    /// Helper to find the first display list name in a geo layout file.
    /// Useful for simple modular pieces that don't have complex hierarchies.
    /// </summary>
    public string? GetPrimaryDisplayListName(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        string content = File.ReadAllText(filePath);
        
        // Try to find any DISPLAY_LIST command
        var match = DisplayListPattern.Match(content);
        if (match.Success) return match.Groups[1].Value;

        // Try commands with DL
        match = TranslateWithDlPattern.Match(content);
        if (match.Success) return match.Groups[4].Value;

        match = RotateWithDlPattern.Match(content);
        if (match.Success) return match.Groups[4].Value;

        match = ScaleWithDlPattern.Match(content);
        if (match.Success) return match.Groups[2].Value;

        match = TranslateRotateWithDlPattern.Match(content);
        if (match.Success) return match.Groups[7].Value;

        return null;
    }

    /// <summary>
    /// Converts SM64 angle units to degrees.
    /// SM64 uses: 0x10000 = 360 degrees
    /// </summary>
    private float ConvertAngleToDegrees(string angleStr)
    {
        long angleUnits;
        if (angleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            angleUnits = Convert.ToInt64(angleStr, 16);
        else
            angleUnits = long.Parse(angleStr);

        // Convert: (angleUnits / 65536.0) * 360.0
        return (float)((angleUnits / 65536.0) * 360.0);
    }

    /// <summary>
    /// Converts SM64 scale value to float.
    /// SM64 uses: 0x10000 = 1.0 (65536 = 1.0)
    /// </summary>
    private float ConvertScale(string scaleStr)
    {
        long scaleUnits;
        if (scaleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            scaleUnits = Convert.ToInt64(scaleStr, 16);
        else
            scaleUnits = long.Parse(scaleStr);

        // Convert: scaleUnits / 65536.0
        return (float)(scaleUnits / 65536.0);
    }

    /// <summary>
    /// Extracts world-space transformations from the scene graph.
    /// For Phase 4B, we build proper transformation matrices.
    /// </summary>
    public Dictionary<string, Matrix4> ExtractTransformations(GeoNode root)
    {
        var transforms = new Dictionary<string, Matrix4>();

        foreach (var child in root.Children)
        {
            if (child.DisplayListName != null)
            {
                var transform = BuildTransformMatrix(child);
                transforms[child.DisplayListName] = transform;
                Console.WriteLine($"Transform for {child.DisplayListName}: T:{child.Translation}, R:{child.Rotation}, S:{child.Scale}");
            }
        }

        return transforms;
    }

    /// <summary>
    /// Builds a transformation matrix from a GeoNode.
    /// Order: Scale * Rotation * Translation (TRS)
    /// </summary>
    private Matrix4 BuildTransformMatrix(GeoNode node)
    {
        var translation = Matrix4.CreateTranslation(node.Translation);
        
        // Convert degrees to radians for OpenTK
        var rotX = MathHelper.DegreesToRadians(node.Rotation.X);
        var rotY = MathHelper.DegreesToRadians(node.Rotation.Y);
        var rotZ = MathHelper.DegreesToRadians(node.Rotation.Z);
        
        var rotation = Matrix4.CreateRotationX(rotX) *
                       Matrix4.CreateRotationY(rotY) *
                       Matrix4.CreateRotationZ(rotZ);
        
        var scale = Matrix4.CreateScale(node.Scale);

        // Combine: Scale * Rotation * Translation
        return scale * rotation * translation;
    }
}

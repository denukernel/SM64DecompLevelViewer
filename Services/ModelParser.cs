using System.IO;
using System.Text.RegularExpressions;
using OpenTK.Mathematics;
using Sm64DecompLevelViewer.Models;

namespace Sm64DecompLevelViewer.Services;

/// <summary>
/// Parser for SM64 decomp model.inc.c files.
/// Extracts visual geometry (vertices with UVs/normals and triangles) from Vtx arrays and display lists.
/// </summary>
public class ModelParser
{
    // Regex patterns for parsing model data
    private static readonly Regex VtxArrayPattern = new(@"static\s+const\s+Vtx\s+(\w+)\[\]\s*=\s*\{", RegexOptions.Compiled);
    
    
    private static readonly Regex VtxDataPattern = new(
        @"\{\{\{\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\},\s*\d+,\s*\{\s*(-?\d+),\s*(-?\d+)\},\s*\{(0x[0-9a-fA-F]{2}),\s*(0x[0-9a-fA-F]{2}),\s*(0x[0-9a-fA-F]{2}),\s*(0x[0-9a-fA-F]{2})\}\}\}",
        RegexOptions.Compiled);
    
    private static readonly Regex SpVertexPattern = new(@"gsSPVertex\((\w+),\s*(\d+),\s*(\d+)\)", RegexOptions.Compiled);
    private static readonly Regex Sp2TrianglesPattern = new(@"gsSP2Triangles\(\s*(\d+),\s*(\d+),\s*(\d+),\s*0x[0-9a-fA-F]+,\s*(\d+),\s*(\d+),\s*(\d+),\s*0x[0-9a-fA-F]+\)", RegexOptions.Compiled);
    private static readonly Regex Sp1TrianglePattern = new(@"gsSP1Triangle\(\s*(\d+),\s*(\d+),\s*(\d+),\s*0x[0-9a-fA-F]+\)", RegexOptions.Compiled);
    private static readonly Regex DisplayListPattern = new(@"const\s+Gfx\s+(\w+)\[\]\s*=\s*\{", RegexOptions.Compiled);

    /// <summary>
    /// Parses a model.inc.c file and returns the visual mesh.
    /// </summary>
    public VisualMesh? ParseModelFile(string modelFilePath, string areaName, string levelName)
    {
        try
        {
            if (!File.Exists(modelFilePath))
            {
                Console.WriteLine($"Model file not found: {modelFilePath}");
                return null;
            }

            var fileContent = File.ReadAllText(modelFilePath);
            var mesh = new VisualMesh
            {
                AreaName = areaName,
                LevelName = levelName
            };

            // Parse all vertex arrays
            var vertexArrays = ParseVertexArrays(fileContent);
            Console.WriteLine($"Found {vertexArrays.Count} vertex arrays");

            // Parse display lists to extract triangles
            ParseDisplayLists(fileContent, vertexArrays, mesh);

            Console.WriteLine($"Parsed visual mesh: {mesh}");
            return mesh;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing model file {modelFilePath}: {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, List<ModelVertex>> ParseVertexArrays(string content)
    {
        var vertexArrays = new Dictionary<string, List<ModelVertex>>();

        // Find all vertex array declarations
        var arrayMatches = VtxArrayPattern.Matches(content);
        
        foreach (Match arrayMatch in arrayMatches)
        {
            string arrayName = arrayMatch.Groups[1].Value;
            int startIndex = arrayMatch.Index + arrayMatch.Length;
            
            // Find the closing brace for this array
            int braceCount = 1;
            int endIndex = startIndex;
            for (int i = startIndex; i < content.Length && braceCount > 0; i++)
            {
                if (content[i] == '{') braceCount++;
                if (content[i] == '}') braceCount--;
                endIndex = i;
            }

            string arrayContent = content.Substring(startIndex, endIndex - startIndex);
            
            // Parse vertices in this array
            var vertices = new List<ModelVertex>();
            var vtxMatches = VtxDataPattern.Matches(arrayContent);
            
            foreach (Match vtxMatch in vtxMatches)
            {
                int x = int.Parse(vtxMatch.Groups[1].Value);
                int y = int.Parse(vtxMatch.Groups[2].Value);
                int z = int.Parse(vtxMatch.Groups[3].Value);
                int s = int.Parse(vtxMatch.Groups[4].Value);
                int t = int.Parse(vtxMatch.Groups[5].Value);
                byte nx = Convert.ToByte(vtxMatch.Groups[6].Value, 16);
                byte ny = Convert.ToByte(vtxMatch.Groups[7].Value, 16);
                byte nz = Convert.ToByte(vtxMatch.Groups[8].Value, 16);
                byte alpha = Convert.ToByte(vtxMatch.Groups[9].Value, 16);

                vertices.Add(new ModelVertex(x, y, z, s, t, nx, ny, nz, alpha));
            }

            if (vertices.Count > 0)
            {
                vertexArrays[arrayName] = vertices;
                Console.WriteLine($"  {arrayName}: {vertices.Count} vertices");
            }
        }

        return vertexArrays;
    }

    private void ParseDisplayLists(string content, Dictionary<string, List<ModelVertex>> vertexArrays, VisualMesh mesh)
    {
        // Find display list sections
        var dlMatches = DisplayListPattern.Matches(content);
        
        foreach (Match dlMatch in dlMatches)
        {
            string dlName = dlMatch.Groups[1].Value;
            mesh.DisplayListNames.Add(dlName);
            if (mesh.MainDisplayListName == null)
            {
                mesh.MainDisplayListName = dlName;
            }

            int startIndex = dlMatch.Index + dlMatch.Length;
            
            // Find the closing brace
            int braceCount = 1;
            int endIndex = startIndex;
            for (int i = startIndex; i < content.Length && braceCount > 0; i++)
            {
                if (content[i] == '{') braceCount++;
                if (content[i] == '}') braceCount--;
                endIndex = i;
            }

            string dlContent = content.Substring(startIndex, endIndex - startIndex);
            
            // Process this display list
            ProcessDisplayList(dlContent, vertexArrays, mesh);
        }
    }

    private void ProcessDisplayList(string dlContent, Dictionary<string, List<ModelVertex>> vertexArrays, VisualMesh mesh)
    {
        // Track current vertex buffer (loaded by gsSPVertex)
        List<ModelVertex>? currentVertexBuffer = null;
        int vertexBufferOffset = 0;

        // Process gsSPVertex commands
        var spVertexMatches = SpVertexPattern.Matches(dlContent);
        var sp2TriMatches = Sp2TrianglesPattern.Matches(dlContent);
        var sp1TriMatches = Sp1TrianglePattern.Matches(dlContent);

        // Build a list of all commands with their positions
        var commands = new List<(int pos, string type, Match match)>();
        
        foreach (Match m in spVertexMatches)
            commands.Add((m.Index, "vertex", m));
        foreach (Match m in sp2TriMatches)
            commands.Add((m.Index, "tri2", m));
        foreach (Match m in sp1TriMatches)
            commands.Add((m.Index, "tri1", m));

        // Sort by position
        commands.Sort((a, b) => a.pos.CompareTo(b.pos));

        // Process commands in order
        foreach (var (pos, type, match) in commands)
        {
            if (type == "vertex")
            {
                string arrayName = match.Groups[1].Value;
                int count = int.Parse(match.Groups[2].Value);
                vertexBufferOffset = int.Parse(match.Groups[3].Value);

                if (vertexArrays.ContainsKey(arrayName))
                {
                    currentVertexBuffer = vertexArrays[arrayName];
                    
                    // Add vertices to mesh (only if not already added)
                    int meshVertexStart = mesh.Vertices.Count;
                    for (int i = 0; i < count && i < currentVertexBuffer.Count; i++)
                    {
                        mesh.Vertices.Add(currentVertexBuffer[i]);
                    }
                }
            }
            else if (type == "tri2" && currentVertexBuffer != null)
            {
                int v1 = int.Parse(match.Groups[1].Value);
                int v2 = int.Parse(match.Groups[2].Value);
                int v3 = int.Parse(match.Groups[3].Value);
                int v4 = int.Parse(match.Groups[4].Value);
                int v5 = int.Parse(match.Groups[5].Value);
                int v6 = int.Parse(match.Groups[6].Value);

                // Calculate absolute indices
                int baseIndex = mesh.Vertices.Count - currentVertexBuffer.Count;
                
                mesh.Triangles.Add(new ModelTriangle(baseIndex + v1, baseIndex + v2, baseIndex + v3));
                mesh.Triangles.Add(new ModelTriangle(baseIndex + v4, baseIndex + v5, baseIndex + v6));
            }
            else if (type == "tri1" && currentVertexBuffer != null)
            {
                int v1 = int.Parse(match.Groups[1].Value);
                int v2 = int.Parse(match.Groups[2].Value);
                int v3 = int.Parse(match.Groups[3].Value);

                int baseIndex = mesh.Vertices.Count - currentVertexBuffer.Count;
                mesh.Triangles.Add(new ModelTriangle(baseIndex + v1, baseIndex + v2, baseIndex + v3));
            }
        }
    }

    /// <summary>
    /// Finds all model.inc.c files in a level directory, grouped by area.
    /// Returns a dictionary where key is area name and value is list of model file paths.
    /// </summary>
    public Dictionary<string, List<string>> FindModelFiles(string levelPath)
    {
        var modelFiles = new Dictionary<string, List<string>>();

        try
        {
            var areasPath = Path.Combine(levelPath, "areas");
            if (!Directory.Exists(areasPath))
            {
                return modelFiles;
            }

            // Find all area directories
            var areaDirectories = Directory.GetDirectories(areasPath);
            foreach (var areaDir in areaDirectories)
            {
                var areaNumber = Path.GetFileName(areaDir);
                var areaName = $"Area {areaNumber}";
                var areaModelFiles = new List<string>();
                
                // Find all sub-area directories (e.g., areas/1/1/, areas/1/2/, etc.)
                var subAreaDirectories = Directory.GetDirectories(areaDir);
                foreach (var subAreaDir in subAreaDirectories)
                {
                    var modelFilePath = Path.Combine(subAreaDir, "model.inc.c");
                    if (File.Exists(modelFilePath))
                    {
                        areaModelFiles.Add(modelFilePath);
                    }
                }
                
                if (areaModelFiles.Count > 0)
                {
                    modelFiles[areaName] = areaModelFiles;
                    Console.WriteLine($"Found {areaModelFiles.Count} model file(s) for {areaName}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding model files: {ex.Message}");
        }

        return modelFiles;
    }

    /// <summary>
    /// Parses multiple model files and merges them into a single VisualMesh.
    /// </summary>
    public VisualMesh? ParseMultipleModelFiles(
        List<string> modelFilePaths, 
        string areaName, 
        string levelName,
        Dictionary<string, Matrix4>? transformations = null)
    {
        if (modelFilePaths == null || modelFilePaths.Count == 0)
            return null;

        var mergedMesh = new VisualMesh
        {
            AreaName = areaName,
            LevelName = levelName
        };

        int totalVertices = 0;
        int totalTriangles = 0;
        int subModelIndex = 0;

        foreach (var filePath in modelFilePaths)
        {
            var subMesh = ParseModelFile(filePath, areaName, levelName);
            if (subMesh != null)
            {
                // Extract sub-model number from path (e.g., "areas/1/3/model.inc.c" -> 3)
                var pathParts = filePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                int subModelNumber = subModelIndex + 1;
                for (int i = pathParts.Length - 1; i >= 0; i--)
                {
                    if (int.TryParse(pathParts[i], out int num))
                    {
                        subModelNumber = num;
                        break;
                    }
                }

                // Apply transformation if provided
                if (transformations != null && transformations.Count > 0)
                {
                    // Prioritize matching by display list name
                    bool transformed = false;
                    string? matchingDl = null;
                    foreach (var dlName in subMesh.DisplayListNames)
                    {
                        if (transformations.ContainsKey(dlName))
                        {
                            matchingDl = dlName;
                            break;
                        }
                    }

                    if (matchingDl != null)
                    {
                        Console.WriteLine($"Applying transformation to sub-model by matching DL: {matchingDl}");
                        ApplyTransform(subMesh.Vertices, transformations[matchingDl]);
                        transformed = true;
                    }

                    // Fall back to matching by sub-model number if not already transformed
                    if (!transformed)
                    {
                        var transformKey = $"SubModel_{subModelNumber}";
                        if (transformations.ContainsKey(transformKey))
                        {
                            Console.WriteLine($"Applying transformation to sub-model {subModelNumber} by index");
                            ApplyTransform(subMesh.Vertices, transformations[transformKey]);
                        }
                    }
                }

                // Create SubMesh entry
                var subMeshEntry = new SubMesh
                {
                    SourceFile = filePath,
                    SubModelNumber = subModelNumber,
                    Vertices = new List<ModelVertex>(subMesh.Vertices),  // Create new list
                    Triangles = new List<ModelTriangle>(subMesh.Triangles),  // Create new list
                    IsVisible = true
                };
                
                // Store vertices and triangles with adjusted indices
                int vertexOffset = mergedMesh.Vertices.Count;
                
                mergedMesh.Vertices.AddRange(subMesh.Vertices);
                
                // Adjust triangle indices to account for merged vertices
                foreach (var tri in subMesh.Triangles)
                {
                    var adjustedTri = new ModelTriangle(
                        tri.V1 + vertexOffset,
                        tri.V2 + vertexOffset,
                        tri.V3 + vertexOffset,
                        tri.TextureName
                    );
                    subMeshEntry.Triangles.Add(new ModelTriangle(tri.V1, tri.V2, tri.V3, tri.TextureName));
                    mergedMesh.Triangles.Add(adjustedTri);
                }
                
                mergedMesh.SubMeshes.Add(subMeshEntry);
                totalVertices += subMesh.VertexCount;
                totalTriangles += subMesh.TriangleCount;
                subModelIndex++;
            }
        }

        Console.WriteLine($"Merged {modelFilePaths.Count} model files: {totalVertices} vertices, {totalTriangles} triangles");
        Console.WriteLine($"Sub-models: {string.Join(", ", mergedMesh.SubMeshes.Select(sm => sm.SubModelNumber))}");
        return mergedMesh;
    }

    /// <summary>
    /// Applies a transformation matrix to a list of vertices.
    /// </summary>
    private void ApplyTransform(List<ModelVertex> vertices, Matrix4 transform)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            var pos = new Vector3(vertex.X, vertex.Y, vertex.Z);
            var transformed = Vector3.TransformPosition(pos, transform);
            
            // Update vertex with transformed position
            vertices[i] = new ModelVertex(
                (int)transformed.X,
                (int)transformed.Y,
                (int)transformed.Z,
                vertex.S,
                vertex.T,
                vertex.NX,
                vertex.NY,
                vertex.NZ,
                vertex.Alpha
            );
        }
        
        Console.WriteLine($"Applied transformation to {vertices.Count} vertices");
    }
}

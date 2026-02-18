namespace Sm64DecompLevelViewer.Models;

/// <summary>
/// Represents a vertex in a visual mesh with position, UV coordinates, and normal.
/// </summary>
public class ModelVertex
{
    // Position
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    // Texture coordinates (UV)
    public int S { get; set; }  // U coordinate
    public int T { get; set; }  // V coordinate

    // Normal vector (stored as signed bytes in SM64)
    public byte NX { get; set; }
    public byte NY { get; set; }
    public byte NZ { get; set; }
    public byte Alpha { get; set; }

    public ModelVertex(int x, int y, int z, int s, int t, byte nx, byte ny, byte nz, byte alpha)
    {
        X = x;
        Y = y;
        Z = z;
        S = s;
        T = t;
        NX = nx;
        NY = ny;
        NZ = nz;
        Alpha = alpha;
    }

    /// <summary>
    /// Converts the normal bytes to normalized float values (-1.0 to 1.0)
    /// </summary>
    public (float nx, float ny, float nz) GetNormalizedNormal()
    {
        // Convert unsigned byte to signed byte, then normalize
        sbyte nx = unchecked((sbyte)NX);
        sbyte ny = unchecked((sbyte)NY);
        sbyte nz = unchecked((sbyte)NZ);

        return (nx / 127.0f, ny / 127.0f, nz / 127.0f);
    }
}

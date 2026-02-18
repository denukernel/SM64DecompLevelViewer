namespace Sm64DecompLevelViewer.Models;

/// <summary>
/// Represents an object placed in a SM64 level.
/// </summary>
public class LevelObject
{
    public string ModelName { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int RX { get; set; }
    public int RY { get; set; }
    public int RZ { get; set; }
    public uint Params { get; set; }
    public string Behavior { get; set; } = string.Empty;
}

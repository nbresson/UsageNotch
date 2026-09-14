namespace UsageNotch.Core.Placement;

/// <summary>Rectangle en pixels physiques (coordonnées écran Win32).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public bool Contains(int px, int py) => px >= X && py >= Y && px < Right && py < Bottom;
}

using OwlTree;

public struct BoardCell : IEncodable
{
    public int x;
    public int y;

    public BoardCell()
    {
        x = 0;
        y = 0;
    }

    public BoardCell(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public int ByteLength()
    {
        return 8;
    }

    public void FromBytes(ReadOnlySpan<byte> bytes)
    {
        x = BitConverter.ToInt32(bytes);
        y = BitConverter.ToInt32(bytes.Slice(4));
    }

    public void InsertBytes(Span<byte> bytes)
    {
        BitConverter.TryWriteBytes(bytes, x);
        BitConverter.TryWriteBytes(bytes.Slice(4), y);
    }
}
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

    public override string ToString()
    {
        return $"{RowChar(x)}{y}";
    }

    private static char RowChar(int r)
    {
        switch(r)
        {
            case 0: return 'A';
            case 1: return 'B';
            case 2: return 'C';
            case 3: return 'D';
            case 4: return 'E';
            case 5: return 'F';
            case 6: return 'G';
            case 7: return 'H';
        }
        return '_';
    }

    private static int RowNum(char r)
    {
        switch(r)
        {
            case 'a': return 0;
            case 'b': return 1;
            case 'c': return 2;
            case 'd': return 3;
            case 'e': return 4;
            case 'f': return 5;
            case 'g': return 6;
            case 'h': return 7;
        }
        return -1;
    }

    public static bool FromString(string str, out BoardCell cell)
    {
        cell = new BoardCell();
        cell.x = RowNum(str[0]);
        if (cell.x == -1)
            return false;
        cell.y = int.Parse(str.Substring(1));
        if (cell.y < 0 || 8 <= cell.y)
            return false;
        return true;
    }
}
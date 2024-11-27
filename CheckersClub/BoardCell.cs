using OwlTree;

public struct BoardCell : IEncodable
{
    public int row;
    public int col;

    public BoardCell()
    {
        row = 0;
        col = 0;
    }

    public BoardCell(int x, int y)
    {
        this.row = x;
        this.col = y;
    }

    public int ByteLength()
    {
        return 8;
    }

    public void FromBytes(ReadOnlySpan<byte> bytes)
    {
        row = BitConverter.ToInt32(bytes);
        col = BitConverter.ToInt32(bytes.Slice(4));
    }

    public void InsertBytes(Span<byte> bytes)
    {
        BitConverter.TryWriteBytes(bytes, row);
        BitConverter.TryWriteBytes(bytes.Slice(4), col);
    }

    public override string ToString()
    {
        return $"{RowChar(row)}{col + 1}";
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
            case 'a': case 'A': return 0;
            case 'b': case 'B': return 1;
            case 'c': case 'C': return 2;
            case 'd': case 'D': return 3;
            case 'e': case 'E': return 4;
            case 'f': case 'F': return 5;
            case 'g': case 'G': return 6;
            case 'h': case 'H': return 7;
        }
        return -1;
    }

    public static bool FromString(string str, out BoardCell cell)
    {
        cell = new BoardCell();
        cell.row = RowNum(str[0]);
        if (cell.row == -1)
            return false;
        cell.col = int.Parse(str.Substring(1)) - 1;
        if (cell.col < 0 || 8 <= cell.col)
            return false;
        return true;
    }
}
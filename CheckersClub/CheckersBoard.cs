
using OwlTree;

public class CheckersBoard
{
    public enum CellState
    {
        Empty,
        Red,
        Black
    }

    public const int BoardSize = 8;

    private CellState[][] _board;

    public CheckersBoard()
    {
        _board = new CellState[BoardSize][];
        for (int i = 0; i < _board.Length; i++)
            _board[i] = new CellState[BoardSize];
    }

    public CellState GetCell(BoardCell cell)
    {
        return _board[cell.x][cell.y];
    }

    public void SetCell(BoardCell cell, CellState state)
    {
        _board[cell.x][cell.y] = state;
    }

    private ClientId _redPlayer;
    private ClientId _blackPlayer;

    private int _remainingRed;
    private int _remainingBlack;

    public int GetRemainingPieces(ClientId player)
    {
        if (player == _redPlayer)
            return _remainingRed;
        else if (player == _blackPlayer)
            return _remainingBlack;
        return -1;
    }

    private void CapturePieceOfColor(CellState color)
    {
        if (color == CellState.Black)
            _remainingBlack--;
        else if (color == CellState.Red)
            _remainingRed--;
    }

    public void ResetBoard(ClientId red, ClientId black)
    {
        _redPlayer = red;
        _blackPlayer = black;
        _remainingBlack = 12;
        _remainingRed = 12;
        
        for (int i = 0; i < _board!.Length; i++)
        {
            for (int j = 0; j < _board![i].Length; j++)
            {
                if (0 <= i && i <= 2)
                {
                    _board![i][j] = j % 2 == (1 - (i % 2)) ? CellState.Red : CellState.Empty;
                }
                else if (5 <= i && i <= 7)
                {
                    _board![i][j] = j % 2 == (0 + ((i - 1) % 2)) ? CellState.Black : CellState.Empty;
                }
                else
                {
                    _board![i][j] = CellState.Empty;
                }
            }
        }
    }

    public NetworkList<Capacity16, NetworkList<Capacity16, byte>> BoardToLists()
    {
        var matrix = new NetworkList<Capacity16, NetworkList<Capacity16, byte>>();
        for(int i = 0; i < _board!.Length; i++)
        {
            matrix.Add(new NetworkList<Capacity16, byte>());
            for (int j = 0; j < _board![i].Length; j++)
            {
                matrix[i].Add((byte)_board![i][j]);
            }
        }
        return matrix;
    }

    public void SetState(NetworkList<Capacity16, NetworkList<Capacity16, byte>> board)
    {
        for (int i = 0; i < board.Count; i++)
        {
            for (int j = 0; j < board[i].Count; i++)
            {
                _board![i][j] = (CellState)board[i][j];
            }
        }
    }

    public enum MoveResult
    {
        Invalid,
        Moved,
        Captured
    }

    public MoveResult MovePiece(BoardCell from, BoardCell to)
    {
        if (!ValidateMove(from, to)) return MoveResult.Invalid;
        
        var player = GetCell(from);
        SetCell(from, CellState.Empty);
        SetCell(to, player);

        if (GetCell(InBetween(from, to)) == GetOppositePlayer(player))
        {
            SetCell(InBetween(from, to), CellState.Empty);
            CapturePieceOfColor(GetOppositePlayer(player));
            return MoveResult.Captured;
        }

        return MoveResult.Moved;
    }

    private bool ValidateMove(BoardCell from, BoardCell to)
    {
        if (GetCell(from) == CellState.Empty) return false;
        if (GetCell(to) != CellState.Empty) return false;
        if (!IsOnBoard(to)) return false;

        if (GetCell(from) == CellState.Black && !IsValidBlackDir(from, to))
            return false;
        else if (GetCell(from) == CellState.Red && !IsValidRedDir(from, to))
            return false;

        if (Steps(from, to) > 1)
        {
            if (GetCell(InBetween(from, to)) != GetOppositePlayer(GetCell(from)))
                return false;
        }

        return true;
    }

    private bool IsOnBoard(BoardCell cell)
    {
        if (cell.x < 0 || BoardSize <= cell.x) return false;
        if (cell.y < 0 || BoardSize <= cell.y) return false;
        return true;
    }

    private bool IsValidBlackDir(BoardCell from, BoardCell to)
    {
        if (from.x == to.x) return false;
        if (from.y >= to.y) return false;
        return true;
    }

    private bool IsValidRedDir(BoardCell from, BoardCell to)
    {
        if (from.x == to.x) return false;
        if (from.y <= to.y) return false;
        return true;
    }

    private int Steps(BoardCell from, BoardCell to)
    {
        return Math.Abs(from.x - to.x);
    }

    private BoardCell InBetween(BoardCell from, BoardCell to)
    {
        var dir = Dir(from, to);
        return new BoardCell(from.x + dir.x, from.y + dir.y);
    }

    private BoardCell Dir(BoardCell from, BoardCell to)
    {
        return new BoardCell(to.x - from.x > 0 ? 1 : -1, to.y - from.y > 0 ? 1 : -1);
    }

    private CellState GetOppositePlayer(CellState player)
    {
        if (player == CellState.Red) return CellState.Black;
        if (player == CellState.Black) return CellState.Red;
        return CellState.Empty;
    }
}
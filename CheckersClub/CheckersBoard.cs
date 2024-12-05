
using OwlTree;

/// <summary>
/// Represents a game state.
/// </summary>
public class CheckersBoard
{
    // * Statics

    // what kind of piece is in a cell
    public enum CellState
    {
        Empty,
        Red,
        Black
    }

    // converts cell to char for displaying the board
    private static char CellChar(CellState cell)
    {
        switch (cell)
        {
            case CellState.Red: return 'R';
            case CellState.Black: return 'B';
            case CellState.Empty:
            default:
                return ' ';
        }
    }

    // dimensions
    public const int BoardSize = 8;

    // * State Management

    public CheckersBoard(int id)
    {
        Id = id;
        _board = new CellState[BoardSize][];
        for (int i = 0; i < _board.Length; i++)
            _board[i] = new CellState[BoardSize];
    }

    /// <summary>
    /// The unique id given to this board, used to communicate which board a command is for.
    /// </summary>
    public int Id { get; private set; }
    // displays 0th indices as 1, this is accounted for by BoardCell
    private CellState[][] _board;

    // get and set cell state, used as helpers
    private CellState GetCell(BoardCell cell) => _board[cell.row][cell.col];
    private void SetCell(BoardCell cell, CellState state) => _board[cell.row][cell.col] = state;

    // used to create board display
    public override string ToString()
    {
        var str = "Turn: " + Turn + "\n   1  2  3  4  5  6  7  8\n";
        for (int i = 0; i < _board.Length; i++)
        {
            str += BoardCell.RowChar(i) + " ";
            for (int j = 0; j < _board[i].Length; j++)
                str += "[" + CellChar(_board[i][j]) + "]";
            str += '\n';
        }
        return str;
    }

    // player client ids, set by Reset()
    public ClientId RedPlayer { get; private set; }
    public ClientId BlackPlayer { get; private set; }

    // tracking cur turn number, and whose turn it is
    public int Turn { get; private set; } = 0;
    public ClientId NextTurn { get { return Turn % 2 == 0 ? RedPlayer : BlackPlayer; } }
    public CellState NextTurnPiece { get { return NextTurn == RedPlayer ? CellState.Red : CellState.Black; } }

    /// <summary>
    /// Returns true if the given client id is either the red or black player.
    /// </summary>
    public bool IsAPlayer(ClientId client) => client == RedPlayer || client == BlackPlayer;

    // track remaining pieces players have to determine when the game is over
    private int _remainingRed;
    private int _remainingBlack;

    /// <summary>
    /// True if one of the players has lost all of their pieces.
    /// </summary>
    public bool IsGameOver { get { return GetRemainingPieces(RedPlayer) <= 0 || GetRemainingPieces(BlackPlayer) <= 0; } }

    /// <summary>
    /// The client id of the player who won. If there isn't a winner yet, returns ClientId.None.
    /// </summary>
    public ClientId Winner { get {
        if (GetRemainingPieces(RedPlayer) <= 0)
            return BlackPlayer;
        if (GetRemainingPieces(BlackPlayer) <= 0)
            return RedPlayer;
        return ClientId.None;
    } }

    /// <summary>
    /// Returns the remaining pieces for the given player. If the given player isn't in this board, returns -1.
    /// </summary>
    public int GetRemainingPieces(ClientId player)
    {
        if (player == RedPlayer)
            return _remainingRed;
        else if (player == BlackPlayer)
            return _remainingBlack;
        return -1;
    }

    // helper used when a move captures a piece
    private void CapturePieceOfColor(CellState color)
    {
        if (color == CellState.Black)
            _remainingBlack--;
        else if (color == CellState.Red)
            _remainingRed--;
    }

    /// <summary>
    /// Resets a board to the starting layout. Assigns two players to the board as players.
    /// </summary>
    public void ResetBoard(ClientId red, ClientId black)
    {
        RedPlayer = red;
        BlackPlayer = black;
        _remainingBlack = 12;
        _remainingRed = 12;
        
        for (int i = 0; i < _board!.Length; i++)
        {
            for (int j = 0; j < _board![i].Length; j++)
            {
                // produces checkered layout
                if (0 <= i && i <= 2)
                    _board![i][j] = j % 2 == (1 - (i % 2)) ? CellState.Red : CellState.Empty;
                else if (5 <= i && i <= 7)
                    _board![i][j] = j % 2 == (0 + ((i - 1) % 2)) ? CellState.Black : CellState.Empty;
                
                // everything else is empty
                else
                    _board![i][j] = CellState.Empty;
            }
        }
    }

    /// <summary>
    /// Convert board state to network list to be sent via RPC.
    /// </summary>
    public NetworkList<Capacity8, NetworkList<Capacity8, byte>> BoardToLists()
    {
        var matrix = new NetworkList<Capacity8, NetworkList<Capacity8, byte>>();
        for(int i = 0; i < _board!.Length; i++)
        {
            matrix.Add(new NetworkList<Capacity8, byte>());
            for (int j = 0; j < _board![i].Length; j++)
            {
                matrix[i].Add((byte)_board![i][j]);
            }
        }
        return matrix;
    }

    /// <summary>
    /// Enforce the given board state.
    /// </summary>
    public void SetState(NetworkList<Capacity8, NetworkList<Capacity8, byte>> board)
    {
        for (int i = 0; i < board.Count; i++)
        {
            for (int j = 0; j < board[i].Count; i++)
            {
                _board![i][j] = (CellState)board[i][j];
            }
        }
    }

    // * Making Moves

    // used to signal the result of a move
    public enum MoveResult
    {
        Invalid, // cannot make move
        Moved, // piece successfully moved
        Captured // piece successfully moved, and an opponent piece was captured
    }

    /// <summary>
    /// Attempts to move the piece at cell <c>from</c>, to cell <c>to</c>.
    /// </summary>
    public MoveResult MovePiece(BoardCell from, BoardCell to)
    {
        if (!ValidateMove(from, to)) return MoveResult.Invalid;
        
        // move the piece
        var player = GetCell(from);
        SetCell(from, CellState.Empty);
        SetCell(to, player);
        Turn++;

        // capture if possible
        if (GetCell(InBetween(from, to)) == GetOppositePlayer(player))
        {
            SetCell(InBetween(from, to), CellState.Empty);
            CapturePieceOfColor(GetOppositePlayer(player));
            return MoveResult.Captured;
        }

        return MoveResult.Moved;
    }

    /// <summary>
    /// Returns true if the move from cell <c>from</c>, to cell <c>to</c> is valid.
    /// </summary>
    public bool ValidateMove(BoardCell from, BoardCell to)
    {
        if (GetCell(from) == CellState.Empty) return false; // cannot move empty space
        if (GetCell(to) != CellState.Empty) return false; // cannot move on top of another piece
        if (!IsOnBoard(to)) return false; // cannot move off the board

        if (GetCell(from) != NextTurnPiece) return false; // cannot move if not your turn

        // cannot move in the wrong direction
        if (GetCell(from) == CellState.Black && !IsValidBlackDir(from, to))
            return false;
        else if (GetCell(from) == CellState.Red && !IsValidRedDir(from, to))
            return false;

        // cannot move 2 cells without an opposing piece to capture
        if (Steps(from, to) > 1 && GetCell(InBetween(from, to)) != GetOppositePlayer(GetCell(from)))
                return false;

        return true;
    }

    private bool IsOnBoard(BoardCell cell) => 0 <= cell.row && cell.row < BoardSize && 0 <= cell.col && cell.col < BoardSize;

    private bool IsValidBlackDir(BoardCell from, BoardCell to) => from.row > to.row;
    private bool IsValidRedDir(BoardCell from, BoardCell to) => from.row < to.row;

    private int Steps(BoardCell from, BoardCell to) => Math.Abs(from.row - to.row) + (Math.Abs(from.col - to.col) > 1 ? 1 : 0);

    private BoardCell InBetween(BoardCell from, BoardCell to)
    {
        var dir = Dir(from, to);
        return new BoardCell(from.row + dir.row, from.col + dir.col);
    }

    private BoardCell Dir(BoardCell from, BoardCell to) => new BoardCell(to.row - from.row > 0 ? 1 : -1, to.col - from.col > 0 ? 1 : -1);

    private CellState GetOppositePlayer(CellState player)
    {
        if (player == CellState.Red) return CellState.Black;
        if (player == CellState.Black) return CellState.Red;
        return CellState.Empty;
    }
}
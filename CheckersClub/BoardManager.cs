using OwlTree;

public class BoardManager : NetworkObject
{
    public static BoardManager? Instance { get; private set; } = null;

    public override void OnSpawn()
    {
        Instance = this;
        Console.WriteLine("board manager init...");
    }

    private Dictionary<int, CheckersBoard> _boards = new();

    [Rpc(RpcCaller.Server, InvokeOnCaller = true)]
    public virtual void AddNewBoard([RpcCallee] ClientId callee, int id)
    {
        var board = new CheckersBoard(id);
        _boards.Add(id, board);
    }

    [Rpc(RpcCaller.Server, InvokeOnCaller = true)]
    public virtual void RemoveBoard([RpcCallee] ClientId callee, int id)
    {
        if (_boards.ContainsKey(id))
            _boards.Remove(id);
    }

    [Rpc(RpcCaller.Server)]
    public virtual void EnforceBoardState([RpcCallee] ClientId callee, int id, NetworkList<Capacity8, NetworkList<Capacity8, byte>> state)
    {
        if (_boards.TryGetValue(id, out var board))
            board.SetState(state);
    }

    [Rpc(RpcCaller.Server, InvokeOnCaller = true)]
    public virtual void EnforceMove([RpcCallee] ClientId callee, int boardId, BoardCell from, BoardCell to)
    {
        if (_boards.TryGetValue(boardId, out var board))
        {
            var result = board.MovePiece(from, to);
        }
    }

    [Rpc(RpcCaller.Client, InvokeOnCaller = true)]
    public virtual void MakeMove(int boardId, BoardCell from, BoardCell to, [RpcCaller] ClientId caller)
    {
        if (!_boards.TryGetValue(boardId, out var board))
            return;

        if (Connection.NetRole == Connection.Role.Server)
        {
            if (!board.IsAPlayer(caller))
                return;

            if (!board.ValidateMove(from, to))
            {
                EnforceBoardState(caller, boardId, board.BoardToLists());
                return;
            }
            
            EnforceMove(board.RedPlayer, boardId, from, to);
            EnforceMove(board.BlackPlayer, boardId, from, to);
        }
        else
        {
            var result = board.MovePiece(from, to);
        }
    }
}
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

    public bool IsPlaying(ClientId id)
    {
        return _boards.Any(p => p.Value.IsAPlayer(id));
    }

    private List<(ClientId challenger, ClientId challenged)> _challenges = new();

    public bool IsChallenging { get; private set; } = false;

    [Rpc(RpcCaller.Client, InvokeOnCaller = true)]
    public virtual void ChallengePlayer(ClientId player, [RpcCaller] ClientId caller = default)
    {
        if (Connection.NetRole == Connection.Role.Server)
        {
            if (!Connection.ContainsClient(player) || IsPlaying(player) || IsPlaying(caller))
            {
                RejectChallenge(caller, player);
                return;
            }
            _challenges.Add((caller, player));
            SendChallenge(player, caller);
        }
        else
        {
            IsChallenging = true;
        }
    }

    public Action<ClientId>? OnChallengeRejected;
    [Rpc(RpcCaller.Server)]
    public virtual void RejectChallenge([RpcCallee] ClientId callee, ClientId player)
    {
        IsChallenging = false;
        OnChallengeRejected?.Invoke(player);
    }

    public Action<ClientId>? OnChallengeReceived;
    [Rpc(RpcCaller.Server)]
    public virtual void SendChallenge([RpcCallee] ClientId callee, ClientId challenger)
    {
        OnChallengeReceived?.Invoke(challenger);
    }

    [Rpc(RpcCaller.Client)]
    public virtual void AcceptChallenge(ClientId challenger, [RpcCaller] ClientId caller = default)
    {
        if (!_challenges.Contains((challenger, caller)))
        {
            return;
        }
        NewGame(challenger, caller);
    }

    private int curBoardId = 0;

    public void NewGame(ClientId redPlayer, ClientId blackPlayer)
    {
        if (Connection.NetRole == Connection.Role.Server)
        {
            AddNewBoard(redPlayer, curBoardId, blackPlayer, true);
            AddNewBoard(blackPlayer, curBoardId, redPlayer, false);
            curBoardId++;
        }
    }

    public Action<ClientId, ClientId>? OnGameStart;

    [Rpc(RpcCaller.Server, InvokeOnCaller = true)]
    public virtual void AddNewBoard([RpcCallee] ClientId callee, int id, ClientId otherPlayer, bool isRed)
    {
        if (_boards.ContainsKey(id)) return;

        var board = new CheckersBoard(id);
        _boards.Add(id, board);
        if (isRed)
            board.ResetBoard(callee, otherPlayer);
        else
            board.ResetBoard(otherPlayer, callee);
        if (Connection.NetRole == Connection.Role.Client)
            OnGameStart?.Invoke(callee, otherPlayer);
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

    [Rpc(RpcCaller.Server)]
    public virtual void EnforceMove([RpcCallee] ClientId callee, int boardId, BoardCell from, BoardCell to)
    {
        if (_boards.TryGetValue(boardId, out var board))
        {
            var result = board.MovePiece(from, to);
        }
    }

    [Rpc(RpcCaller.Client, InvokeOnCaller = true)]
    public virtual void MakeMove(int boardId, BoardCell from, BoardCell to, [RpcCaller] ClientId caller = default)
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
            
            var result = board.MovePiece(from, to);
            EnforceMove(board.RedPlayer, boardId, from, to);
            EnforceMove(board.BlackPlayer, boardId, from, to);

            if (board.IsGameOver)
            {
                DeclareWinner(board.RedPlayer, board.Winner);
                DeclareWinner(board.BlackPlayer, board.Winner);
                RemoveBoard(board.RedPlayer, boardId);
                RemoveBoard(board.BlackPlayer, boardId);
            }
        }
        else
        {
            var result = board.MovePiece(from, to);
        }
    }

    public Action<CheckersBoard>? OnRequestMove;

    [Rpc(RpcCaller.Server)]
    public virtual void RequestMove([RpcCallee] ClientId callee, int boardId)
    {
        if (!_boards.TryGetValue(boardId, out var board))
            return;
        OnRequestMove?.Invoke(board);
    }

    public Action<ClientId>? OnGameOver;

    [Rpc(RpcCaller.Server)]
    public virtual void DeclareWinner([RpcCallee] ClientId callee, ClientId winner)
    {
        OnGameOver?.Invoke(winner);
    }
}
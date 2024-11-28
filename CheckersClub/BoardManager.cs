using OwlTree;

public class BoardManager : NetworkObject
{
    public static BoardManager? Instance { get; private set; } = null;

    public override void OnSpawn()
    {
        Instance = this;
        Connection.OnClientDisconnected += RemovePlayer;
        Console.WriteLine("board manager init...");
    }

    private Dictionary<int, CheckersBoard> _boards = new();

    public bool IsPlaying(ClientId id)
    {
        return _boards.Any(p => p.Value.IsAPlayer(id));
    }

    public void RemovePlayer(ClientId id)
    {
        if (Connection.IsClient) return;

        for (int i = 0; i < _challenges.Count; i++)
        {
            if (_challenges[i].challenged == id || _challenges[i].challenger == id)
            {
                _challenges.RemoveAt(i);
                i--;
            }
        }

        if (IsPlaying(id))
        {
            var board = _boards.Where(p => p.Value.IsAPlayer(id)).FirstOrDefault().Value;
            var player = board.RedPlayer == id ? board.BlackPlayer : board.RedPlayer;
            OpponentDisconnected(player, board.Id);
        }
    }

    public Action<ClientId>? OnOpponentDisconnected;
    [Rpc(RpcCaller.Server)]
    public virtual void OpponentDisconnected([RpcCallee] ClientId callee, int id)
    {
        if (_boards.TryGetValue(id, out var board))
        {
            var other = board.RedPlayer == callee ? board.BlackPlayer : board.RedPlayer;
            OnOpponentDisconnected?.Invoke(other);
            _boards.Remove(id);
        }
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

    public Action? OnChallengeFailed;
    [Rpc(RpcCaller.Server)]
    public virtual void ChallengeFailed([RpcCallee] ClientId callee)
    {
        OnChallengeFailed?.Invoke();
    }

    public Action<ClientId>? OnChallengeReceived;
    [Rpc(RpcCaller.Server)]
    public virtual void SendChallenge([RpcCallee] ClientId callee, ClientId challenger)
    {
        OnChallengeReceived?.Invoke(challenger);
    }

    [Rpc(RpcCaller.Client)]
    public virtual void ChallengeResponse(ClientId challenger, bool response, [RpcCaller] ClientId caller = default)
    {
        if (!_challenges.Contains((challenger, caller)))
        {
            ChallengeFailed(caller);
        }
        else if (!response)
        {
            RejectChallenge(challenger, caller);
        }
        else
        {
            NewGame(challenger, caller);
        }
    }

    private int curBoardId = 0;

    public void NewGame(ClientId redPlayer, ClientId blackPlayer)
    {
        if (Connection.NetRole == Connection.Role.Server)
        {
            var board = new CheckersBoard(curBoardId);
            board.ResetBoard(redPlayer, blackPlayer);
            _boards.Add(board.Id, board);
            curBoardId++;

            AddNewBoard(redPlayer, board.Id, blackPlayer, true);
            AddNewBoard(blackPlayer, board.Id, redPlayer, false);

            RequestMove(board.NextTurn, board.Id);
        }
    }

    public Action<ClientId, ClientId, CheckersBoard>? OnGameStart;

    [Rpc(RpcCaller.Server, InvokeOnCaller = false)]
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
            OnGameStart?.Invoke(callee, otherPlayer, board);
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
    
    public Action<CheckersBoard, ClientId, BoardCell, BoardCell>? OnMoveReceived;
    [Rpc(RpcCaller.Server)]
    public virtual void EnforceMove([RpcCallee] ClientId callee, int boardId, ClientId player, BoardCell from, BoardCell to)
    {
        if (_boards.TryGetValue(boardId, out var board))
        {
            var result = board.MovePiece(from, to);
            OnMoveReceived?.Invoke(board, player, from, to);
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
            EnforceMove(board.RedPlayer, boardId, caller, from, to);
            EnforceMove(board.BlackPlayer, boardId, caller, from, to);

            if (board.IsGameOver)
            {
                DeclareWinner(board.RedPlayer, board.Winner);
                DeclareWinner(board.BlackPlayer, board.Winner);
                RemoveBoard(board.RedPlayer, boardId);
                RemoveBoard(board.BlackPlayer, boardId);
            }
            else
            {
                RequestMove(board.NextTurn, board.Id);
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
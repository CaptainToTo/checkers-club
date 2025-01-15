using OwlTree;

/// <summary>
/// Singleton manages game states across clients
/// </summary>
public class BoardManager : NetworkObject
{
    public static BoardManager? Instance { get; private set; } = null;

    public override void OnSpawn()
    {
        Instance = this;
        Connection.OnClientDisconnected += RemovePlayer;
        Console.WriteLine("board manager init...");
    }

    // manages all active boards, clients will only have the board they are assigned to for their game
    private Dictionary<int, CheckersBoard> _boards = new();

    /// <summary>
    /// True if the given client id is currently in a game.
    /// </summary>
    public bool IsPlaying(ClientId id) => _boards.Any(p => p.Value.IsAPlayer(id));

    /// <summary>
    /// Cancels any challenges the player has made, and ends the game they are if they are in one.
    /// </summary>
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
            _boards.Remove(board.Id);
        }
    }

    // send to players if their opponent disconnected mid-game
    public Action<ClientId>? OnOpponentDisconnected;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void OpponentDisconnected([CalleeId] ClientId callee, int id)
    {
        if (_boards.TryGetValue(id, out var board))
        {
            var other = board.RedPlayer == callee ? board.BlackPlayer : board.RedPlayer;
            OnOpponentDisconnected?.Invoke(other);
            _boards.Remove(id);
        }
    }

    // * Challenges

    // only used by server to track which players are currently awaiting challenge responses
    private List<(ClientId challenger, ClientId challenged)> _challenges = new();

    // only used by clients to track whether they are waiting for a challenge response
    public bool IsChallenging { get; private set; } = false;

    // send a challenge request to the server
    [Rpc(RpcPerms.ClientsToAuthority, InvokeOnCaller = true)]
    public virtual void ChallengePlayer(ClientId player, [CallerId] ClientId caller = default)
    {
        // validate request on server
        if (Connection.IsServer)
        {
            if (!Connection.ContainsClient(player) || IsPlaying(player) || IsPlaying(caller))
            {
                RejectChallenge(caller, player);
                return;
            }
            _challenges.Add((caller, player));
            SendChallenge(player, caller);
        }
        // set challenging state on client
        else
        {
            IsChallenging = true;
        }
    }

    // notify a player their challenge was rejected
    public Action<ClientId>? OnChallengeRejected;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void RejectChallenge([CalleeId] ClientId callee, ClientId player)
    {
        IsChallenging = false;
        OnChallengeRejected?.Invoke(player);
    }

    // send a challenge to the player being challenged
    public Action<ClientId>? OnChallengeReceived;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void SendChallenge([CalleeId] ClientId callee, ClientId challenger)
    {
        OnChallengeReceived?.Invoke(challenger);
    }

    // player being challenged notifies the server of their response, true if challenge accepted
    [Rpc(RpcPerms.ClientsToAuthority)]
    public virtual void ChallengeResponse(ClientId challenger, bool response, [CallerId] ClientId caller = default)
    {
        if (!_challenges.Contains((challenger, caller)))
            ChallengeFailed(caller);
        else if (response)
            NewGame(challenger, caller);
        else
            RejectChallenge(challenger, caller);
    }

    // notify a player their challenge response failed
    public Action? OnChallengeFailed;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void ChallengeFailed([CalleeId] ClientId callee)
    {
        OnChallengeFailed?.Invoke();
    }

    // * Game State Management

    // used by server to track current board id, each new board gets a unique id
    private int curBoardId = 0;

    /// <summary>
    /// Starts a new game of checkers between 2 players.
    /// </summary>
    public void NewGame(ClientId redPlayer, ClientId blackPlayer)
    {
        if (Connection.IsServer)
        {
            // create a new board
            var board = new CheckersBoard(curBoardId);
            board.ResetBoard(redPlayer, blackPlayer);
            _boards.Add(board.Id, board);
            curBoardId++;

            // send the new board to only the involved players
            AddNewBoard(redPlayer, board.Id, blackPlayer, true);
            AddNewBoard(blackPlayer, board.Id, redPlayer, false);

            // start the game, red goes first
            RequestMove(board.NextTurn, board.Id);
        }
    }

    // start the game on clients
    public Action<ClientId, ClientId, CheckersBoard>? OnGameStart;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void AddNewBoard([CalleeId] ClientId callee, int id, ClientId otherPlayer, bool isRed)
    {
        if (_boards.ContainsKey(id)) return;

        // create a board matching the server
        var board = new CheckersBoard(id);
        _boards.Add(id, board);

        // if the local player (callee) is the red player, callee is the first arg
        if (isRed)
            board.ResetBoard(callee, otherPlayer);
        else
            board.ResetBoard(otherPlayer, callee);

        OnGameStart?.Invoke(callee, otherPlayer, board);
    }

    // server requests the next move from the next player's turn
    public Action<CheckersBoard>? OnRequestMove;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void RequestMove([CalleeId] ClientId callee, int boardId)
    {
        if (!_boards.TryGetValue(boardId, out var board))
            return;
        OnRequestMove?.Invoke(board);
    }

    // client sends their selected move
    [Rpc(RpcPerms.ClientsToAuthority)]
    public virtual void MakeMove(int boardId, BoardCell from, BoardCell to, [CallerId] ClientId caller = default)
    {
        if (!_boards.TryGetValue(boardId, out var board))
            return;

        // players can only make moves on the board they're a player of
        if (!board.IsAPlayer(caller))
            return;

        // validate the move, if the move isn't valid, enforce the board state on the player
        if (!board.ValidateMove(from, to))
        {
            EnforceBoardState(caller, boardId, board.BoardToLists());
            return;
        }
        
        // move piece and send the move to both players
        var result = board.MovePiece(from, to);
        EnforceMove(board.RedPlayer, boardId, caller, from, to);
        EnforceMove(board.BlackPlayer, boardId, caller, from, to);

        // if a player has won, declare game over
        if (board.IsGameOver)
        {
            DeclareWinner(board.RedPlayer, board.Winner);
            DeclareWinner(board.BlackPlayer, board.Winner);
            RemoveBoard(board.RedPlayer, boardId);
            RemoveBoard(board.BlackPlayer, boardId);
        }
        // otherwise, get the next move from the other player
        else
        {
            RequestMove(board.NextTurn, board.Id);
        }
    }

    // server sends up-to-date game state to fix clients who might have de-synced
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void EnforceBoardState([CalleeId] ClientId callee, int id, NetworkList<Capacity8, NetworkList<Capacity8, byte>> state)
    {
        if (_boards.TryGetValue(id, out var board))
            board.SetState(state);
    }
    
    // server sends new move to players after validating
    public Action<CheckersBoard, ClientId, BoardCell, BoardCell>? OnMoveReceived;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void EnforceMove([CalleeId] ClientId callee, int boardId, ClientId player, BoardCell from, BoardCell to)
    {
        if (_boards.TryGetValue(boardId, out var board))
        {
            var result = board.MovePiece(from, to);
            OnMoveReceived?.Invoke(board, player, from, to);
        }
    }

    // remove the board for the client at the end of the game
    [Rpc(RpcPerms.AuthorityToClients, InvokeOnCaller = true)]
    public virtual void RemoveBoard([CalleeId] ClientId callee, int id)
    {
        if (_boards.ContainsKey(id))
            _boards.Remove(id);
    }

    // server sends the winner to each player
    public Action<ClientId>? OnGameOver;
    [Rpc(RpcPerms.AuthorityToClients)]
    public virtual void DeclareWinner([CalleeId] ClientId callee, ClientId winner)
    {
        OnGameOver?.Invoke(winner);
    }
}
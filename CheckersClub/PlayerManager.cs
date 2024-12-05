using OwlTree;

/// <summary>
/// Singleton that manages player usernames.
/// </summary>
public class PlayerManager : NetworkObject
{
    public static PlayerManager? Instance { get; private set; } = null;

    public override void OnSpawn()
    {
        Instance = this;
        Connection.OnClientDisconnected += RemovePlayer;
        if (Connection.NetRole == Connection.Role.Server)
            Connection.OnClientConnected += (id) => SendPlayers(id, _players);
        Console.WriteLine("player manager init...");
    }

    public NetworkDict<Capacity16, ClientId, NetworkString<Capacity32>> _players = new();

    /// <summary>
    /// Removes any usernames for players no-longer connected.
    /// </summary>
    public void CheckPlayers()
    {
        foreach (var id in _players.Keys.ToArray())
        {
            if (!Connection.ContainsClient(id))
                RemovePlayer(id);
        }
    }

    /// <summary>
    /// Get an iterable of the current player manager state.
    /// </summary>
    public IEnumerable<(ClientId id, string name)> GetNames() => _players.Select(p => (p.Key, (string)p.Value));

    public const string DefaultPlayerName = "NewPlayer";

    /// <summary>
    /// Returns the username assigned to the given client.
    /// If the client id doesn't exist, returns the default player name.
    /// </summary>
    public string GetName(ClientId id) 
    {
        if (_players.TryGetValue(id, out var name))
            return name;
        return DefaultPlayerName;
    }

    /// <summary>
    /// Gets the local client username.
    /// </summary>
    public string GetLocalName() => GetName(Connection.LocalId);

    /// <summary>
    /// Finds the client id associated with the given username.
    /// </summary>
    public ClientId GetId(string name) => _players.Where(p => p.Value == name).FirstOrDefault().Key;

    /// <summary>
    /// True if the given username exists.
    /// </summary>
    public bool HasName(string name) => _players.ContainsValue(name);

    /// <summary>
    /// Remove the given player's username.
    /// </summary>
    private void RemovePlayer(ClientId id)
    {
        if (_players.ContainsKey(id))
            _players.Remove(id);
    }

    // send all usernames to a specific client
    [Rpc(RpcCaller.Server)]
    public virtual void SendPlayers([RpcCallee] ClientId callee, NetworkDict<Capacity16, ClientId, NetworkString<Capacity32>> players)
    {
        _players = players;
    }

    // submit username to server
    [Rpc(RpcCaller.Client, InvokeOnCaller = false)]
    public virtual void SendUsername(NetworkString<Capacity32> name, [RpcCaller] ClientId caller = default)
    {
        Console.WriteLine("attempted player name assignment: " + caller + " " + name);
        if (_players.ContainsValue(name))
            DenyUsername(caller); // tell player their username was rejected
        else
            BroadcastUsername(caller, name); // send the new username to all players
    }

    public Action? OnNameDenied;
    public Action<string>? OnNameAssigned;

    [Rpc(RpcCaller.Server)]
    public virtual void DenyUsername([RpcCallee] ClientId callee)
    {
        OnNameDenied?.Invoke();
    }

    [Rpc(RpcCaller.Server, InvokeOnCaller = true)]
    public virtual void BroadcastUsername(ClientId player, NetworkString<Capacity32> name)
    {
        if (Connection.NetRole == Connection.Role.Server)
            Console.WriteLine("assigned player name: " + player + " " + name);
        _players[player] = name;
        if (player == Connection.LocalId)
            OnNameAssigned?.Invoke(name);
    }

}
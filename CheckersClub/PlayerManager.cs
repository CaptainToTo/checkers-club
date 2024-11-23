using OwlTree;

public class PlayerManager : NetworkObject
{
    public static PlayerManager? Instance { get; private set; } = null;

    public override void OnSpawn()
    {
        Instance = this;
        Connection.OnClientDisconnected += RemovePlayer;
        Console.WriteLine("player manager init...");
    }

    private NetworkDict<Capacity16, ClientId, NetworkString<Capacity32>> _players = new();

    public IEnumerable<(ClientId id, string name)> GetNames()
    {
        return _players.Select(p => (p.Key, (string)p.Value));
    }

    public string GetName(ClientId id) 
    {
        if (_players.TryGetValue(id, out var name))
            return name;
        return DefaultPlayerName;
    }

    public string GetLocalName() => GetName(Connection.LocalId);

    public ClientId GetId(string name) => _players.Where(p => p.Value == name).FirstOrDefault().Key;

    public bool HasName(string name) => _players.ContainsValue(name);

    public const string DefaultPlayerName = "NewPlayer";

    private void RemovePlayer(ClientId id)
    {
        if (_players.ContainsKey(id))
            _players.Remove(id);
    }

    [Rpc(RpcCaller.Server)]
    public virtual void SendPlayers([RpcCallee] ClientId callee, NetworkDict<Capacity16, ClientId, NetworkString<Capacity32>> players)
    {
        _players = players;
    }

    [Rpc(RpcCaller.Client, InvokeOnCaller = false)]
    public virtual void SendUsername(NetworkString<Capacity32> name, [RpcCaller] ClientId caller = default)
    {
        Console.WriteLine("attempted player name assignment: " + caller + " " + name);
        if (_players.ContainsValue(name))
            DenyUsername(caller);
        else
            BroadcastUsername(caller, name);
    }

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

    public Action<string>? OnNameAssigned;
    public Action? OnNameDenied;
}
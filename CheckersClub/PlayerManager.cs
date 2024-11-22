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

    public string GetName(ClientId id) 
    {
        if (_players.TryGetValue(id, out var name))
            return name;
        return DefaultPlayerName;
    }

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
        Console.WriteLine("player name: " + caller + " " + name);
        BroadcastUsername(caller, name);
    }

    [Rpc(RpcCaller.Server, InvokeOnCaller = true)]
    public virtual void BroadcastUsername(ClientId player, NetworkString<Capacity32> name)
    {
        _players[player] = name;
    }
}
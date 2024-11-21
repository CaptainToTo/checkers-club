using OwlTree;

public class PlayerManager : NetworkObject
{
    public static PlayerManager? Instance { get; private set; } = null;

    public override void OnSpawn()
    {
        Instance = this;
        Connection.OnClientConnected += AddPlayer;
    }

    private void AddPlayer(ClientId id)
    {
        
    }

    private NetworkDict<Capacity16, ClientId, NetworkString<Capacity32>> _players = new();

    public const string DefaultPlayerName = "NewPlayer";
}
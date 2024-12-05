using OwlTree;

public class Program
{
    // clients must have a matching app id to the server
    public const string AppId = "CheckersClub_OwlTreeExample";

    // id used to differentiate the log files produced by different instances running on the same machine
    public static long logId = 0;

    // expects either "client" or "server" to be provided in the 
    static void Main(string[] args)
    {
        logId = new Random(DateTime.UtcNow.Millisecond).NextInt64();
        Console.WriteLine("log id is " + logId.ToString());

        string addr = args.Length >= 2 ? args[1] : "127.0.0.1";

        if (args[0] == "server")
        {
            File.WriteAllText($"OwlTreeServer{logId}.log", "");
            var server = new Connection(new Connection.Args
            {
                appId = AppId,
                role = Connection.Role.Server,
                serverAddr = addr,
                maxClients = 16,    // 16 clients per server
                threaded = false,   // server is single threaded since it doesn't need to run update logic
                printer = ServerLog,
                verbosity = Logger.Includes().All()
            });

            // add some simple logs
            server.OnClientConnected += (id) => Console.WriteLine(id + " joined");
            server.OnClientDisconnected += (id) => Console.WriteLine(id + " left");

            // init managers, these will be spawned immediately for any clients that connect
            server.Spawn<BoardManager>();
            server.Spawn<PlayerManager>();
            ServerUpdateLoop(server);
        }
        else if (args[0] == "client")
        {
            File.WriteAllText($"OwlTreeClient{logId}.log", "");
            var client = new Connection(new Connection.Args
            {
                appId = AppId,
                role = Connection.Role.Client,
                serverAddr = addr,
                threadUpdateDelta = 500,    // clients are multithreaded, they will recv&send every half sec
                printer = ClientLog,
                verbosity = Logger.Includes().All()
            });
            client.OnReady += (_) => Console.WriteLine("connected!");

            var ui = new UI();  // ui class used to display cli interface
            ClientUpdateLoop(client, ui);
        }


        // testing interface for the checkers board
        else if (args[0] == "test")
        {
            Tester.RunTests();
            return;
        }
        else
        {
            Console.WriteLine("Must specify whether to start a server or client connection, exiting...");
            return;
        }
        return;
    }


    // server is request driven, continuously handle RPCs sent by clients, and send their responses
    static void ServerUpdateLoop(Connection connection)
    {
        while (connection.IsActive)
        {
            connection.Read();
            connection.ExecuteQueue();
            connection.Send();
            Thread.Sleep(100);
        }
    }
    static void ServerLog(string text) => File.AppendAllText($"OwlTreeServer{logId}.log", text);


    // cur state is used to select different update logic, all states are in the ClientStates class
    public static Action<Connection, UI> curState = ClientStates.WaitingForConnection;
    static void ClientUpdateLoop(Connection connection, UI ui)
    {
        Console.WriteLine("waiting for server...");
        while (connection.IsActive)
        {
            connection.ExecuteQueue();
            curState.Invoke(connection, ui);
            Thread.Sleep(200);
        }
        // disconnecting will require a new Connection to be made,
        // end the program to make that easier
        Console.WriteLine("disconnected...");
        Environment.Exit(0);
    }
    static void ClientLog(string text) => File.AppendAllText($"OwlTreeClient{logId}.log", text);
}
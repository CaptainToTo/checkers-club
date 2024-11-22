using OwlTree;

public class Program
{
    static void Main(string[] args)
    {
        Connection? connection = null;

        if (args[0] == "server")
        {
            connection = new Connection(new Connection.Args
            {
                appId = "CheckersClub_OwlTreeExample",
                role = Connection.Role.Server,
                serverAddr = "127.0.0.1",
                maxClients = 16,
                threadUpdateDelta = 500,
                printer = ServerLog,
                verbosity = Logger.Includes().All()
            });
            // init managers
            connection.Spawn<BoardManager>();
            connection.Spawn<PlayerManager>();
        }
        else if (args[0] == "client")
        {
            connection = new Connection(new Connection.Args
            {
                appId = "CheckersClub_OwlTreeExample",
                role = Connection.Role.Client,
                serverAddr = "127.0.0.1",
                threadUpdateDelta = 500,
                printer = ClientLog,
                verbosity = Logger.Includes().All()
            });
        }
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

        UpdateLoop(connection);
    }

    static void UpdateLoop(Connection connection)
    {
        while (connection.IsActive)
        {
            connection.ExecuteQueue();
            Thread.Sleep(500);
        }
    }
    
    static void ServerLog(string text) => File.AppendAllText("OwlTreeServer.log", text);
    static void ClientLog(string text) => File.AppendAllText("OwlTreeClient.log", text);
}
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
                threadUpdateDelta = 500,
                printer = ServerLog,
                verbosity = Logger.Includes().All()
            });
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
        else
        {
            Console.WriteLine("Must specify whether to start a server or client connection, exiting...");
            return;
        }

        UpdateLoop(connection);
    }

    static void ServerLog(string text)
    {
        File.AppendAllText("OwlTreeServer.out", text);
    }

    static void ClientLog(string text)
    {
        File.AppendAllText("OwlTreeClient.out", text);
    }

    static void UpdateLoop(Connection connection)
    {
        while (connection.IsActive)
        {
            connection.ExecuteQueue();
            Thread.Sleep(100);
        }
    }
}
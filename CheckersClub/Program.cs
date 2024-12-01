﻿using OwlTree;

public class Program
{
    static void Main(string[] args)
    {
        if (args[0] == "server")
        {
            File.WriteAllText("OwlTreeServer.log", "");
            var server = new Connection(new Connection.Args
            {
                appId = "CheckersClub_OwlTreeExample",
                role = Connection.Role.Server,
                serverAddr = "127.0.0.1",
                maxClients = 16,
                threaded = false,
                printer = ServerLog,
                verbosity = Logger.Includes().All()
            });

            server.OnClientConnected += (id) => Console.WriteLine(id + " joined");
            server.OnClientDisconnected += (id) => Console.WriteLine(id + " left");

            // init managers
            server.Spawn<BoardManager>();
            server.Spawn<PlayerManager>();
            ServerUpdateLoop(server);
        }
        else if (args[0] == "client")
        {
            // File.WriteAllText("OwlTreeClient.log", "");
            var client = new Connection(new Connection.Args
            {
                appId = "CheckersClub_OwlTreeExample",
                role = Connection.Role.Client,
                serverAddr = "127.0.0.1",
                threadUpdateDelta = 500,
                // printer = ClientLog,
                // verbosity = Logger.Includes().All()
            });
            client.OnReady += (_) => Console.WriteLine("connected!");
            var ui = new UI();
            ClientUpdateLoop(client, ui);
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
        return;
    }

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
    static void ServerLog(string text) => File.AppendAllText("OwlTreeServer.log", text);

    public static Action<Connection, UI> curState = ClientStates.WaitingForConnection;

    static void ClientUpdateLoop(Connection connection, UI ui)
    {
        Console.WriteLine("waiting for server...");
        while (connection.IsActive)
        {
            connection.ExecuteQueue();
            curState.Invoke(connection, ui);
            connection.ExecuteQueue();
            Thread.Sleep(200);
        }
        Console.WriteLine("disconnected...");
        Environment.Exit(0);
    }
    static void ClientLog(string text) => File.AppendAllText("OwlTreeClient.log", text);
}
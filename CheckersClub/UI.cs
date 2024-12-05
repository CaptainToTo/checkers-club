
using OwlTree;

// all client cli handled here

public enum Command
{
    Quit,
    Players,
    Challenge,
    Help,
    Failed
}

public class UI
{
    public PlayerManager? players = null;

    public void SetPlayers(PlayerManager players)
    {
        this.players = players;
        this.players.OnNameDenied += NameRejected;
        this.players.OnNameAssigned += Welcome;
    }

    public BoardManager? boards = null;

    public void SetBoards(BoardManager boards)
    {
        this.boards = boards;
        this.boards.OnChallengeRejected += ChallengeRejected;
        this.boards.OnChallengeReceived += ChallengeReceived;
        this.boards.OnGameStart += StartGame;
        this.boards.OnRequestMove += GetMove;
        this.boards.OnMoveReceived += DisplayMove;
        this.boards.OnGameOver += GameOver;
        this.boards.OnChallengeFailed += ChallengeFailed;
        this.boards.OnOpponentDisconnected += OpponentDisconnected;
    }

    private void DisplayMove(CheckersBoard board, ClientId player, BoardCell from, BoardCell to)
    {
        var name = players!.Connection.LocalId == player ? "you" : players!.GetName(player);
        Console.WriteLine($"{name} moved {from} to {to}\n{board}\n");
        if (name == "you")
            Console.WriteLine("\nwaiting for " + players!.GetName(board.NextTurn) +"'s move...\n");
    }

    private void GameOver(ClientId id)
    {
        var name = players!.Connection.LocalId == id ? "you" : players!.GetName(id);
        Console.WriteLine(name + " won!\n");
        Program.curState = ClientStates.GetCommand;
    }

    private void GetMove(CheckersBoard board)
    {
        bool goodResponse = false;
        while (!goodResponse)
        {
            Console.Write("your move (ex: 'b2 to c3'): ");
            var response = Console.ReadLine() ?? "";
            var tokens = response.Split(' ');
            if (tokens.Length != 3)
            {
                goodResponse = false;
                Console.WriteLine("format your move as '[cell] to [cell]', where the cells are written as (letter)(number), like a3, b2, or d6");
                continue;
            }

            if (!BoardCell.FromString(tokens[0], out var from))
            {
                goodResponse = false;
                Console.WriteLine("format your move as '[cell] to [cell]', where the cells are written as (letter)(number), like a3, b2, or d6");
                continue;
            }

            if (!BoardCell.FromString(tokens[2], out var to))
            {
                goodResponse = false;
                Console.WriteLine("format your move as '[cell] to [cell]', where the cells are written as (letter)(number), like a3, b2, or d6");
                continue;
            }

            if (!board.ValidateMove(from, to))
            {
                Console.WriteLine("you move must be valid");
                continue;
            }

            boards!.MakeMove(board.Id, from, to);
            break;
        }
    }

    private void StartGame(ClientId id1, ClientId id2, CheckersBoard board)
    {
        bool id1IsRed = id1 == board.RedPlayer;
        bool id2IsRed = id2 == board.RedPlayer;
        Console.WriteLine($"\nNew game, '{players!.GetName(id1)}' ({(id1IsRed ? 'R' : 'B')}) vs '{players!.GetName(id2)}' ({(id2IsRed ? 'R' : 'B')})\n" + board.ToString());
        if (board.NextTurn != players!.Connection.LocalId)
            Console.WriteLine("\nwaiting for " + players!.GetName(board.NextTurn) +"'s move...\n");
        Program.curState = ClientStates.InGame;
    }

    private void ChallengeFailed()
    {
        Console.WriteLine("Game failed...\n");
        Program.curState = ClientStates.GetCommand;
    }

    private void OpponentDisconnected(ClientId id)
    {
        Console.WriteLine("Game ended, your opponent disconnected...\n");
        Program.curState = ClientStates.GetCommand;
    }

    private void ChallengeReceived(ClientId id)
    {
        Console.Write("\n'" + players!.GetName(id) + "' wants to play, accept? (yes/no): ");
        var response = Console.ReadLine() ?? "no";
        boards!.ChallengeResponse(id, response == "y" || response == "yes");
        Program.curState = ClientStates.WaitForChallenge;
    }

    private void ChallengeRejected(ClientId id)
    {
        Console.WriteLine("challenge rejected.\n");
        Program.curState = ClientStates.GetCommand;
    }

    public bool IsReady { get { return players != null && boards != null; } }

    public bool HasName { get { return name != null; } }

    public string? name = null;

    public void GetUsername()
    {
        Console.Write("Username: ");
        var name = Console.ReadLine() ?? PlayerManager.DefaultPlayerName;
        name.Replace(' ', '_');
        players!.SendUsername(name);
    }

    public void NameRejected()
    {
        Console.WriteLine("Someone already has that name, please enter another.");
        GetUsername();
    }

    public void Welcome(string name)
    {
        Console.WriteLine("Welcome, " + name + "!");
        this.name = name;
    }

    public Command GetCommand()
    {
        Console.Write("Command (h): ");
        var c = Console.ReadLine() ?? "";

        var tokens = c.Split(' ') ?? [""];

        switch(tokens[0])
        {
            case "q":
            case "quit": Quit(); return Command.Quit;

            case "p":
            case "players": ShowPlayers(); return Command.Players;

            case "play":
                if (tokens.Length == 2 && ChallengePlayer(tokens[1])) 
                    return Command.Challenge;
                Help();
                return Command.Failed;

            case "h":
            case "help": Help(); return Command.Help;
        }

        return Command.Failed;
    }

    private void Quit()
    {
        players!.Connection.Disconnect();
    }

    private void Help()
    {
        var str = "\nCheckers Club Commands:\n";
        str += "           (h)elp: displays this message\n";
        str += "        (p)layers: displays a list of players online\n";
        str += "  play (username): challenge a player to a game of checkers\n";
        str += "           (q)uit: disconnect from the server\n";
        
        Console.WriteLine(str);
    }

    private void ShowPlayers()
    {
        players!.CheckPlayers();
        var str = "\nPlayers:\n";
        
        foreach (var pair in players!.GetNames())
        {
            str += "  (" + pair.id.Id + ") " + pair.name + " ";
            if (pair.name == name)
                str += "(you)";
            else if (boards!.IsPlaying(pair.id))
                str += "[in game]";
            str += "\n";
        }

        Console.WriteLine(str);
    }

    private bool ChallengePlayer(string player)
    {
        if (!players!.HasName(player))
        {
            Console.WriteLine("\n'" + player + "' is not online\n");
            return false;
        }

        if (boards!.IsPlaying(players!.GetId(player)))
        {
            Console.WriteLine("\n'" + player +"' is already in a game\n");
            return false;
        }

        boards!.ChallengePlayer(players!.GetId(player));

        Console.WriteLine("Sending challenge to " + player + ", waiting for response...");
        return true;
    }
}
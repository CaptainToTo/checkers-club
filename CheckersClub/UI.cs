
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
    }

    public bool IsReady { get { return players != null && boards != null; } }

    public bool HasName { get { return name != null; } }

    public string? name = null;

    public void GetUsername()
    {
        Console.Write("Username: ");
        var name = Console.ReadLine();
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

    public bool GetCommand()
    {
        Console.Write("Command (h): ");
        var c = Console.ReadLine();

        switch(c)
        {
            case "q":
            case "quit": Quit(); break;

            case "p":
            case "players": ShowPlayers(); break;

            case "h":
            case "help": Help(); break;
        }

        return true;
    }

    private void Quit()
    {
        players!.Connection.Disconnect();
    }

    private void Help()
    {
        var str = "\nCheckers Club Commands:\n";
        str += "  (h)elp: displays this message\n";
        str += "  (p)layers: displays a list of players online\n";
        str += "  play (username): challenge a player to a game of checkers\n";
        str += "  (q)uit: disconnect from the server\n";
        
        Console.WriteLine(str);
    }

    private void ShowPlayers()
    {
        var str = "\nPlayers:\n";

        foreach (var pair in players!.GetNames())
        {
            str += "  " + name + " ";
            if (pair.name == name)
                str += "(you)";
            else if (boards!.IsPlaying(pair.id))
                str += "[in game]";
            str += "\n";
        }

        Console.WriteLine(str);
    }
}
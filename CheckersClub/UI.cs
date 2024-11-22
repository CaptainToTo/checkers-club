public class UI
{
    public PlayerManager? players = null;
    public BoardManager? boards = null;

    public bool IsReady { get { return players != null && boards != null; } }

    public void GetUsername()
    {
        Console.WriteLine("Username: ");
        var name = Console.ReadLine();
        players!.SendUsername(name);
    }

    public bool GetCommand()
    {
        Console.WriteLine("Command (h): ");
        var c = Console.ReadLine();

        return true;
    }
}
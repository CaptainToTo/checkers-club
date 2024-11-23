using OwlTree;

public static class ClientStates
{
    public static void WaitingForConnection(Connection client, UI ui)
    {
        if (PlayerManager.Instance != null)
            ui.SetPlayers(PlayerManager.Instance);
        if (BoardManager.Instance != null)
            ui.SetBoards(BoardManager.Instance);

        if (ui.IsReady)
        {
            ui.GetUsername();
            Program.curState = WaitingForName;
        }
    }

    public static void WaitingForName(Connection client, UI ui)
    {
        if (ui.HasName)
            Program.curState = GetCommand;
    }

    public static void GetCommand(Connection client, UI ui)
    {
        var command = ui.GetCommand();

        if (command == Command.Challenge)
            Program.curState = WaitForChallenge;
    }

    public static void WaitForChallenge(Connection client, UI ui)
    {

    }

    public static void InGame(Connection client, UI ui)
    {
        
    }
}
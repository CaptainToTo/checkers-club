using OwlTree;

public static class ClientStates
{
    // wait for the managers to spawn before getting the player's name, and entering the command loop
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

    // repeated ask for name until a response is accepted
    public static void WaitingForName(Connection client, UI ui)
    {
        if (ui.HasName)
            Program.curState = GetCommand;
    }

    // command loop
    public static void GetCommand(Connection client, UI ui)
    {
        var command = ui.GetCommand();

        if (command == Command.Challenge)
            Program.curState = WaitForChallenge;
    }

    // do nothing while waiting for the challenged player to response
    public static void WaitForChallenge(Connection client, UI ui)
    {

    }

    // update logic is handled in BoardManager, and UI
    public static void InGame(Connection client, UI ui)
    {
        
    }
}
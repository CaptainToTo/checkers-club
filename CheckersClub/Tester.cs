
using OwlTree;

public static class Tester
{
    public static void RunTests()
    {
        var board = new CheckersBoard(10);
        board.ResetBoard(ClientId.New(), ClientId.New());
        Console.WriteLine(board.ToString());
    }
}
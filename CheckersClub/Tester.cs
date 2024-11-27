
using OwlTree;

public static class Tester
{
    public static void RunTests()
    {
        var board = new CheckersBoard(10);
        board.ResetBoard(ClientId.New(), ClientId.New());
        Console.WriteLine(board.ToString());

        while (!board.IsGameOver)
        {
            bool goodResponse = false;
            while (!goodResponse)
            {
                Console.Write($"your move ({board.NextTurnPiece}): ");
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

                Console.WriteLine(from + "  " + to);

                if (!board.ValidateMove(from, to))
                {
                    Console.WriteLine("you move must be valid");
                    continue;
                }

                board.MovePiece(from, to);
                goodResponse = true;
                break;
            }
            Console.WriteLine(board.ToString());
        }
    }
}
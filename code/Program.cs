// See https://aka.ms/new-console-template for more information
using Chess;
using ChessPlay;
class Program
{
    static void Main(string[] args)
    {
        ChessBoard board = new ChessBoard();
        while (!board.IsEndGame)
        {
            ProgLogic pL = new ProgLogic(board);
            var moves = board.Moves();            
            board.Move(pL.compMove(board));
            Console.WriteLine(board.ToAscii());
            foreach (var move in board.Moves())
            {
                Console.WriteLine(move.ToString());
            }
            pL.playerMove(board);
            Thread.Sleep(1000);
            Console.Clear();
        }
    }
    
}
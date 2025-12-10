// See https://aka.ms/new-console-template for more information
using Chess;
using ChessPlay;
class Program
{
    static void Main(string[] args)
    {
        ProgLogic pL;
        ChessBoard board = new ChessBoard();
        Console.WriteLine("Press 1 for easy difficulty, 2 for normal, 3 for hard, 4 for very hard");
        string diffIn = Console.ReadLine();
        if (diffIn == "1")
            pL = new ProgLogic(board, 3);
        else if (diffIn == "2")
            pL = new ProgLogic(board, 5);
        else if (diffIn == "3")
            pL = new ProgLogic(board, 7);
        else if (diffIn == "4")
            pL = new ProgLogic(board, 9);
        else
            return;
        pL.populatePiecesAtStart();
        while (!board.IsEndGame)
        {
            var moves = pL.generateMoves(board, pL.pieceLocations);
            board.Move(pL.compMove(board));
            string strBoard = board.ToAscii();
            var sb = new System.Text.StringBuilder(strBoard);

            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '.')
                    sb[i] = '-';
            }

            strBoard = sb.ToString();
            Console.WriteLine(strBoard);
            Move[] moveArr = pL.generateMoves( board, pL.pieceLocations);
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
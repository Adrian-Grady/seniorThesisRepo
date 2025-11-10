using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chess;

namespace ChessPlay
{
    struct TreeNode
    {
        public ChessBoard board { get; set; }
        public List<TreeNode> childNodes { get; set; }
    }
    struct SearchResult
    {
        public int Score;
        public Move BestMove;
    }

    internal class ProgLogic
    {
        int totalCount = 0;
        ChessBoard board;
        public ProgLogic(ChessBoard inBoard) { board = inBoard; }
        public Move compMove(ChessBoard board)
        {
            TreeNode baseNode = new TreeNode { board = board, childNodes = null };
            SearchResult finalRes = recursiveBoardExplore(0, false, baseNode);
            Console.WriteLine(totalCount);
            return finalRes.BestMove;
        }
        private SearchResult recursiveBoardExplore(int depth, bool isWhiteTurn, TreeNode currentNode, int alpha = int.MinValue, int beta = int.MaxValue)
        {
            totalCount++;
            const int MaxDepth = 3;

            if (currentNode.board == null)
                throw new ArgumentNullException(nameof(currentNode.board));
            if (depth >= MaxDepth)
                return new SearchResult { Score = evaluatePosition(currentNode.board), BestMove = null };
            Move[] moves = orderMoves(currentNode.board, currentNode.board.Moves());
            if (moves == null || moves.Length == 0)
            {
                return new SearchResult { Score = evaluatePosition(currentNode.board), BestMove = null };
            }
            if (isWhiteTurn)
            {
                int bestScore = int.MinValue;
                Move bestMove = null;

                foreach (var m in moves)
                {
                    currentNode.board.Move(m);
                    var childNode = new TreeNode { board = currentNode.board, childNodes = null };
                    var childResult = recursiveBoardExplore(depth + 1, false, childNode, alpha, beta);
                    currentNode.board.Cancel();
                    if (childResult.Score > bestScore)
                    {
                        bestScore = childResult.Score;
                        bestMove = m;
                    }

                    alpha = Math.Max(alpha, bestScore);
                    if (alpha >= beta)
                        break;
                }

                return new SearchResult { Score = bestScore, BestMove = bestMove };
            }
            else
            {
                int bestScore = int.MaxValue;
                Move bestMove = null;
                foreach (var m in moves)
                {
                   currentNode.board.Move(m);
                    var childNode = new TreeNode { board = currentNode.board, childNodes = null };
                    var childResult = recursiveBoardExplore(depth + 1, true, childNode, alpha, beta);
                    currentNode.board.Cancel();
                    if (childResult.Score < bestScore)
                    {
                        bestScore = childResult.Score;
                        bestMove = m;
                    }
                    beta = Math.Min(beta, bestScore);
                    if (alpha >= beta) 
                        break;
                }
                return new SearchResult { Score = bestScore, BestMove = bestMove };
            }
        }
        private Move[] orderMoves(ChessBoard board, Move[] inMoves)
        {
            if (inMoves == null || inMoves.Length == 0)
                return Array.Empty<Move>();
            var mates = new List<Move>();
            var promotions = new List<Move>();
            var checks = new List<Move>();
            var longNotation = new List<Move>();
            var others = new List<Move>();

            for (int i = 0; i < inMoves.Length; i++)
            {
                var mv = inMoves[i];
                if (mv == null)
                    continue;
                if (mv.IsMate)
                {
                    mates.Add(mv);
                }
                else if (mv.IsPromotion)
                {
                    promotions.Add(mv);
                }
                else if (mv.IsCheck)
                {
                    checks.Add(mv);
                }
                else if (mv.ToString().Length > 12)
                {
                    longNotation.Add(mv);
                }
                else
                {
                    others.Add(mv);
                }
            }
            var othersWithMobility = new List<(Move Move, int Mobility)>(others.Count);
            foreach (var mv in others)
            {
                board.Move(mv);
                var avail = board.Moves();
                int mobility;
                if (avail != null)
                    mobility = avail.Length;
                else
                    mobility = 0;
                board.Cancel();
                othersWithMobility.Add((mv, mobility));
            }

            othersWithMobility.Sort((a, b) => b.Mobility.CompareTo(a.Mobility));

            int total = mates.Count + promotions.Count + checks.Count + longNotation.Count + othersWithMobility.Count;
            var sortedMoves = new Move[total];
            int idx = 0;

            void Append(List<Move> list)
            {
                for (int j = 0; j < list.Count; j++)
                    sortedMoves[idx++] = list[j];
            }

            Append(mates);
            Append(promotions);
            Append(checks);
            Append(longNotation);

            // Append sorted others
            for (int j = 0; j < othersWithMobility.Count; j++)
                sortedMoves[idx++] = othersWithMobility[j].Move;

            return sortedMoves;
        }
        public void playerMove(ChessBoard inBoard)
        {
            var moves = inBoard.Moves();
            bool valMove = false;
            Move move = null;
            Console.WriteLine("Enter your move in standard chess notation");
            string playerIn = Console.ReadLine();
            playerIn = "{" + playerIn + "}";
            for (int moveIter = 0; moveIter < moves.Length; moveIter++)
            {
                string strMove = moves[moveIter].ToString();
                if (strMove == playerIn)
                {
                    valMove = true;
                    move = moves[moveIter];
                    break;
                }
            }
            if (valMove == false)
            {
                Console.WriteLine("Invalid Move");
                playerMove(inBoard);
            }
            else
            {
                inBoard.Move(move);
                Console.WriteLine(inBoard.ToAscii());
            }
        }
        public int evaluatePosition(ChessBoard inBoard)
        {
            int currentPosValue = 0;
            const int pawnVal = 100;
            const int doubledPawnVal = 50;
            const int knightVal = 300;
            const int bishopVal = 300;
            const int rookVal = 500;
            const int queenVal = 900;
            const int kingVal = 1000000;
            const int openSpaceVal = 5;
            const int checkVal = 30;
            for (int xIter = 0; xIter < 8; xIter++)
            {
                bool doubledPawnCheck = false;
                for (int yIter = 0; yIter < 8; yIter++)
                {
                    if (inBoard[xIter, yIter] != null)
                    {
                        if (inBoard[xIter, yIter].Equals("wp") && doubledPawnCheck == false)
                        {
                            
                            currentPosValue += pawnVal;
                            doubledPawnCheck = true;
                        }
                        else if (inBoard[xIter, yIter].Equals("wp") && doubledPawnCheck == true)
                        {
                            currentPosValue += doubledPawnVal;
                        }
                        if (inBoard[xIter, yIter].Equals("wkn"))
                        {
                            currentPosValue += knightVal;
                        }
                        if (inBoard[xIter, yIter].Equals("wb"))
                        {
                            currentPosValue += bishopVal;
                        }

                        if (inBoard[xIter, yIter].Equals("wr"))
                        {
                            currentPosValue += rookVal;
                        }

                        if (inBoard[xIter, yIter].Equals("wq")  )
                        {
                            currentPosValue += queenVal;
                        }
                        if (inBoard[xIter, yIter].Equals("wk"))
                        {
                            currentPosValue += kingVal;
                        }
                        if (inBoard[xIter, yIter].Equals("bp") && doubledPawnCheck == false)
                        {
                            currentPosValue -= pawnVal;
                            doubledPawnCheck = true;
                        }
                        else if (inBoard[xIter, yIter].Equals("bp") && doubledPawnCheck == true)
                        {
                            currentPosValue -= doubledPawnVal;
                        }
                        if (inBoard[xIter, yIter].Equals("bkn"))
                        {
                            currentPosValue -= knightVal;
                        }
                        if (inBoard[xIter, yIter].Equals("bb"))
                        {
                            currentPosValue -= bishopVal;
                        }

                        if (inBoard[xIter, yIter].Equals("br"))
                        {
                            currentPosValue -= rookVal;
                        }

                        if (inBoard[xIter, yIter].Equals("bq"))
                        {
                            currentPosValue -= queenVal;
                        }
                        if (inBoard[xIter, yIter].Equals("bk"))
                        {
                            currentPosValue -= kingVal;
                        }
                    }
                }

            }
            if (inBoard.BlackKingChecked == true)
            {
                currentPosValue += checkVal;
            }
            if(inBoard.WhiteKingChecked == true)
            {
                currentPosValue -= checkVal;
            }
            for (int moveIter = 0; moveIter < inBoard.Moves().Length; moveIter++)
            {
                currentPosValue += openSpaceVal;
            }
            return currentPosValue;
        }


    }
}

using System;
using System.Collections.Generic;
using Chess;

namespace ChessPlay
{
    struct TreeNode
    {
        public ChessBoard board { get; set; }
        public List<TreeNode> childNodes { get; set; }
        public List<PieceLocation> PieceLocations { get; set; }
    }

    struct SearchResult
    {
        public int Score;
        public Move BestMove;
    }

    struct PieceLocation
    {
        public int piece;
        public bool isWhite;
        public int xPos;
        public int yPos;
        public bool kingHasMoved;
        public bool rookHasMoved;
    }

    internal struct TTEntry
    {
        public int Depth;
        public int Score;
        public Move BestMove;
    }

    internal class ProgLogic
    {
        public List<PieceLocation> pieceLocations = new List<PieceLocation>();

        int totalCount = 0;
        ChessBoard board;
        int depth;
        int capturedPieceValue = 0;

        zobrist zob;
        ulong rootHash;
        Dictionary<ulong, TTEntry> tt = new Dictionary<ulong, TTEntry>();

        public ProgLogic(ChessBoard inBoard, int maxDepth)
        {
            board = inBoard;
            depth = maxDepth;

            zob = new zobrist(inBoard);
            rootHash = zob.Hash;
        }

        public void populatePiecesAtStart()
        {
            pieceLocations.Clear();

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Piece p = board[x, y];
                    if (p == null) continue;

                    var pl = new PieceLocation();
                    pl.piece = MapPieceType(p.Type);
                    pl.isWhite = (p.Color == PieceColor.White);
                    pl.xPos = x;
                    pl.yPos = y;
                    pl.kingHasMoved = false;
                    pl.rookHasMoved = false;

                    pieceLocations.Add(pl);
                }
            }
        }

        private int MapPieceType(PieceType type)
        {
            if (type == PieceType.Pawn) return 0;
            if (type == PieceType.Rook) return 1;
            if (type == PieceType.Knight) return 2;
            if (type == PieceType.Bishop) return 3;
            if (type == PieceType.Queen) return 4;
            if (type == PieceType.King) return 5;
            return -1;
        }

        private List<PieceLocation> ClonePieceLocations(List<PieceLocation> src)
        {
            return new List<PieceLocation>(src);
        }

        private bool TryGetCapture(ChessBoard inBoard, Move mv,
                                   out int capX, out int capY, out Piece captured)
        {
            capX = -1;
            capY = -1;
            captured = null;

            int oldX = mv.OriginalPosition.X;
            int oldY = mv.OriginalPosition.Y;
            int newX = mv.NewPosition.X;
            int newY = mv.NewPosition.Y;

            Piece moving = inBoard[oldX, oldY];
            if (moving == null)
                return false;

            Piece dest = inBoard[newX, newY];

            if (dest != null && dest.Color != moving.Color)
            {
                capX = newX;
                capY = newY;
                captured = dest;
                return true;
            }

            if (moving.Type == PieceType.Pawn && oldX != newX && dest == null)
            {
                int epX = newX;
                int epY = oldY; // pawn is on the original rank
                Piece epPawn = inBoard[epX, epY];
                if (epPawn != null && epPawn.Type == PieceType.Pawn && epPawn.Color != moving.Color)
                {
                    capX = epX;
                    capY = epY;
                    captured = epPawn;
                    return true;
                }
            }

            return false;
        }

        public List<PieceLocation> updatePieceLoc(List<PieceLocation> pieceLocs,
                                                  Move inMove,
                                                  ChessBoard inBoard)
        {
            int oldX = inMove.OriginalPosition.X;
            int oldY = inMove.OriginalPosition.Y;
            int newX = inMove.NewPosition.X;
            int newY = inMove.NewPosition.Y;

            int movingIndex = -1;
            PieceLocation moving = default;

            for (int i = 0; i < pieceLocs.Count; i++)
            {
                var pl = pieceLocs[i];
                if (pl.xPos == oldX && pl.yPos == oldY)
                {
                    moving = pl;
                    movingIndex = i;
                    break;
                }
            }

            if (movingIndex == -1)
            {
                return pieceLocs;
            }

            Piece movingPieceOnBoard = inBoard[oldX, oldY];
            if (movingPieceOnBoard == null)
            {
                return pieceLocs;
            }

            if (TryGetCapture(inBoard, inMove, out int capX, out int capY, out Piece _))
            {
                for (int i = 0; i < pieceLocs.Count; i++)
                {
                    var pl = pieceLocs[i];
                    if (pl.xPos == capX && pl.yPos == capY)
                    {
                        pieceLocs.RemoveAt(i);
                        if (i < movingIndex) movingIndex--;
                        break;
                    }
                }
            }

            moving.xPos = newX;
            moving.yPos = newY;

            if (movingPieceOnBoard.Type == PieceType.King)
                moving.kingHasMoved = true;
            if (movingPieceOnBoard.Type == PieceType.Rook)
                moving.rookHasMoved = true;

            if (movingPieceOnBoard.Type == PieceType.King && oldX == 4)
            {
                if (newX == 6)      
                    MoveRookInPieceLocs(pieceLocs, 7, oldY, 5, oldY);
                else if (newX == 2)  
                    MoveRookInPieceLocs(pieceLocs, 0, oldY, 3, oldY);
            }

            pieceLocs.RemoveAt(movingIndex);
            pieceLocs.Add(moving);

            return pieceLocs;
        }

        private void MoveRookInPieceLocs(List<PieceLocation> pieceLocs,
                                         int oldX, int oldY, int newX, int newY)
        {
            for (int i = 0; i < pieceLocs.Count; i++)
            {
                var pl = pieceLocs[i];
                if (pl.xPos == oldX && pl.yPos == oldY)
                {
                    pl.xPos = newX;
                    pl.yPos = newY;
                    pl.rookHasMoved = true;
                    pieceLocs[i] = pl;
                    break;
                }
            }
        }

        public Move compMove(ChessBoard inBoard)
        {
            var searchBoard = ChessBoard.LoadFromFen(inBoard.ToFen());
            var rootPieces = ClonePieceLocations(pieceLocations);

            rootHash = zob.ComputeHash(searchBoard);
            tt.Clear();
            totalCount = 0;

            TreeNode root = new TreeNode
            {
                board = searchBoard,
                childNodes = null,
                PieceLocations = rootPieces
            };

            bool aiIsWhite = (searchBoard.Turn == PieceColor.White);
            SearchResult res = recursiveBoardExplore(
                0,
                aiIsWhite,
                root,
                int.MinValue,
                int.MaxValue,
                depth,
                capturedPieceValue,
                rootHash
            );

            Move best = res.BestMove;

            if (best != null)
            {
                if (best.CapturedPiece != null)
                    capturedPieceValue += GetCapturedPieceValue(best.CapturedPiece);
                pieceLocations = updatePieceLoc(pieceLocations, best, inBoard);
            }

            Console.WriteLine(totalCount);
            Console.WriteLine("AI Move: " + best);
            Console.WriteLine("AI Score: " + res.Score);

            return best;
        }

        private int PieceBaseValue(Piece p)
        {
            if (p == null) return 0;

            if (p.Type == PieceType.Pawn) return 100;
            if (p.Type == PieceType.Knight) return 300;
            if (p.Type == PieceType.Bishop) return 300;
            if (p.Type == PieceType.Rook) return 500;
            if (p.Type == PieceType.Queen) return 900;
            if (p.Type == PieceType.King) return 10000;

            return 0;
        }

        private SearchResult recursiveBoardExplore(
    int depth,
    bool maximizingWhite,
    TreeNode node,
    int alpha,
    int beta,
    int maxDepth,
    int inCapPieces,
    ulong hash)
        {
            if (node.board == null)
                throw new ArgumentNullException(nameof(node.board));

            totalCount++;

            Move[] moves = generateMoves(node.board, node.PieceLocations);
            int moveCount = (moves == null) ? 0 : moves.Length;
            int remainingDepth = maxDepth - depth;

            ulong ttKey = MakeTTKey(hash, inCapPieces);
            if (tt.TryGetValue(ttKey, out TTEntry entry) && entry.Depth >= remainingDepth)
            {
                return new SearchResult { Score = entry.Score, BestMove = entry.BestMove };
            }

            if (depth >= maxDepth || moveCount == 0)
            {
                int eval = evaluatePosition(node.board, inCapPieces, moveCount, maximizingWhite);
                tt[ttKey] = new TTEntry { Depth = remainingDepth, Score = eval, BestMove = null };
                return new SearchResult { Score = eval, BestMove = null };
            }

            moves = orderMoves(node.board, moves);

            bool whiteToMove = (node.board.Turn == PieceColor.White);
            bool isMaximizing = (whiteToMove == maximizingWhite);

            if (isMaximizing)
            {
                int bestScore = int.MinValue;
                Move bestMove = null;

                foreach (Move m in moves)
                {
                    int capPieces = inCapPieces; 

                    var childPieces = ClonePieceLocations(node.PieceLocations);
                    childPieces = updatePieceLoc(childPieces, m, node.board);

                    try
                    {
                        node.board.Move(m);
                        if (m.CapturedPiece != null)
                            capPieces += GetCapturedPieceValue(m.CapturedPiece);
                    }
                    catch
                    {
                        continue;
                    }

                    ulong childHash = zob.ComputeHash(node.board);

                    var childNode = new TreeNode
                    {
                        board = node.board,
                        childNodes = null,
                        PieceLocations = childPieces
                    };

                    var childRes = recursiveBoardExplore(
                        depth + 1,
                        maximizingWhite,
                        childNode,
                        alpha,
                        beta,
                        maxDepth,
                        capPieces,
                        childHash);

                    node.board.Cancel();

                    if (childRes.Score > bestScore)
                    {
                        bestScore = childRes.Score;
                        bestMove = m;
                    }

                    alpha = Math.Max(alpha, bestScore);
                    if (alpha >= beta)
                        break;
                }

                tt[ttKey] = new TTEntry { Depth = remainingDepth, Score = bestScore, BestMove = bestMove };
                return new SearchResult { Score = bestScore, BestMove = bestMove };
            }
            else
            {
                int bestScore = int.MaxValue;
                Move bestMove = null;

                foreach (Move m in moves)
                {
                    int capPieces = inCapPieces;  // <-- moved here

                    var childPieces = ClonePieceLocations(node.PieceLocations);
                    childPieces = updatePieceLoc(childPieces, m, node.board);

                    try
                    {
                        node.board.Move(m);
                        if (m.CapturedPiece != null)
                            capPieces += GetCapturedPieceValue(m.CapturedPiece);
                    }
                    catch
                    {
                        continue;
                    }

                    ulong childHash = zob.ComputeHash(node.board);

                    var childNode = new TreeNode
                    {
                        board = node.board,
                        childNodes = null,
                        PieceLocations = childPieces
                    };

                    var childRes = recursiveBoardExplore(
                        depth + 1,
                        maximizingWhite,
                        childNode,
                        alpha,
                        beta,
                        maxDepth,
                        capPieces,
                        childHash);

                    node.board.Cancel();

                    if (childRes.Score < bestScore)
                    {
                        bestScore = childRes.Score;
                        bestMove = m;
                    }

                    beta = Math.Min(beta, bestScore);
                    if (alpha >= beta)
                        break;
                }

                tt[ttKey] = new TTEntry { Depth = remainingDepth, Score = bestScore, BestMove = bestMove };
                return new SearchResult { Score = bestScore, BestMove = bestMove };
            }
        }

        private int GetCapturedPieceValue(Piece capPiece)
        {
            int baseVal;
            if (capPiece.Type == PieceType.Pawn) baseVal = 100;
            else if (capPiece.Type == PieceType.Bishop) baseVal = 300;
            else if (capPiece.Type == PieceType.Knight) baseVal = 300;
            else if (capPiece.Type == PieceType.Rook) baseVal = 500;
            else if (capPiece.Type == PieceType.Queen) baseVal = 900;
            else if (capPiece.Type == PieceType.King) baseVal = 100000;
            else baseVal = 0;

            if (capPiece.Color == PieceColor.White)
                return -baseVal;
            else
                return baseVal;
        }


        private Move[] orderMoves(ChessBoard board, Move[] inMoves)
        {
            if (inMoves == null || inMoves.Length == 0)
                return Array.Empty<Move>();

            var scored = new List<(Move move, int score)>(inMoves.Length);

            foreach (var mv in inMoves)
            {
                if (mv == null) continue;

                int sx = mv.OriginalPosition.X;
                int sy = mv.OriginalPosition.Y;

                Piece attacker = board[sx, sy];
                if (attacker == null)
                {
                    scored.Add((mv, int.MinValue + 1));
                    continue;
                }

                int score = 0;

                if (TryGetCapture(board, mv, out int capX, out int capY, out Piece victim) &&
                    victim != null)
                {
                    int victimVal = PieceBaseValue(victim);
                    int attackerVal = PieceBaseValue(attacker);
                    score += 10000 + (victimVal * 10) - attackerVal;
                }

                if (attacker.Type == PieceType.Pawn)
                {
                    int ty = mv.NewPosition.Y;
                    if (ty == 0 || ty == 7)
                    {
                        score += 5000;
                    }
                }

                if (mv.IsCheck)
                {
                    score += 3000;
                }

                if (victim == null && !mv.IsCheck)
                {
                    int attackerVal = PieceBaseValue(attacker);
                    score += 1000 - attackerVal; 
                }

                scored.Add((mv, score));
            }
            scored.Sort((a, b) => b.score.CompareTo(a.score));

            Move[] result = new Move[scored.Count];
            for (int i = 0; i < scored.Count; i++)
                result[i] = scored[i].move;

            return result;
        }
        private int PieceSquareBonus(Piece p, int x, int y)
        {
            int bonus = 0;

            if (p.Type == PieceType.Knight || p.Type == PieceType.Bishop)
            {
                int dx = Math.Min(x, 7 - x);
                int dy = Math.Min(y, 7 - y);
                bonus += (dx + dy) * 4;      
            }

            if (p.Type == PieceType.Pawn)
            { 
                if (p.Color == PieceColor.White)
                    bonus += y * 2;
                else
                    bonus += (7 - y) * 2;

                int distFromCenterFile = Math.Min(Math.Abs(x - 3), Math.Abs(x - 4));
                bonus += (3 - distFromCenterFile) * 2;
            }

            if (p.Type == PieceType.Rook)
            {
                int emptyCount = 0;
                for (int ry = 0; ry < 8; ry++)
                {
                    if (ry == y) continue;
                    if (board[x, ry] == null) emptyCount++;
                }
                bonus += emptyCount;
            }

            if (p.Type == PieceType.Queen)
            {
                int dx = Math.Min(x, 7 - x);
                int dy = Math.Min(y, 7 - y);
                bonus += (dx + dy) * 2;
            }

            return bonus;
        }
        private int MaterialFromBoard(ChessBoard b, out int psq)
        {
            int material = 0;
            psq = 0;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Piece p = b[x, y];
                    if (p == null) continue;

                    int val = PieceBaseValue(p);       
                    int sq = PieceSquareBonus(p, x, y); 

                    if (p.Color == PieceColor.White)
                    {
                        material += val;
                        psq += sq;
                    }
                    else
                    {
                        material -= val;
                        psq -= sq;
                    }
                }
            }

            return material;
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
            if (!valMove)
            {
                Console.WriteLine("Invalid Move");
                playerMove(inBoard);
            }
            else
            {
                if (move.CapturedPiece != null)
                {
                    int capPieceVal = GetCapturedPieceValue(move.CapturedPiece);
                    capturedPieceValue += capPieceVal;
                }
                pieceLocations = updatePieceLoc(pieceLocations, move, inBoard);
                inBoard.Move(move);

                rootHash = zob.ComputeHash(inBoard);

                Console.WriteLine(inBoard.ToAscii());
            }
        }

        public char intToChar(int input)
        {
            return (char)(input + (int)'a');
        }

        public bool isPosInbounds(int x, int y)
        {
            return !(x < 0 || x > 7 || y < 0 || y > 7);
        }

        public string Square(int x, int y)
        {
            y++;
            return intToChar(x).ToString() + y.ToString();
        }

        private bool IsSquareAttacked(ChessBoard board, int targetX, int targetY, PieceColor byColor)
        {
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Piece p = board[x, y];
                    if (p == null || p.Color != byColor) continue;

                    if (p.Type == PieceType.Pawn)
                    {
                        int dir = (byColor == PieceColor.White) ? 1 : -1;
                        if (x + 1 == targetX && y + dir == targetY) return true;
                        if (x - 1 == targetX && y + dir == targetY) return true;
                    }
                    else if (p.Type == PieceType.Knight)
                    {
                        int[] kx = { 1, 2, 2, 1, -1, -2, -2, -1 };
                        int[] ky = { 2, 1, -1, -2, -2, -1, 1, 2 };
                        for (int i = 0; i < 8; i++)
                        {
                            int nx = x + kx[i];
                            int ny = y + ky[i];
                            if (nx == targetX && ny == targetY)
                                return true;
                        }
                    }
                    else if (p.Type == PieceType.Bishop ||
                             p.Type == PieceType.Rook ||
                             p.Type == PieceType.Queen)
                    {
                        (int dx, int dy)[] dirs;
                        if (p.Type == PieceType.Bishop)
                        {
                            dirs = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };
                        }
                        else if (p.Type == PieceType.Rook)
                        {
                            dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                        }
                        else
                        {
                            dirs = new[]
                            {
                                (1,0),(-1,0),(0,1),(0,-1),
                                (1,1),(1,-1),(-1,1),(-1,-1)
                            };
                        }

                        foreach (var d in dirs)
                        {
                            int nx = x + d.dx;
                            int ny = y + d.dy;
                            while (isPosInbounds(nx, ny))
                            {
                                if (nx == targetX && ny == targetY)
                                    return true;

                                if (board[nx, ny] != null)
                                    break;

                                nx += d.dx;
                                ny += d.dy;
                            }
                        }
                    }
                    else if (p.Type == PieceType.King)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx == targetX && ny == targetY)
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
        public Move[] generateMoves(ChessBoard inBoard, List<PieceLocation> alivePieces)
        {
            bool whiteToMove = (inBoard.Turn == PieceColor.White);
            var moves = new List<Move>();

            foreach (var pl in alivePieces)
            {
                int x = pl.xPos;
                int y = pl.yPos;
                if (!isPosInbounds(x, y)) continue;

                Piece p = inBoard[x, y];
                if (p == null) continue;

                if ((p.Color == PieceColor.White) != whiteToMove)
                    continue;

                if (p.Type == PieceType.Pawn)
                {
                    GeneratePawnMoves(inBoard, moves, x, y, p.Color);
                }
                else if (p.Type == PieceType.Rook)
                {
                    GenerateSlidingMoves(inBoard, moves, x, y, p.Color,
                        new (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) });
                }
                else if (p.Type == PieceType.Bishop)
                {
                    GenerateSlidingMoves(inBoard, moves, x, y, p.Color,
                        new (int, int)[] { (1, 1), (1, -1), (-1, 1), (-1, -1) });
                }
                else if (p.Type == PieceType.Queen)
                {
                    GenerateSlidingMoves(inBoard, moves, x, y, p.Color,
                        new (int, int)[] {
                            (1,0),(-1,0),(0,1),(0,-1),
                            (1,1),(1,-1),(-1,1),(-1,-1)
                        });
                }
                else if (p.Type == PieceType.Knight)
                {
                    GenerateKnightMoves(inBoard, moves, x, y, p.Color);
                }
                else if (p.Type == PieceType.King)
                {
                    GenerateKingMovesAndCastling(inBoard, moves, alivePieces, pl, p.Color);
                }
            }

            return moves.ToArray();
        }

        private void GeneratePawnMoves(ChessBoard board, List<Move> moves, int x, int y, PieceColor color)
        {
            int dir = (color == PieceColor.White) ? 1 : -1;
            int startRank = (color == PieceColor.White) ? 1 : 6;

            int ny = y + dir;
            if (isPosInbounds(x, ny) && board[x, ny] == null)
            {
                moves.Add(new Move(Square(x, y), Square(x, ny)));

                if (y == startRank)
                {
                    int ny2 = y + 2 * dir;
                    if (isPosInbounds(x, ny2) && board[x, ny2] == null)
                    {
                        moves.Add(new Move(Square(x, y), Square(x, ny2)));
                    }
                }
            }

            int[] dxs = { -1, 1 };
            foreach (int dx in dxs)
            {
                int nx = x + dx;
                ny = y + dir;
                if (!isPosInbounds(nx, ny)) continue;
                Piece dest = board[nx, ny];
                if (dest != null && dest.Color != color)
                {
                    moves.Add(new Move(Square(x, y), Square(nx, ny)));
                }
            }

            Move last = null;
            try
            {
                if (board.ExecutedMoves.Count > 0)
                    last = board.ExecutedMoves[board.ExecutedMoves.Count - 1];
            }
            catch
            {
                last = null;
            }

            if (last != null && last.Piece != null && last.Piece.Type == PieceType.Pawn)
            {
                int fromY = last.OriginalPosition.Y;
                int toY = last.NewPosition.Y;
                if (Math.Abs(fromY - toY) == 2)
                {
                    int lastX = last.NewPosition.X;
                    int lastY = last.NewPosition.Y;

                    if (lastY == y && Math.Abs(lastX - x) == 1)
                    {
                        int epX = lastX;
                        int epY = y + dir;
                        if (isPosInbounds(epX, epY) && board[epX, epY] == null)
                        {
                            moves.Add(new Move(Square(x, y), Square(epX, epY)));
                        }
                    }
                }
            }
        }

        private void GenerateSlidingMoves(ChessBoard board, List<Move> moves,
                                          int x, int y, PieceColor color,
                                          (int dx, int dy)[] dirs)
        {
            foreach (var d in dirs)
            {
                int nx = x + d.dx;
                int ny = y + d.dy;
                while (isPosInbounds(nx, ny))
                {
                    Piece dest = board[nx, ny];
                    if (dest == null)
                    {
                        moves.Add(new Move(Square(x, y), Square(nx, ny)));
                    }
                    else
                    {
                        if (dest.Color != color)
                            moves.Add(new Move(Square(x, y), Square(nx, ny)));
                        break;
                    }
                    nx += d.dx;
                    ny += d.dy;
                }
            }
        }

        private void GenerateKnightMoves(ChessBoard board, List<Move> moves,
                                         int x, int y, PieceColor color)
        {
            int[] kx = { 1, 2, 2, 1, -1, -2, -2, -1 };
            int[] ky = { 2, 1, -1, -2, -2, -1, 1, 2 };

            for (int i = 0; i < 8; i++)
            {
                int nx = x + kx[i];
                int ny = y + ky[i];
                if (!isPosInbounds(nx, ny)) continue;

                Piece dest = board[nx, ny];
                if (dest == null || dest.Color != color)
                {
                    moves.Add(new Move(Square(x, y), Square(nx, ny)));
                }
            }
        }

        private void GenerateKingMovesAndCastling(ChessBoard board, List<Move> moves,
                                                  List<PieceLocation> alivePieces,
                                                  PieceLocation kingLoc,
                                                  PieceColor color)
        {
            int x = kingLoc.xPos;
            int y = kingLoc.yPos;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!isPosInbounds(nx, ny)) continue;

                    Piece dest = board[nx, ny];
                    if (dest == null || dest.Color != color)
                    {
                        moves.Add(new Move(Square(x, y), Square(nx, ny)));
                    }
                }
            }

            if (x == 4 && !kingLoc.kingHasMoved)
            {
                PieceColor enemyColor = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;

                PieceLocation? ksRook = null;
                for (int i = 0; i < alivePieces.Count; i++)
                {
                    var pl = alivePieces[i];
                    if (pl.isWhite == (color == PieceColor.White) &&
                        pl.xPos == 7 && pl.yPos == y && !pl.rookHasMoved)
                    {
                        Piece rp = board[pl.xPos, pl.yPos];
                        if (rp != null && rp.Type == PieceType.Rook && rp.Color == color)
                        {
                            ksRook = pl;
                            break;
                        }
                    }
                }

                if (ksRook.HasValue)
                {
                    if (board[5, y] == null && board[6, y] == null &&
                        !IsSquareAttacked(board, 4, y, enemyColor) &&
                        !IsSquareAttacked(board, 5, y, enemyColor) &&
                        !IsSquareAttacked(board, 6, y, enemyColor))
                    {
                        moves.Add(new Move(Square(x, y), Square(6, y)));
                    }
                }

                PieceLocation? qsRook = null;
                for (int i = 0; i < alivePieces.Count; i++)
                {
                    var pl = alivePieces[i];
                    if (pl.isWhite == (color == PieceColor.White) &&
                        pl.xPos == 0 && pl.yPos == y && !pl.rookHasMoved)
                    {
                        Piece rp = board[pl.xPos, pl.yPos];
                        if (rp != null && rp.Type == PieceType.Rook && rp.Color == color)
                        {
                            qsRook = pl;
                            break;
                        }
                    }
                }

                if (qsRook.HasValue)
                {
                    if (board[3, y] == null && board[2, y] == null && board[1, y] == null &&
                        !IsSquareAttacked(board, 4, y, enemyColor) &&
                        !IsSquareAttacked(board, 3, y, enemyColor) &&
                        !IsSquareAttacked(board, 2, y, enemyColor))
                    {
                        moves.Add(new Move(Square(x, y), Square(2, y)));
                    }
                }
            }
        }
        private ulong MakeTTKey(ulong boardHash, int capPieces)
        {
            unchecked
            {
                ulong m = 0x9e3779b97f4a7c15UL;   // some arbitrary 64-bit odd constant
                ulong cp = (ulong)(uint)capPieces;
                ulong h = boardHash;
                h ^= cp + m + (h << 6) + (h >> 2);
                return h;
            }
        }

        public int evaluatePosition(ChessBoard inBoard, int capPieces, int moveCount, bool maximizingWhite)
        {
            int score = 0;
            const int openSpaceVal = 2; 
            const int checkVal = 30;

            int psq;
            int material = MaterialFromBoard(inBoard, out psq);
            score += material;
            score += psq;
            if (inBoard.BlackKingChecked)
                score += checkVal;  
            if (inBoard.WhiteKingChecked)
                score -= checkVal;   
            bool sideToMoveIsMax =
                (inBoard.Turn == PieceColor.White) == maximizingWhite;

            int mobilityTerm = moveCount * openSpaceVal;
            if (sideToMoveIsMax)
                score += mobilityTerm;
            else
                score -= mobilityTerm;

            return score;
        }


    }
}

using System;
using Chess;

namespace ChessPlay
{
    internal class zobrist
    {
        private readonly ulong[,,] pieceKeys = new ulong[2, 6, 64];
        private readonly ulong sideKey;


        public ulong Hash { get; private set; }

        public zobrist(ChessBoard board)
        {
            var rand = new Random(0);

            for (int c = 0; c < 2; c++)
            {
                for (int t = 0; t < 6; t++)
                {
                    for (int sq = 0; sq < 64; sq++)
                    {
                        pieceKeys[c, t, sq] = Rand64(rand);
                    }
                }
            }
            sideKey = Rand64(rand);

            Hash = ComputeHash(board);
        }

        private ulong Rand64(Random rand)
        {
            byte[] buffer = new byte[8];
            rand.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }

        private int PieceTypeIndex(PieceType type)
        {
            if(type == PieceType.Pawn) return 0;
            else if(type == PieceType.Knight) return 1;
            else if(type == PieceType.Bishop) return 2;
            else if(type == PieceType.Rook) return 3;
            else if(type == PieceType.Queen) return 4;
            else if(type == PieceType.King) return 5;
            else throw new ArgumentException("Invalid piece type for Zobrist hashing");
        }

        private int ColorIndex(PieceColor color)
        {
            return (color == PieceColor.White) ? 0 : 1;
        }

        private int SquareIndex(int x, int y)
        {
            return y * 8 + x;
        }

        public ulong ComputeHash(ChessBoard board)
        {
            ulong h = 0;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Piece p = board[x, y];
                    if (p == null) continue;

                    int c = ColorIndex(p.Color);
                    int t = PieceTypeIndex(p.Type);
                    int sq = SquareIndex(x, y);

                    h ^= pieceKeys[c, t, sq];
                }
            }

            if (board.Turn == PieceColor.White)
                h ^= sideKey;

            return h;
        }
        public ulong TogglePiece(ulong hash, Piece p, int x, int y)
        {
            int c = ColorIndex(p.Color);
            int t = PieceTypeIndex(p.Type);
            int sq = SquareIndex(x, y);
            return hash ^ pieceKeys[c, t, sq];
        }

        public ulong ToggleSide(ulong hash)
        {
            return hash ^ sideKey;
        }
        public ulong UpdateHashForMove(ulong hash, ChessBoard board, Move move)
        {
            // toggle side
            hash = ToggleSide(hash);

            int fromX = move.OriginalPosition.X;
            int fromY = move.OriginalPosition.Y;
            int toX = move.NewPosition.X;
            int toY = move.NewPosition.Y;

            Piece moving = board[fromX, fromY];
            if (moving == null)
            {
                moving = move.Piece;
            }

            if (moving == null)
            {
                return hash;
            }

            hash = TogglePiece(hash, moving, fromX, fromY);
            hash = TogglePiece(hash, moving, toX, toY);

            if (move.CapturedPiece != null)
            {
                hash = TogglePiece(hash, move.CapturedPiece, toX, toY);
            }

            return hash;
        }
    }
}

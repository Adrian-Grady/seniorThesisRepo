using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chess;
namespace ChessPlay
{
    internal class zobrist
    {
        ulong[,] pieceKeys = new ulong[12, 64];
        ulong sideKey;
        ulong[] castleKeys = new ulong[16];
        ulong[] enPassantKeys = new ulong[8];
        public ulong pawn;
        public ulong knight;
        public ulong bishop;
        public ulong rook;
        public ulong queen;
        public ulong king;
        public ulong hash;
        public enum Square
        {
            A1, B1, C1, D1, E1, F1, G1, H1,
            A2, B2, C2, D2, E2, F2, G2, H2,
            A3, B3, C3, D3, E3, F3, G3, H3,
            A4, B4, C4, D4, E4, F4, G4, H4,
            A5, B5, C5, D5, E5, F5, G5, H5,
            A6, B6, C6, D6, E6, F6, G6, H6,
            A7, B7, C7, D7, E7, F7, G7, H7,
            A8, B8, C8, D8, E8, F8, G8, H8
        }
        public enum PieceKey
        {
            WhitePawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
            BlackPawn, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing
        }
        public void updateHash(Move moveToHash, ulong hash)
        {
           string strPiece = moveToHash.Piece.ToString();
            PieceKey pkPieceValue;
            PieceFromSymbol.TryGetValue(strPiece, out pkPieceValue);
            int iVal = ((int)pkPieceValue);
            SquareFromName.TryGetValue(moveToHash.OriginalPosition.ToString(), out Square sqFromValue);
            SquareFromName.TryGetValue(moveToHash.NewPosition.ToString(), out Square sqToValue);
            hash ^= pieceKeys[iVal, (int)sqFromValue];
            hash ^= pieceKeys[iVal, (int)sqToValue];
        }
        private static readonly Dictionary<string, Square> SquareFromName = new()
        {
            ["a1"] = Square.A1,
            ["b1"] = Square.B1,
            ["c1"] = Square.C1,
            ["d1"] = Square.D1,
            ["e1"] = Square.E1,
            ["f1"] = Square.F1,
            ["g1"] = Square.G1,
            ["h1"] = Square.H1,
            ["a2"] = Square.A2,
            ["b2"] = Square.B2,
            ["c2"] = Square.C2,
            ["d2"] = Square.D2,
            ["e2"] = Square.E2,
            ["f2"] = Square.F2,
            ["g2"] = Square.G2,
            ["h2"] = Square.H2,
            ["a3"] = Square.A3,
            ["b3"] = Square.B3,
            ["c3"] = Square.C3,
            ["d3"] = Square.D3,
            ["e3"] = Square.E3,
            ["f3"] = Square.F3,
            ["g3"] = Square.G3,
            ["h3"] = Square.H3,
            ["a4"] = Square.A4,
            ["b4"] = Square.B4,
            ["c4"] = Square.C4,
            ["d4"] = Square.D4,
            ["e4"] = Square.E4,
            ["f4"] = Square.F4,
            ["g4"] = Square.G4,
            ["h4"] = Square.H4,
            ["a5"] = Square.A5,
            ["b5"] = Square.B5,
            ["c5"] = Square.C5,
            ["d5"] = Square.D5,
            ["e5"] = Square.E5,
            ["f5"] = Square.F5,
            ["g5"] = Square.G5,
            ["h5"] = Square.H5,
            ["a6"] = Square.A6,
            ["b6"] = Square.B6,
            ["c6"] = Square.C6,
            ["d6"] = Square.D6,
            ["e6"] = Square.E6,
            ["f6"] = Square.F6,
            ["g6"] = Square.G6,
            ["h6"] = Square.H6,
            ["a7"] = Square.A7,
            ["b7"] = Square.B7,
            ["c7"] = Square.C7,
            ["d7"] = Square.D7,
            ["e7"] = Square.E7,
            ["f7"] = Square.F7,
            ["g7"] = Square.G7,
            ["h7"] = Square.H7,
            ["a8"] = Square.A8,
            ["b8"] = Square.B8,
            ["c8"] = Square.C8,
            ["d8"] = Square.D8,
            ["e8"] = Square.E8,
            ["f8"] = Square.F8,
            ["g8"] = Square.G8,
            ["h8"] = Square.H8
        };
        private static readonly Dictionary<string, PieceKey> PieceFromSymbol = new()
        {
            ["wp"] = PieceKey.WhitePawn,
            ["wn"] = PieceKey.WhiteKnight,
            ["wb"] = PieceKey.WhiteBishop,
            ["wr"] = PieceKey.WhiteRook,
            ["wq"] = PieceKey.WhiteQueen,
            ["wk"] = PieceKey.WhiteKing,

            ["bp"] = PieceKey.BlackPawn,
            ["bn"] = PieceKey.BlackKnight,
            ["bb"] = PieceKey.BlackBishop,
            ["br"] = PieceKey.BlackRook,
            ["bq"] = PieceKey.BlackQueen,
            ["bk"] = PieceKey.BlackKing
        };
        public zobrist(ChessBoard board)
        {
            Random rand = new Random(0); // fixed seed for reproducibility

            for (int piece = 0; piece < 12; piece++)
            {
                for (int square = 0; square < 64; square++)
                {
                    pieceKeys[piece, square] = rand64(rand);
                }
            }

            sideKey = rand64(rand);
            for (int i = 0; i < 16; i++)
            {
                castleKeys[i] = rand64(rand);
            }
            for (int i = 0; i < 8; i++)
            {
                enPassantKeys[i] = rand64(rand);
            }
            for(int rowIter = 0; rowIter < 8; rowIter++)
            {
                for(int colIter = 0; colIter < 8; colIter++)
                {
                    Piece inPiece = board[rowIter, colIter];
                    string strPiece = inPiece.ToString();
                    PieceKey pkPieceValue;
                    PieceFromSymbol.TryGetValue(strPiece, out pkPieceValue);
                    int iVal = ((int)pkPieceValue);
                    int rowNum = rowIter * 8 + colIter;
                    if(strPiece != null)
                    {
                        hash ^= pieceKeys[iVal, rowNum];
                    }
                }
            }
        }
        private ulong rand64(Random rand)
        {
            byte[] buffer = new byte[8];
            rand.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}

using System.Numerics;
using System.Runtime.CompilerServices;

namespace LifeGame.Core;

/// <summary>
/// 64セルを詰めたワード単位で、B3/S23(誕生3、生存2・3)の次世代をビット演算だけで求める。
/// a・b・c はそれぞれ上・中央・下の行のワードで、Prev/Next は同じ行の左右隣のワード。
/// 近傍8マスの数を、ビットごとの加算器(半加算器・全加算器)で64セル同時に数える。
/// </summary>
public static class LifeRule
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Next(
        ulong aPrev, ulong a, ulong aNext,
        ulong bPrev, ulong b, ulong bNext,
        ulong cPrev, ulong c, ulong cNext)
    {
        // 左隣(x-1)と右隣(x+1)のセルを、ビット x の位置へ寄せる
        ulong aW = (a << 1) | (aPrev >> 63);
        ulong aE = (a >> 1) | (aNext << 63);
        ulong bW = (b << 1) | (bPrev >> 63);
        ulong bE = (b >> 1) | (bNext << 63);
        ulong cW = (c << 1) | (cPrev >> 63);
        ulong cE = (c >> 1) | (cNext << 63);

        // 上下の行は3マスの和を全加算器で2ビットにする(1が1の位、2が2の位)
        ulong a1 = aW ^ a ^ aE;
        ulong a2 = (aW & a) | (aE & (aW ^ a));
        ulong c1 = cW ^ c ^ cE;
        ulong c2 = (cW & c) | (cE & (cW ^ c));
        // 中央の行は自分を除いた左右2マスの和(半加算器)
        ulong b1 = bW ^ bE;
        ulong b2 = bW & bE;

        // 1の位を全加算し、繰り上がり carry を2の位へ回す
        ulong ones = a1 ^ b1 ^ c1;
        ulong carry = (a1 & b1) | (c1 & (a1 ^ b1));

        // 2の位の4ビット(a2, b2, c2, carry)がちょうど1つ立つとき、近傍の数は2か3
        ulong p = a2 ^ b2;
        ulong q = c2 ^ carry;
        ulong exactlyOneTwo = (p ^ q) & ~((a2 & b2) | (c2 & carry));

        // 3なら誕生か生存、2なら生存中のセルだけ残る
        return exactlyOneTwo & (ones | b);
    }

    /// <summary><see cref="Next(ulong, ulong, ulong, ulong, ulong, ulong, ulong, ulong, ulong)"/> を、Vector の幅の分だけ同時に行う。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<ulong> Next(
        Vector<ulong> aPrev, Vector<ulong> a, Vector<ulong> aNext,
        Vector<ulong> bPrev, Vector<ulong> b, Vector<ulong> bNext,
        Vector<ulong> cPrev, Vector<ulong> c, Vector<ulong> cNext)
    {
        var aW = Vector.ShiftLeft(a, 1) | Vector.ShiftRightLogical(aPrev, 63);
        var aE = Vector.ShiftRightLogical(a, 1) | Vector.ShiftLeft(aNext, 63);
        var bW = Vector.ShiftLeft(b, 1) | Vector.ShiftRightLogical(bPrev, 63);
        var bE = Vector.ShiftRightLogical(b, 1) | Vector.ShiftLeft(bNext, 63);
        var cW = Vector.ShiftLeft(c, 1) | Vector.ShiftRightLogical(cPrev, 63);
        var cE = Vector.ShiftRightLogical(c, 1) | Vector.ShiftLeft(cNext, 63);

        var a1 = aW ^ a ^ aE;
        var a2 = (aW & a) | (aE & (aW ^ a));
        var c1 = cW ^ c ^ cE;
        var c2 = (cW & c) | (cE & (cW ^ c));
        var b1 = bW ^ bE;
        var b2 = bW & bE;

        var ones = a1 ^ b1 ^ c1;
        var carry = (a1 & b1) | (c1 & (a1 ^ b1));

        var p = a2 ^ b2;
        var q = c2 ^ carry;
        var exactlyOneTwo = Vector.AndNot(p ^ q, (a2 & b2) | (c2 & carry));

        return exactlyOneTwo & (ones | b);
    }
}

using System.Collections.Generic;
using System.Text;

/// <summary>Small local QR encoder (Model 2, version 4-L, byte mode), sufficient for LAN game URLs.</summary>
internal static class LocalQrCode
{
    private const int Version = 4;
    private const int Size = 17 + 4 * Version;
    private const int DataCodewords = 80;
    private const int ErrorCodewords = 20;

    public static bool TryEncode(string value, out bool[,] result)
    {
        result = null;
        byte[] payload = Encoding.UTF8.GetBytes(value ?? string.Empty);
        if (payload.Length > 78) return false;

        var bits = new List<bool>(DataCodewords * 8);
        AppendBits(bits, 0x4, 4);
        AppendBits(bits, payload.Length, 8);
        foreach (byte b in payload) AppendBits(bits, b, 8);
        int capacity = DataCodewords * 8;
        for (int i = 0; i < 4 && bits.Count < capacity; i++) bits.Add(false);
        while ((bits.Count & 7) != 0) bits.Add(false);
        for (int pad = 0; bits.Count < capacity; pad++) AppendBits(bits, pad % 2 == 0 ? 0xEC : 0x11, 8);
        var data = new byte[DataCodewords];
        for (int i = 0; i < data.Length; i++)
            for (int j = 0; j < 8; j++) data[i] = (byte)((data[i] << 1) | (bits[i * 8 + j] ? 1 : 0));
        byte[] codewords = AddErrorCorrection(data);

        bool[,] baseMatrix = new bool[Size, Size];
        bool[,] function = new bool[Size, Size];
        DrawFinder(baseMatrix, function, 0, 0);
        DrawFinder(baseMatrix, function, Size - 7, 0);
        DrawFinder(baseMatrix, function, 0, Size - 7);
        DrawAlignment(baseMatrix, function, 26, 26);
        for (int i = 8; i < Size - 8; i++)
        {
            SetFunction(baseMatrix, function, 6, i, (i & 1) == 0);
            SetFunction(baseMatrix, function, i, 6, (i & 1) == 0);
        }
        ReserveFormat(function);
        SetFunction(baseMatrix, function, 8, Size - 8, true);

        int bitIndex = 0;
        bool upward = true;
        for (int right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int vert = 0; vert < Size; vert++)
            {
                int y = upward ? Size - 1 - vert : vert;
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    if (function[y, x]) continue;
                    int byteIndex = bitIndex >> 3;
                    bool dark = byteIndex < codewords.Length && ((codewords[byteIndex] >> (7 - (bitIndex & 7))) & 1) != 0;
                    baseMatrix[y, x] = dark;
                    bitIndex++;
                }
            }
            upward = !upward;
        }

        int bestPenalty = int.MaxValue;
        bool[,] best = null;
        for (int mask = 0; mask < 8; mask++)
        {
            bool[,] candidate = (bool[,])baseMatrix.Clone();
            ApplyMask(candidate, function, mask);
            DrawFormat(candidate, mask);
            int penalty = Penalty(candidate);
            if (penalty < bestPenalty) { bestPenalty = penalty; best = candidate; }
        }
        result = best;
        return true;
    }

    private static void AppendBits(List<bool> bits, int value, int count)
    {
        for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    private static byte[] AddErrorCorrection(byte[] data)
    {
        byte[] generator = Generator(ErrorCodewords);
        byte[] rem = new byte[ErrorCodewords];
        foreach (byte value in data)
        {
            byte factor = (byte)(value ^ rem[0]);
            for (int i = 0; i < rem.Length - 1; i++) rem[i] = rem[i + 1];
            rem[rem.Length - 1] = 0;
            for (int i = 0; i < rem.Length; i++) rem[i] ^= Multiply(generator[i], factor);
        }
        byte[] result = new byte[data.Length + rem.Length];
        data.CopyTo(result, 0);
        rem.CopyTo(result, data.Length);
        return result;
    }

    private static byte[] Generator(int degree)
    {
        byte[] result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < result.Length; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = Multiply(root, 0x02);
        }
        return result;
    }

    private static byte Multiply(byte x, byte y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ (((z >> 7) & 1) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    private static void DrawFinder(bool[,] m, bool[,] f, int x, int y)
    {
        for (int dy = -1; dy <= 7; dy++)
        for (int dx = -1; dx <= 7; dx++)
        {
            int xx = x + dx, yy = y + dy;
            if (xx < 0 || yy < 0 || xx >= Size || yy >= Size) continue;
            bool dark = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6 &&
                        (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
            SetFunction(m, f, xx, yy, dark);
        }
    }

    private static void DrawAlignment(bool[,] m, bool[,] f, int cx, int cy)
    {
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
            SetFunction(m, f, cx + dx, cy + dy, System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != 1);
    }

    private static void SetFunction(bool[,] m, bool[,] f, int x, int y, bool dark)
    {
        m[y, x] = dark;
        f[y, x] = true;
    }

    private static void ReserveFormat(bool[,] f)
    {
        for (int i = 0; i <= 5; i++) f[8, i] = true;
        f[8, 7] = true; f[8, 8] = true; f[7, 8] = true;
        for (int i = 9; i < 15; i++) f[14 - i, 8] = true;
        for (int i = 0; i < 8; i++) f[8, Size - 1 - i] = true;
        for (int i = 8; i < 15; i++) f[Size - 15 + i, 8] = true;
    }

    private static void DrawFormat(bool[,] m, int mask)
    {
        int data = (1 << 3) | mask; // Error correction level L has format bits 01.
        int rem = data;
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        int bits = ((data << 10) | rem) ^ 0x5412;
        for (int i = 0; i <= 5; i++) m[8, i] = GetBit(bits, i);
        m[8, 7] = GetBit(bits, 6); m[8, 8] = GetBit(bits, 7); m[7, 8] = GetBit(bits, 8);
        for (int i = 9; i < 15; i++) m[14 - i, 8] = GetBit(bits, i);
        for (int i = 0; i < 8; i++) m[8, Size - 1 - i] = GetBit(bits, i);
        for (int i = 8; i < 15; i++) m[Size - 15 + i, 8] = GetBit(bits, i);
        m[Size - 8, 8] = true;
    }

    private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

    private static void ApplyMask(bool[,] m, bool[,] f, int mask)
    {
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            if (f[y, x]) continue;
            bool invert;
            switch (mask)
            {
                case 0: invert = (x + y) % 2 == 0; break;
                case 1: invert = y % 2 == 0; break;
                case 2: invert = x % 3 == 0; break;
                case 3: invert = (x + y) % 3 == 0; break;
                case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
            }
            if (invert) m[y, x] = !m[y, x];
        }
    }

    private static int Penalty(bool[,] m)
    {
        int score = 0;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int same = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int xx = x + dx, yy = y + dy;
                if (xx >= 0 && xx < Size && yy >= 0 && yy < Size && m[y, x] == m[yy, xx]) same++;
            }
            if (same > 5) score += 3 + same - 5;
        }
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool dark = m[y, x];
            if (x + 5 < Size && dark == m[y, x + 1] && dark == m[y, x + 2] && dark == m[y, x + 3] && dark == m[y, x + 4] && dark == m[y, x + 5]) score += 3;
            if (y + 5 < Size && dark == m[y + 1, x] && dark == m[y + 2, x] && dark == m[y + 3, x] && dark == m[y + 4, x] && dark == m[y + 5, x]) score += 3;
        }
        return score;
    }
}

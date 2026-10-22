using System;
using CloverPitPatch;

internal static class TransitionTests
{
    public static void RunAll()
    {
        int[,] cases = {
            // forward
            { 1, 1, 2 },
            { 2, 1, 3 },
            { 3, 1, 4 },
            { 4, 1, 8 },
            { 8, 1, 16 },
            { 16, 1, 32 },
            { 32, 1, 64 },
            { 64, 1, 1 },
            // backward
            { 1, -1, 64 },
            { 2, -1, 1 },
            { 3, -1, 2 },
            { 4, -1, 3 },
            { 8, -1, 4 },
            { 16, -1, 8 },
            { 32, -1, 16 },
            { 64, -1, 32 },
            // no op
            { 4, 0, 4 },
            { 64, 0, 64 },
            { 5, 0, 5 },
            // unsupported and extreme
            { 5, 1, 8 },
            { 5, -1, 4 },
            { 0, 1, 1 },
            { 0, -1, 64 },
            { 100, 1, 1 },
            { 100, -1, 64 },
            { int.MaxValue, 1, 1 },
            { int.MinValue, -1, 64 },
            { 4, int.MaxValue, 8 },
            { 4, int.MinValue, 3 },
        };
        for (int i = 0; i < cases.GetLength(0); i++)
        {
            int actual = TransitionSpeeds.Next(cases[i, 0], cases[i, 1]);
            if (actual != cases[i, 2])
                throw new Exception("Transition case " + i + ": expected " + cases[i, 2] + ", got " + actual);
        }
        Console.WriteLine("PASS: " + cases.GetLength(0) + " transition cases covering forward/backward wrap, no-op, unsupported values and integer extremes.");
    }
}

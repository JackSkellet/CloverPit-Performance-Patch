using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using CloverPitPerformancePatch;

internal static class PatternTests
{
    static int cases;
    // Independent executable specification: a bounded rotation pass followed
    // by ascending ordered compare-swaps. This models observable record order;
    // it is not a copied or executed game method.
    static void ReferenceOrder(List<SlotMachineScript.PatternInfos> pool)
    {
        var records = pool.ToArray();
        int head = 0;
        for (int consumed = 1; consumed < records.Length; consumed++)
        {
            if (records[head].enabled) { head++; continue; }
            var removed = records[head];
            Array.Copy(records, head + 1, records, head, records.Length - head - 1);
            records[records.Length - 1] = removed;
        }
        for (int left = 0; left < records.Length; left++)
        {
            for (int right = left + 1; right < records.Length; right++)
            {
                ulong a = records[left].GetOrderWeight();
                ulong b = records[right].GetOrderWeight();
                bool rightFirst = records[right].enabled &&
                    (!records[left].enabled || a > b ||
                     (a == b && records[left].coins.CompareTo(records[right].coins) > 0));
                if (rightFirst)
                    (records[left], records[right]) = (records[right], records[left]);
            }
        }
        for (int i = 0; i < records.Length; i++) pool[i] = records[i];
    }
    static void Check(List<SlotMachineScript.PatternInfos> original)
    {
        var left = new List<SlotMachineScript.PatternInfos>(original);
        var right = new List<SlotMachineScript.PatternInfos>(original);
        ReferenceOrder(left); PatternAlgorithms.CompactAndSort(right);
        if (left.Count != right.Count) throw new Exception("Count mismatch");
        for (int i = 0; i < left.Count; i++)
            if (!Object.ReferenceEquals(left[i], right[i]))
                throw new Exception("Reference-order mismatch in case " + cases + " at " + i);
        var scratch = (SlotMachineScript.PatternInfos[])typeof(PatternAlgorithms).GetField("scratch", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (var item in scratch) if (item != null) throw new Exception("Scratch retained record");
        cases++;
    }
    public static void RunAll()
    {
        cases = 0;
        for (int n = 0, max = 1; n <= 6; n++, max *= 6)
            for (int value = 0; value < max; value++)
            {
                int code = value; var pool = new List<SlotMachineScript.PatternInfos>();
                for (int i = 0; i < n; i++)
                {
                    int state = code % 6; code /= 6;
                    pool.Add(new SlotMachineScript.PatternInfos { id = i, enabled = state / 3 != 0, weight = (ulong)(state % 3), coins = state % 2 });
                }
                Check(pool);
            }
        var random = new Random(3314790);
        for (int i = 0; i < 5000; i++)
        {
            int count = random.Next(100); var pool = new List<SlotMachineScript.PatternInfos>();
            for (int j = 0; j < count; j++) pool.Add(new SlotMachineScript.PatternInfos { id = j, enabled = random.Next(2) == 1, weight = (ulong)random.Next(7), coins = j % 3 == 0 ? BigInteger.Pow(10, 100) + random.Next(3) : random.Next(-10, 11) });
            Check(pool);
            // Reuse the same objects with changed state. The helper must not cache
            // old enabled flags, weights or scores between calls.
            if (i < 100)
            {
                foreach (var item in pool) { item.enabled = !item.enabled; item.weight = 8 - item.weight; item.coins = -item.coins; }
                Check(pool);
            }
        }
        var highWater = new List<SlotMachineScript.PatternInfos>();
        for (int i = 0; i < 4096; i++) highWater.Add(new SlotMachineScript.PatternInfos { id = i, enabled = i % 128 == 0, weight = (ulong)(i % 7), coins = i % 11 });
        var expected = new List<SlotMachineScript.PatternInfos>(highWater);
        SlotMachineScript.PatternInfos.WeightCalls = 0; ReferenceOrder(expected); long before = SlotMachineScript.PatternInfos.WeightCalls;
        SlotMachineScript.PatternInfos.WeightCalls = 0; PatternAlgorithms.CompactAndSort(highWater); long after = SlotMachineScript.PatternInfos.WeightCalls;
        for (int i = 0; i < expected.Count; i++) if (!Object.ReferenceEquals(expected[i], highWater[i])) throw new Exception("High-water order mismatch");
        if (after != 32) throw new Exception("Incorrect active-only weight count");
        Console.WriteLine("PASS: " + cases + " exact-reference differential cases plus 4096-record high-water case.");
        Console.WriteLine("High-water GetOrderWeight calls: original=" + before + ", patched=" + after + ". This is algorithm work, not an in-game FPS measurement.");

    }
}

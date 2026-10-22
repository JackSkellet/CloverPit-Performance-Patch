using System;
using System.Collections.Generic;

namespace CloverPitPerformancePatch
{
    // Invoked synchronously by the game's main-thread scoring coroutine.
    // Scratch storage contains no retained PatternInfos references between calls.
    public static class PatternAlgorithms
    {
        private static SlotMachineScript.PatternInfos[] scratch = new SlotMachineScript.PatternInfos[0];
        private static ulong[] weights = new ulong[0];

        public static void CompactAndSort(List<SlotMachineScript.PatternInfos> pool)
        {
            int count = pool.Count;
            if (count < 2)
                return;

            if (scratch.Length < count)
            {
                int capacity = Math.Max(count, Math.Max(8, scratch.Length * 2));
                scratch = new SlotMachineScript.PatternInfos[capacity];
                weights = new ulong[capacity];
            }

            int write = 0;
            for (int i = 0; i < count; i++)
                if (pool[i].enabled)
                    scratch[write++] = pool[i];
            int activeCount = write;

            // The original RemoveAt/Add loop processes exactly count-1 original
            // records. An original final disabled record therefore precedes the
            // other disabled records, rather than following them.
            if (!pool[count - 1].enabled)
                scratch[write++] = pool[count - 1];
            for (int i = 0; i < count - 1; i++)
                if (!pool[i].enabled)
                    scratch[write++] = pool[i];

            for (int i = 0; i < count; i++)
            {
                pool[i] = scratch[i];
                scratch[i] = null;
            }

            if (activeCount < 2)
                return;
            for (int i = 0; i < activeCount; i++)
                weights[i] = pool[i].GetOrderWeight();

            // Preserve the original exchange-sort permutation, including ties.
            // A standard List.Sort or stable sort changes equal-key record order.
            for (int i = 0; i < activeCount; i++)
            {
                for (int j = i + 1; j < activeCount; j++)
                {
                    ulong leftWeight = weights[i];
                    ulong rightWeight = weights[j];
                    if (leftWeight > rightWeight ||
                        (leftWeight == rightWeight && pool[i].coins > pool[j].coins))
                    {
                        SlotMachineScript.PatternInfos item = pool[i];
                        pool[i] = pool[j];
                        pool[j] = item;
                        weights[i] = rightWeight;
                        weights[j] = leftWeight;
                    }
                }
            }
        }
    }
}

// Minimal record shape required by the authored pattern helper.
using System.Numerics;

public class SlotMachineScript
{
    public class PatternInfos
    {
        public int id;
        public bool enabled;
        public ulong weight;
        public BigInteger coins;
        public static long WeightCalls;
        public ulong GetOrderWeight() { WeightCalls++; return weight; }
    }
}

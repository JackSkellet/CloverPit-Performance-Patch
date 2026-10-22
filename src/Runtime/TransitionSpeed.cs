namespace CloverPitPatch
{
    public static class TransitionSpeeds
    {
        private static readonly int[] Values = { 1, 2, 3, 4, 8, 16, 32, 64 };

        public static int Next(int current, int direction)
        {
            if (direction == 0)
                return current;

            // Selecting the nearest value also repairs unsupported saved speeds
            // without arithmetic overflow or skipping a supported setting.
            if (direction > 0)
            {
                for (int i = 0; i < Values.Length; i++)
                    if (Values[i] > current)
                        return Values[i];
                return Values[0];
            }

            for (int i = Values.Length - 1; i >= 0; i--)
                if (Values[i] < current)
                    return Values[i];
            return Values[Values.Length - 1];
        }
    }
}

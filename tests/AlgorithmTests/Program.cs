using System;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("This test runner takes no arguments.");
            return 2;
        }
        try
        {
            TransitionTests.RunAll();
            PatternTests.RunAll();
            OpacityTests.RunAll();
            Console.WriteLine("All algorithm tests passed. No game installation was loaded or modified.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}

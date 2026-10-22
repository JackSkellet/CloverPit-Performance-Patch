using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

internal static class Program
{
    internal const string OriginalHash = "7e904219b9c26bb58efd9170d13dfb5070bd848add6d2d117223910284a7498e";
    internal const string PatchedHash = "8508cde02362bcc9ffe566a6b5fca9457e8f53d284f91c570e5c95a3f034040a";
    internal const string RuntimeHash = "4e10da52074c8f5e0e7293e297365816b1cc1dbd1baab3659fcafab45302e283";
    private const string BackupName = "CloverPitPerformancePatch-backup";

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--version" or "version")
        { Console.WriteLine("CloverPit Performance Patch 2.0.0"); return 0; }
        if (args.Length == 0 || args[0] is "--help" or "help")
        {
            Console.WriteLine("CloverPitPatch install <game-directory>\nCloverPitPatch verify <game-directory>\nCloverPitPatch restore <game-directory>\n\nFor Steam build 22785177. Save and close CloverPit before install/restore.");
            return 0;
        }
        try
        {
            if (args.Length != 2 || args[0] is not ("install" or "verify" or "restore"))
                throw new InvalidOperationException("Use --help for commands.");
            string game = Path.GetFullPath(args[1]);
            string managed = Path.Combine(game, "CloverPit_Data", "Managed");
            string assembly = Path.Combine(managed, "Assembly-CSharp.dll");
            string helper = Path.Combine(managed, "CloverPitPatch.dll");
            if (!File.Exists(assembly)) throw new InvalidOperationException("Choose the CloverPit game folder containing CloverPit_Data.");
            if (args[0] == "verify") return Verify(assembly, helper);
            EnsureClosed();
            if (args[0] == "restore") Restore(game, assembly, helper);
            else Install(game, managed, assembly, helper);
            return 0;
        }
        catch (Exception e)
        { Console.Error.WriteLine("Error: " + e.Message); return 1; }
    }

    internal static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
    private static void Expect(string file, string hash)
    {
        if (!File.Exists(file) || Hash(file) != hash)
            throw new InvalidOperationException("Unsupported or modified file: " + file + ". No forced patching is supported.");
    }
    private static void EnsureClosed()
    {
        foreach (string name in new[] { "CloverPit", "CloverPit.exe" })
        {
            var processes = Process.GetProcessesByName(name);
            bool found = processes.Length > 0;
            foreach (var process in processes) process.Dispose();
            if (found) throw new InvalidOperationException("Save and close CloverPit first.");
        }
    }

    private static int Verify(string assembly, string helper)
    {
        string hash = Hash(assembly);
        if (hash == PatchedHash)
        {
            Expect(helper, RuntimeHash);
            Console.WriteLine("Combined patch 2.0.0 verified. All performance fixes and 1x–64x options are installed.");
            return 0;
        }
        if (hash == OriginalHash && !File.Exists(helper))
        { Console.WriteLine("Supported unmodified game; patch is not installed."); return 0; }
        throw new InvalidOperationException("This is not the supported original or complete combined patch. Another game version or mod may be installed.");
    }
    private static void Replace(string source, string destination)
    {
        string temp = destination + ".cloverpatch-new";
        File.Copy(source, temp, true);
        File.Move(temp, destination, true);
    }
    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        foreach (string directory in Directory.EnumerateDirectories(source))
            CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
    private static void Install(string game, string managed, string assembly, string helper)
    {
        if (Hash(assembly) == PatchedHash) { Verify(assembly, helper); Console.WriteLine("Already installed; nothing changed."); return; }
        Expect(assembly, OriginalHash);
        if (File.Exists(helper)) throw new InvalidOperationException("An existing CloverPitPatch.dll was found. Restore the earlier installation before continuing.");
        string stage = Path.Combine(Path.GetTempPath(), "cloverpit-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            string runtime = Path.Combine(stage, "CloverPitPatch.dll");
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("CloverPitPatch.Runtime.dll") ?? throw new InvalidOperationException("Missing runtime resource"))
            using (Stream output = File.Create(runtime)) input.CopyTo(output);
            Expect(runtime, RuntimeHash);
            string intermediate = Path.Combine(stage, "stage-one.dll"), final = Path.Combine(stage, "Assembly-CSharp.dll");
            StageOne.Apply(new[] { assembly, runtime, intermediate, managed });
            StageTwo.Apply(new[] { intermediate, runtime, final, managed });
            Expect(final, PatchedHash);
            string backup = Path.Combine(game, BackupName), original = Path.Combine(backup, "Assembly-CSharp.dll");
            if (Directory.Exists(backup)) Expect(original, OriginalHash);
            else
            {
                Directory.CreateDirectory(backup);
                File.Copy(assembly, original);
                if (Directory.Exists(Path.Combine(game, "SaveData"))) CopyTree(Path.Combine(game, "SaveData"), Path.Combine(backup, "SaveData"));
            }
            Expect(original, OriginalHash);
            // Recheck immediately before replacing the files.
            EnsureClosed(); Expect(assembly, OriginalHash);
            if (File.Exists(helper)) throw new InvalidOperationException("A companion appeared during preparation; nothing installed.");
            try
            {
                Replace(runtime, helper);
                Replace(final, assembly);
                Expect(assembly, PatchedHash); Expect(helper, RuntimeHash);
            }
            catch
            {
                Replace(original, assembly);
                if (File.Exists(helper) && Hash(helper) == RuntimeHash) File.Delete(helper);
                throw;
            }
            Console.WriteLine("Combined patch installed and verified. Backups: " + backup);
            Console.WriteLine("Current saves, saved options, boot.config and Unity libraries were preserved.");
        }
        finally { Directory.Delete(stage, true); }
    }
    private static void Restore(string game, string assembly, string helper)
    {
        if (Hash(assembly) == OriginalHash && !File.Exists(helper))
        { Console.WriteLine("Already restored; nothing changed."); return; }
        Expect(assembly, PatchedHash); Expect(helper, RuntimeHash);
        string original = new[] { Path.Combine(game, BackupName, "Assembly-CSharp.dll"), Path.Combine(game, "CloverPitPatch-backup-v1", "Assembly-CSharp.dll") }
            .FirstOrDefault(p => File.Exists(p) && Hash(p) == OriginalHash)
            ?? throw new InvalidOperationException("No verified original backup found. Steam file verification can restore the game files.");
        Replace(original, assembly);
        File.Delete(helper);
        Expect(assembly, OriginalHash);
        Console.WriteLine("Unmodified game assembly restored. Current save progress and boot.config were preserved.");
    }
}

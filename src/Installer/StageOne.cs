using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
internal static class StageOne
{
    const string OriginalHash = "7e904219b9c26bb58efd9170d13dfb5070bd848add6d2d117223910284a7498e";
    static IEnumerable<TypeDefinition> Types(TypeDefinition t) { yield return t; foreach (var n in t.NestedTypes) foreach (var x in Types(n)) yield return x; }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static void Nop(Instruction i) { i.OpCode = OpCodes.Nop; i.Operand = null; }
    static void ExpandBranches(MethodDefinition m)
    {
        var ops = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Name);
        foreach (var i in m.Body.Instructions) if (i.OpCode.OperandType == OperandType.ShortInlineBrTarget) i.OpCode = ops[i.OpCode.Name.Substring(0, i.OpCode.Name.Length - 2)];
    }
    static void Cache(MethodDefinition m, FieldDefinition data, FieldDefinition cache)
    {
        var il = m.Body.GetILProcessor(); var first = m.Body.Instructions[0]; var oldReturns = m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        // Preserve incoming branches to each original return by transforming that
        // instruction itself into the first cache-store instruction.
        foreach (var ret in oldReturns)
        {
            ret.OpCode = OpCodes.Ldarg_0; ret.Operand = null; var last = ret;
            foreach (var x in new[] { il.Create(OpCodes.Ldarg_0), il.Create(OpCodes.Ldfld, data), il.Create(OpCodes.Stfld, cache), il.Create(OpCodes.Ret) }) { il.InsertAfter(last, x); last = x; }
        }
        foreach (var x in new[] { il.Create(OpCodes.Ldarg_0), il.Create(OpCodes.Ldfld, cache), il.Create(OpCodes.Brfalse, first), il.Create(OpCodes.Ldarg_0), il.Create(OpCodes.Ldfld, cache), il.Create(OpCodes.Ldarg_0), il.Create(OpCodes.Ldfld, data), il.Create(OpCodes.Bne_Un, first), il.Create(OpCodes.Ret) }) il.InsertBefore(first, x);
        ExpandBranches(m);
    }
    static void Invalidate(MethodDefinition m, List<FieldDefinition> fields)
    {
        Require(!m.IsStatic, m.Name + " must be instance method"); var il = m.Body.GetILProcessor(); var first = m.Body.Instructions[0];
        foreach (var f in fields) foreach (var x in new[] { il.Create(OpCodes.Ldarg_0), il.Create(OpCodes.Ldnull), il.Create(OpCodes.Stfld, f) }) il.InsertBefore(first, x);
        ExpandBranches(m);
    }
    static void Speed(MethodDefinition m, int addOffset, int removeStart, int removeEnd, MethodReference next)
    {
        var ins = m.Body.Instructions; var add = ins.Single(i => i.Offset == addOffset); Require(add.OpCode == OpCodes.Add, "speed add changed"); add.OpCode = OpCodes.Call; add.Operand = next;
        foreach (var i in ins.Where(i => i.Offset >= removeStart && i.Offset < removeEnd)) Nop(i); ExpandBranches(m);
    }
    public static void Apply(string[] args)
    {
        Require(args.Length == 4, "Patcher original.dll helper.dll output.dll game-managed-directory"); var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))).ToLowerInvariant(); Require(hash == OriginalHash, "Unsupported game assembly hash: " + hash);
        using var resolver = new DefaultAssemblyResolver(); resolver.AddSearchDirectory(args[3]); resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))); using var asm = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }); using var helper = AssemblyDefinition.ReadAssembly(args[1], new ReaderParameters { AssemblyResolver = resolver }); var module = asm.MainModule; var types = module.Types.SelectMany(Types).ToList();
        var gameplay = types.Single(t => t.FullName == "GameplayData"); var caches = new List<FieldDefinition>();
        foreach (var item in new[] { ("symbolsData", "_EnsureSymbolsArray"), ("patternsData", "_EnsurePatternsArray"), ("powerupsData", "_EnsurePowerupDataArray") })
        {
            var data = gameplay.Fields.Single(f => f.Name == item.Item1); var cache = new FieldDefinition("__cpValidated_" + item.Item1, FieldAttributes.Private | FieldAttributes.NotSerialized, data.FieldType); gameplay.Fields.Add(cache); caches.Add(cache);
            Cache(gameplay.Methods.Single(m => m.Name == item.Item2), data, cache);
        }
        foreach (var name in new[] { "Save_Prepare", "Load_Format" }) Invalidate(gameplay.Methods.Single(m => m.Name == name), caches);
        var next = module.ImportReference(helper.MainModule.Types.Single(t => t.FullName == "CloverPitPatch.TransitionSpeeds").Methods.Single(m => m.Name == "Next"));
        Speed(types.Single(t => t.Name == "MainMenuScript").Methods.Single(m => m.Name == "MFunc_TransitionSpeed"), 0x21, 0x27, 0x59, next);
        Speed(types.Single(t => t.Name == "ConsolePrompt").Methods.Single(m => m.Name == "<CommandsGenerate>b__32_16"), 0xc, 0x12, 0x2a, next);
        var slot = types.Single(t => t.FullName == "SlotMachineScript"); var pool = slot.Fields.Single(f => f.Name == "_patternInfos");
        var compute = slot.NestedTypes.Single(t => t.Name == "<PatternsCompute_Coroutine>d__178").Methods.Single(m => m.Name == "MoveNext");
        var instructions = compute.Body.Instructions; var replaced = instructions.Where(i => i.Offset >= 0x696 && i.Offset < 0x7f9).ToArray(); Require(replaced.Length > 50, "sort block missing");
        Require(replaced.Count(i => i.Operand is MethodReference mr && mr.Name == "GetOrderWeight") == 2, "sort weight calls changed");
        var region = new HashSet<Instruction>(replaced);
        foreach (var i in instructions.Where(i => !region.Contains(i)))
        {
            if (i.Operand is Instruction target) Require(!region.Contains(target) || target == replaced[0], "External branch enters replacement interior");
            if (i.Operand is Instruction[] targets) Require(targets.All(t => !region.Contains(t) || t == replaced[0]), "External switch enters replacement interior");
        }
        foreach (var i in replaced) Nop(i);
        replaced[0].OpCode = OpCodes.Ldloc_1; replaced[1].OpCode = OpCodes.Ldfld; replaced[1].Operand = pool;
        replaced[2].OpCode = OpCodes.Call; replaced[2].Operand = module.ImportReference(helper.MainModule.Types.Single(t => t.FullName == "CloverPitPerformancePatch.PatternAlgorithms").Methods.Single(m => m.Name == "CompactAndSort")); ExpandBranches(compute);
        var log = types.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MethodReference>().First(m => m.FullName == "System.Void UnityEngine.Debug::Log(System.Object)");
        var awake = types.Single(t => t.Name == "GameplayMaster").Methods.Single(m => m.Name == "Awake"); var ai = awake.Body.GetILProcessor(); var af = awake.Body.Instructions[0]; ai.InsertBefore(af, ai.Create(OpCodes.Ldstr, "[CloverPitPatch] 1.0 loaded: cached data validation, exact pattern ordering, transition speeds through 64x.")); ai.InsertBefore(af, ai.Create(OpCodes.Call, log)); ExpandBranches(awake);
        module.Mvid = new Guid("a0bda1df-b3c6-44a4-b956-6d17cd158b10"); asm.Write(args[2]); Console.WriteLine("Patched: " + args[2]); Console.WriteLine("SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))).ToLowerInvariant());
    }
}

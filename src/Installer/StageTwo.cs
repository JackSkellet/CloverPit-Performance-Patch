using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
internal static class StageTwo
{
    static IEnumerable<TypeDefinition> Types(TypeDefinition t) { yield return t; foreach (var n in t.NestedTypes) foreach (var x in Types(n)) yield return x; }
    static string Body(MethodDefinition m) { if (!m.HasBody) return ""; var ins = m.Body.Instructions; return string.Join("\n", ins.Select(i => i.OpCode.Code + " " + (i.Operand is Instruction target ? "label:" + ins.IndexOf(target) : i.Operand is Instruction[] targets ? string.Join(",", targets.Select(x => ins.IndexOf(x))) : i.Operand is VariableDefinition v ? "local:" + v.Index : i.Operand is ParameterDefinition p ? "arg:" + p.Index : i.Operand?.ToString()))); }
    public static void Apply(string[] a)
    {
        if (a.Length != 4) throw new Exception("Patcher2 v1.dll helper.dll output.dll managed-dir"); if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a[0]))).ToLowerInvariant() != "012252f4ac421044fc5c98013237fa4ca5a6fccf76c3487b9c4a343b0dfb34df") throw new Exception("Expected exact v1 assembly"); using var resolver = new DefaultAssemblyResolver(); resolver.AddSearchDirectory(a[3]); resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(a[1])));
        using var asm = AssemblyDefinition.ReadAssembly(a[0], new ReaderParameters { AssemblyResolver = resolver }); using var helper = AssemblyDefinition.ReadAssembly(a[1], new ReaderParameters { AssemblyResolver = resolver }); var types = asm.MainModule.Types.SelectMany(Types).ToArray(); var before = types.SelectMany(t => t.Methods).ToDictionary(m => m.FullName, Body);
        CarouselPatcher.Apply(asm.MainModule);
        var crt = types.Single(t => t.Name == "CrtTextEffect").Methods.Single(m => m.Name == "Update"); var setter = crt.Body.Instructions.Single(i => i.Operand is MethodReference mr && mr.DeclaringType.FullName == "TMPro.TMP_Text" && mr.Name == "set_alpha"); setter.OpCode = OpCodes.Call; setter.Operand = asm.MainModule.ImportReference(helper.MainModule.Types.Single(t => t.FullName == "CloverPitPatch.CrtOpacity").Methods.Single(m => m.Name == "Apply"));
        var awake = types.Single(t => t.Name == "GameplayMaster").Methods.Single(m => m.Name == "Awake"); var message = awake.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr && ((string)i.Operand).StartsWith("[CloverPitPatch] 1.0 loaded")); message.Operand = "[CloverPitPatch] 2.0 loaded: v1 fixes and 64x retained; plain-text opacity and hidden carousel optimized.";
        var allowed = new HashSet<string> { crt.FullName, awake.FullName }; foreach (var m in types.SelectMany(t => t.Methods)) if ((m.DeclaringType.Name == "SurgeryMachine" && (m.Name == "Update" || m.Name == "Open")) || (m.DeclaringType.Name == "<SurgeryInProgress>d__38" && m.Name == "MoveNext")) allowed.Add(m.FullName);
        var changed = types.SelectMany(t => t.Methods).Where(m => before.ContainsKey(m.FullName) && before[m.FullName] != Body(m)).Select(m => m.FullName).ToArray(); if (changed.Any(x => !allowed.Contains(x))) throw new Exception("Unexpected modified method");
        asm.MainModule.Mvid = new Guid("541e15ef-e6a6-4a02-b4d6-218986a4c263"); asm.Write(a[2]); Console.WriteLine("Existing method bodies changed: " + changed.Length); foreach (var m in changed) Console.WriteLine(m); Console.WriteLine("All other existing methods preserved, including v1 cache/scoring/speed fixes."); Console.WriteLine("SHA256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a[2]))).ToLowerInvariant());
    }
}

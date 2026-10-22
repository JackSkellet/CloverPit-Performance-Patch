using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class CarouselPatcher
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Carousel patch: " + message);
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes)
            foreach (var child in AllTypes(nested)) yield return child;
    }

    private static void ExpandBranches(MethodDefinition method)
    {
        var byName = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)).ToDictionary(op => op.Name);
        foreach (var instruction in method.Body.Instructions)
            if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                instruction.OpCode = byName[instruction.OpCode.Name.Substring(0, instruction.OpCode.Name.Length - 2)];
    }

    private static void Nop(Instruction instruction)
    {
        instruction.OpCode = OpCodes.Nop;
        instruction.Operand = null;
    }

    private static MethodDefinition NewMethod(TypeDefinition owner, string name, ModuleDefinition module)
    {
        var method = new MethodDefinition(name,
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        method.Body = new Mono.Cecil.Cil.MethodBody(method) { InitLocals = true, MaxStackSize = 8 };
        owner.Methods.Add(method);
        return method;
    }

    private static void FlushBeforeActivation(MethodDefinition method, int offset,
        FieldDefinition instance, FieldDefinition holder, MethodDefinition flush)
    {
        var anchor = method.Body.Instructions.Single(i => i.Offset == offset);
        Require(anchor.OpCode == OpCodes.Ldsfld && ((FieldReference)anchor.Operand).FullName == instance.FullName,
            method.FullName + " activation instance load changed");
        Require(anchor.Next.OpCode == OpCodes.Ldfld && ((FieldReference)anchor.Next.Operand).FullName == holder.FullName,
            method.FullName + " activation holder changed");
        Require(anchor.Next.Next.OpCode == OpCodes.Ldc_I4_1 &&
            anchor.Next.Next.Next.Operand is MethodReference &&
            ((MethodReference)anchor.Next.Next.Next.Operand).Name == "SetActive",
            method.FullName + " activation no longer sets true");
        // Retain the original anchor so an incoming branch also executes flush.
        // Its instance is consumed by flush, then restored for the original code.
        var il = method.Body.GetILProcessor();
        var call = il.Create(OpCodes.Call, flush);
        il.InsertAfter(anchor, call);
        il.InsertAfter(call, il.Create(OpCodes.Ldsfld, instance));
        ExpandBranches(method);
    }

    public static void Apply(ModuleDefinition module)
    {
        var allTypes = module.Types.SelectMany(AllTypes).ToArray();
        var owner = allTypes.Single(t => t.FullName == "SurgeryMachine");
        Require(!owner.Fields.Any(f => f.Name == "__cpPendingCarouselBlend") &&
            !owner.Methods.Any(m => m.Name.StartsWith("__cp", StringComparison.Ordinal) && m.Name.Contains("Carousel")),
            "carousel patch already present or helper metadata collides");
        var update = owner.Methods.Single(m => m.Name == "Update" && m.Parameters.Count == 0);
        var open = owner.Methods.Single(m => m.Name == "Open" && m.Parameters.Count == 0);
        var surgery = owner.NestedTypes.Single(t => t.Name == "<SurgeryInProgress>d__38")
            .Methods.Single(m => m.Name == "MoveNext");
        var holder = owner.Fields.Single(f => f.Name == "surgeryUIHolder");
        var instance = owner.Fields.Single(f => f.Name == "instance");
        var region = update.Body.Instructions.Where(i => i.Offset >= 0x148 && i.Offset < 0x259).ToArray();
        Require(region.Length > 40 && region[0].Offset == 0x148 && region.Last().Next.Offset == 0x259,
            "original carousel region changed");
        Require(region.Count(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "Lerp") == 2,
            "expected Vector2 and Vector3 interpolation calls");
        Require(region.Count(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "CharmScaleMult") == 1,
            "expected original per-charm scale multiplier");
        var regionSet = new HashSet<Instruction>(region);
        foreach (var instruction in update.Body.Instructions.Where(i => !regionSet.Contains(i)))
        {
            if (instruction.Operand is Instruction)
            {
                var target = (Instruction)instruction.Operand;
                Require(!regionSet.Contains(target) || target == region[0], "outside branch enters carousel interior");
            }
            if (instruction.Operand is Instruction[])
                Require(((Instruction[])instruction.Operand).All(t => !regionSet.Contains(t) || t == region[0]),
                    "outside switch enters carousel interior");
        }
        Require(update.Body.ExceptionHandlers.Count == 0, "Update now has exception regions; re-evaluate extraction");
        var references = allTypes.SelectMany(t => t.Methods).Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
        var time = references.First(m => m.DeclaringType.FullName == "Panik.Tick" && m.Name == "get_Time" && m.Parameters.Count == 0);
        var clamp = references.First(m => m.DeclaringType.FullName == "UnityEngine.Mathf" && m.Name == "Clamp01" && m.Parameters.Count == 1);
        var active = references.First(m => m.DeclaringType.FullName == "UnityEngine.GameObject" && m.Name == "get_activeSelf" && m.Parameters.Count == 0);

        var pending = new FieldDefinition("__cpPendingCarouselBlend",
            Mono.Cecil.FieldAttributes.Private | Mono.Cecil.FieldAttributes.NotSerialized,
            module.TypeSystem.Double);
        owner.Fields.Add(pending);
        var step = NewMethod(owner, "__cpCarouselStep", module);
        step.Parameters.Add(new ParameterDefinition("amount", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Single));
        // Keeping the same local indexes also preserves implicit ldloc/stloc forms.
        foreach (var variable in update.Body.Variables)
            step.Body.Variables.Add(new VariableDefinition(variable.VariableType));
        var clones = new Dictionary<Instruction, Instruction>();
        foreach (var instruction in region)
        {
            var clone = Instruction.Create(OpCodes.Nop);
            clone.OpCode = instruction.OpCode;
            clone.Operand = instruction.Operand;
            clones.Add(instruction, clone);
            step.Body.Instructions.Add(clone);
        }
        foreach (var instruction in region)
        {
            var clone = clones[instruction];
            if (instruction.Operand is Instruction)
            {
                var target = (Instruction)instruction.Operand;
                Require(clones.ContainsKey(target), "extracted step branches outside region");
                clone.Operand = clones[target];
            }
            else if (instruction.Operand is Instruction[])
            {
                var targets = (Instruction[])instruction.Operand;
                Require(targets.All(clones.ContainsKey), "extracted step switch leaves region");
                clone.Operand = targets.Select(t => clones[t]).ToArray();
            }
            else if (instruction.Operand is VariableDefinition)
                clone.Operand = step.Body.Variables[((VariableDefinition)instruction.Operand).Index];
            else if (instruction.Operand is ParameterDefinition)
                throw new InvalidOperationException("Unexpected explicit source parameter in parameterless Update");
        }
        foreach (int offset in new[] { 0x18b, 0x22f })
        {
            var start = region.Single(i => i.Offset == offset);
            Require(start.OpCode == OpCodes.Call && ((MethodReference)start.Operand).FullName == time.FullName &&
                start.Next.OpCode == OpCodes.Ldc_R4 && (float)start.Next.Operand == 20f && start.Next.Next.OpCode == OpCodes.Mul,
                "original smoothing delta changed");
            clones[start].OpCode = OpCodes.Ldarg_1;
            clones[start].Operand = null;
            Nop(clones[start.Next]);
            Nop(clones[start.Next.Next]);
        }
        step.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        ExpandBranches(step);

        var flush = NewMethod(owner, "__cpFlushCarousel", module);
        var fi = flush.Body.GetILProcessor();
        var flushReturn = fi.Create(OpCodes.Ret);
        fi.Append(fi.Create(OpCodes.Ldarg_0)); fi.Append(fi.Create(OpCodes.Ldfld, pending));
        fi.Append(fi.Create(OpCodes.Ldc_R8, 0.0)); fi.Append(fi.Create(OpCodes.Beq, flushReturn));
        fi.Append(fi.Create(OpCodes.Ldarg_0)); fi.Append(fi.Create(OpCodes.Ldarg_0));
        fi.Append(fi.Create(OpCodes.Ldfld, pending)); fi.Append(fi.Create(OpCodes.Conv_R4));
        fi.Append(fi.Create(OpCodes.Call, step));
        fi.Append(fi.Create(OpCodes.Ldarg_0)); fi.Append(fi.Create(OpCodes.Ldc_R8, 0.0));
        fi.Append(fi.Create(OpCodes.Stfld, pending)); fi.Append(flushReturn);

        var tick = NewMethod(owner, "__cpTickCarousel", module);
        tick.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Single));
        var ti = tick.Body.GetILProcessor();
        var visible = ti.Create(OpCodes.Ldarg_0);
        ti.Append(ti.Create(OpCodes.Call, time)); ti.Append(ti.Create(OpCodes.Ldc_R4, 20f));
        ti.Append(ti.Create(OpCodes.Mul)); ti.Append(ti.Create(OpCodes.Call, clamp)); ti.Append(ti.Create(OpCodes.Stloc_0));
        ti.Append(ti.Create(OpCodes.Ldarg_0)); ti.Append(ti.Create(OpCodes.Ldfld, holder));
        ti.Append(ti.Create(OpCodes.Callvirt, active)); ti.Append(ti.Create(OpCodes.Brtrue, visible));
        ti.Append(ti.Create(OpCodes.Ldarg_0)); // field-store receiver
        ti.Append(ti.Create(OpCodes.Ldarg_0)); ti.Append(ti.Create(OpCodes.Ldfld, pending)); // p
        ti.Append(ti.Create(OpCodes.Ldc_R8, 1.0));
        ti.Append(ti.Create(OpCodes.Ldarg_0)); ti.Append(ti.Create(OpCodes.Ldfld, pending));
        ti.Append(ti.Create(OpCodes.Sub)); // 1-p
        ti.Append(ti.Create(OpCodes.Ldloc_0)); ti.Append(ti.Create(OpCodes.Conv_R8));
        ti.Append(ti.Create(OpCodes.Mul)); ti.Append(ti.Create(OpCodes.Add));
        ti.Append(ti.Create(OpCodes.Stfld, pending)); ti.Append(ti.Create(OpCodes.Ret));
        ti.Append(visible); ti.Append(ti.Create(OpCodes.Call, flush));
        ti.Append(ti.Create(OpCodes.Ldarg_0)); ti.Append(ti.Create(OpCodes.Ldloc_0));
        ti.Append(ti.Create(OpCodes.Call, step)); ti.Append(ti.Create(OpCodes.Ret));

        foreach (var instruction in region) Nop(instruction);
        region[0].OpCode = OpCodes.Ldarg_0;
        region[1].OpCode = OpCodes.Call; region[1].Operand = tick;
        ExpandBranches(update);
        FlushBeforeActivation(open, 0x38, instance, holder, flush);
        FlushBeforeActivation(surgery, 0x736, instance, holder, flush);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using NitroxClient.MonoBehaviours;
using NitroxModel.Core;
using NitroxModel.Helper;
using NitroxPatcher.PatternMatching;

namespace NitroxPatcher;

internal static class TranspilerHelper
{
    private static readonly MethodInfo serviceLocator = typeof(NitroxServiceLocator)
        .GetMethod(nameof(NitroxServiceLocator.LocateService), BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);

    public static CodeInstruction LocateService<T>()
    {
        return new CodeInstruction(OpCodes.Call, serviceLocator.MakeGenericMethod(typeof(T)));
    }

    private static IEnumerable<LocalVariableInfo> GetMatchingLocalVariables<T>(MethodBase method)
    {
        return method.GetMethodBody()?.LocalVariables.Where(v => v.LocalType == typeof(T)) ?? Array.Empty<LocalVariableInfo>();
    }

    /// <summary>
    /// Outputs an If (Multiplayer.Active) check
    /// </summary>
    /// <param name="jmpLabel">Spot to jump to if Multiplayer.Active is false</param>
    /// <param name="generator">The ILGenerator</param>
    /// <returns></returns>
    public static IEnumerable<CodeInstruction> IsMultiplayer(Label jmpLabel, ILGenerator generator)
    {
        yield return new CodeInstruction(OpCodes.Callvirt, Reflect.Property(() => Multiplayer.Active).GetMethod);
        yield return new CodeInstruction(OpCodes.Brfalse, jmpLabel); // If false jump to the end of the code block
    }

    /// <summary>
    /// Outputs an If (!Multiplayer.Active) check
    /// </summary>
    /// <param name="jmpLabel">Spot to jump to if Multiplayer.Active is true</param>
    /// <param name="generator">The ILGenerator</param>
    /// <returns></returns>
    public static IEnumerable<CodeInstruction> IsNotMultiplayer(Label jmpLabel, ILGenerator generator)
    {
        yield return new CodeInstruction(OpCodes.Callvirt, Reflect.Property(() => Multiplayer.Active).GetMethod);
        yield return new CodeInstruction(OpCodes.Brtrue, jmpLabel); // If true jump to the end of the code block
    }

    /// <summary>
    /// Returns the one and only local variable of type <typeparamref name="T"/>. Throws <see cref="InvalidOperationException"/> if there is not exactly one local variable of that type.
    /// </summary>
    /// <exception cref="InvalidOperationException" />
    public static int GetLocalVariableIndex<T>(this MethodBase method)
    {
        return GetMatchingLocalVariables<T>(method).Single().LocalIndex;
    }

    /// <summary>
    /// Returns the index of the <paramref name="i"/>'th local variable of type <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException" />
    public static int GetLocalVariableIndex<T>(this MethodBase method, int i)
    {
        return GetMatchingLocalVariables<T>(method).ElementAt(i).LocalIndex;
    }

    /// <summary>
    /// Returns the shortest possible Ldloc instruction for the given local variable index <paramref name="i"/>.
    /// </summary>
    private static CodeInstruction Ldloc(int i)
    {
        switch (i)
        {
            case 0:
                return new CodeInstruction(OpCodes.Ldloc_0);
            case 1:
                return new CodeInstruction(OpCodes.Ldloc_1);
            case 2:
                return new CodeInstruction(OpCodes.Ldloc_2);
            case 3:
                return new CodeInstruction(OpCodes.Ldloc_3);
            default:
                if (i <= 0xFF)
                {
                    return new CodeInstruction(OpCodes.Ldloc_S, (byte)i);
                }
                else
                {
                    return new CodeInstruction(OpCodes.Ldloc, (ushort)i);
                }
        }
    }

    /// <summary>
    /// Returns an instruction that loads the one and only local variable of type <typeparamref name="T"/> in <paramref name="method"/> onto the evaluation stack.
    /// </summary>
    /// <typeparam name="T">The type to locate</typeparam>
    /// <param name="method">The method in which to locate the local variable</param>
    public static CodeInstruction Ldloc<T>(this MethodBase method)
    {
        return Ldloc(method.GetLocalVariableIndex<T>());
    }

    /// <summary>
    /// Loads the <paramref name="i"/>'th occurence of a local variable of type <typeparamref name="T"/> in <paramref name="method"/>.
    /// </summary>
    /// <typeparam name="T">The type to locate</typeparam>
    /// <param name="method">The method in which to locate the local variable</param>
    /// <param name="i">Index of the local variable to load, in a list of only variables of type <typeparamref name="T"/></param>
    public static CodeInstruction Ldloc<T>(this MethodBase method, int i)
    {
        if (method == null)
        {
            return new CodeInstruction(OpCodes.Nop);
        }

        return Ldloc(method.GetLocalVariableIndex<T>(i));
    }

    // ReSharper disable once UnusedMember.Global
    public static CodeMatcher LogInstructions(this CodeMatcher matcher)
    {
        StringBuilder sb = new();
        sb.AppendLine();
        sb.Append("IL-Instructions Valid: ");
        sb.AppendLine(matcher.IsValid.ToString());
        sb.AppendLine(matcher.InstructionEnumeration().ToPrettyString());
        Log.Info(sb.ToString());

        return matcher;
    }
}

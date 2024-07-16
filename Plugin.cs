// Copyright (C) 2024 Rémy Cases
// See LICENSE file for extended copyright information.
// This file is part of the Speedshard repository from https://github.com/remyCases/TheIronOath-AllyFreeze.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Tools;

namespace AllyFreeze;

public static class PluginInfo
{
    public const string PLUGIN_GUID = "AllyFreeze";
    public const string PLUGIN_NAME = "AllyFreeze";
    public const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Log;
    private void Awake()
    {
        Log = base.Logger;
        // Plugin startup logic
        HarmonyFileLog.Enabled = true;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        Harmony harmony = new(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }
}

[HarmonyDebug]
[HarmonyPatch]
public static class AllyFreeze
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BaseClass))]
    [HarmonyPatch(nameof(BaseClass.AddDebuff), new System.Type[] { typeof(DebuffBase), typeof(bool) })]
    static IEnumerable<CodeInstruction> BaseClassAddDebuff(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {       
        bool areWeImmuneFound = false;
        bool patchingDone = false;
        bool labelTrueFix = false;
        bool labelFalseFix = false;

        Label labelFalse = il.DefineLabel();
        Label labelTrue = il.DefineLabel();

        FieldInfo debuffFld = typeof(BaseClass)
            .GetNestedTypes(AccessTools.all)
            .Where(t => t.Name.Contains("Display"))
            .SelectMany(AccessTools.GetDeclaredFields)
            .FirstOrDefault(m => m.Name.Contains("debuff"));

        foreach(CodeInstruction instruction in instructions)
        {
            if (!areWeImmuneFound && instruction.Calls(AccessTools.Method(typeof(BaseClass), nameof(BaseClass.AreWeImmune), new System.Type[] { typeof(ResistType) })))
            {
                areWeImmuneFound = true;
            }
            else if (!patchingDone && areWeImmuneFound && instruction.opcode == OpCodes.Brfalse)
            {
                yield return new CodeInstruction(OpCodes.Brfalse_S, labelFalse);
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Brfalse_S, labelTrue);
                yield return new CodeInstruction(OpCodes.Ldloc_0);
                yield return new CodeInstruction(OpCodes.Ldfld, debuffFld);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BuffBase), "Type"));
                yield return new CodeInstruction(OpCodes.Ldc_I4_S, 15);
                yield return new CodeInstruction(OpCodes.Beq_S, labelFalse);

                patchingDone = true;
                continue;
            }
            else if (!labelTrueFix && patchingDone && instruction.opcode == OpCodes.Ldarg_0)
            {
                instruction.labels.Add(labelTrue);
                labelTrueFix = true;
            }
            else if (!labelFalseFix && labelTrueFix && instruction.opcode == OpCodes.Ldarg_0)
            {
                instruction.labels.Add(labelFalse);
                labelFalseFix = true;
            }
            yield return instruction;
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Pugilist))]
    [HarmonyPatch(nameof(Pugilist.AddDebuff), new System.Type[] { typeof(DebuffBase), typeof(bool) })]
    static IEnumerable<CodeInstruction> PugilistAddDebuff(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {       
        bool patchingDone = false;
        bool labelTrueFix = false;
        bool labelFalseFix = false;

        Label labelFalse = il.DefineLabel();
        Label labelTrue = il.DefineLabel();

        foreach(CodeInstruction instruction in instructions)
        {
            if (!patchingDone && instruction.opcode == OpCodes.Bne_Un)
            {
                yield return new CodeInstruction(OpCodes.Beq, labelTrue);
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Brfalse_S, labelFalse);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BuffBase), "Type"));
                yield return new CodeInstruction(OpCodes.Ldc_I4_S, 15);
                yield return new CodeInstruction(OpCodes.Bne_Un, labelFalse);

                patchingDone = true;
                continue;
            }
            else if (!labelTrueFix && patchingDone && instruction.opcode == OpCodes.Ldarg_0)
            {
                instruction.labels.Add(labelTrue);
                labelTrueFix = true;
            }
            else if (!labelFalseFix && labelTrueFix && instruction.opcode == OpCodes.Ldarg_0)
            {
                instruction.labels.Add(labelFalse);
                labelFalseFix = true;
            }
            yield return instruction;
        }
    }
}
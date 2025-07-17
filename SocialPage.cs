using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace Voidsent.Patches;

/*
 * Usage:
 * Add the following keys to Strings\StringsFromCSFiles to override the relationship descriptor text for an NPC:
 * - SocialPage_Relationship_Housemate_{NPC Name}
 * - SocialPage_Relationship_Spouse_{NPC Name}
 * - SocialPage_Relationship_Partner_{NPC Name}
 * - SocialPage_Relationship_ExSpouse_{NPC Name}
 * - SocialPage_Relationship_Single_{NPC Name}
 *
 * Some languages have gender-specific versions for every single relationship, so
 * Single and Housemate do need to have i18ns added too when making a character's
 * relationship descriptors gender-neutral, even if they stay the same in English,
 * so that translators can correctly translate them.
 */

[HarmonyPatch(typeof(StardewValley.Menus.SocialPage))]
[HarmonyPatch(nameof(StardewValley.Menus.SocialPage.drawNPCSlot))]
public static class SocialPagePatch
{
    private static IMonitor Monitor { get; set; } = null!;

    public static void Initialize(IMonitor monitor)
    {
        Monitor = monitor;
    }

    private static bool CheckForNamedString(string baseString, string name)
    {
        return Game1.content.LoadStringReturnNullIfNotFound($"{baseString}_{name}") is not null;
    }

    private static string NamedString(string baseString, string name)
    {
        return Game1.content.LoadString($"{baseString}_{name}");
    }

    /*
     * The five relationship types are all structured the same way, so patch them the same way,
     * replacing <searchString> and <baseString> with the specific strings for each type:
     *
     * Original IL:
     *
         [possibly label0 (see note below)] ldloc.2
         ldc.i4.1
         beq.s label1
         
         ldsfld class StardewValley.LocalizedContentManager StardewValley.Game1::content
         ldstr <searchString>
         callvirt instance string StardewValley.LocalizedContentManager::LoadString(string)
         br.s label2
         ...
     *
     * Patched IL:
     *
         [possibly label0 (see note below)] ldstr <baseString>
         ldloc.1
         call static System.Boolean Voidsent.SocialPagePatch::CheckForNamedString(System.String baseString, System.String name)
         brfalse.s label3
         
         ldstr <baseString>
         ldloc.1
         call static System.String Voidsent.SocialPagePatch::NamedString(System.String baseString, System.String name)
         br.s label2
         
         [label3] ldloc.2
         ldc.i4.1
         beq.s label1
         
         ldsfld StardewValley.LocalizedContentManager StardewValley.Game1::content
         ldstr <searchString>
         callvirt virtual System.String StardewValley.LocalizedContentManager::LoadString(System.String path)
         br.s label2
         ...
     *
     * possibly label0: not all relationship types have a label here, but if one exists it's moved as shown.
     *
     * Original C#:
     *
         (gender == Gender.Female) 
             ? Game1.content.LoadString(...) 
             : Game1.content.LoadString(...)
     *
     * Patched C# (approximately):
     *
         CheckForNamedString(<baseString>, name)
             ? NamedString(<baseString>, name)
             : (gender == Gender.Female)
                 ? Game1.content.LoadString(...)
                 : Game1.content.LoadString(...)
     * 
     */
    private static CodeMatcher PatchRelationship(
        CodeMatcher codeMatcher,
        string searchString,
        string baseString
    )
    {
        codeMatcher
            .MatchStartForward(new CodeMatch(OpCodes.Ldstr, searchString))
            .MatchStartForward(new CodeMatch(OpCodes.Br_S));
        var label = (Label)codeMatcher.Operand;

        codeMatcher
            .MatchStartBackwards(new CodeMatch(OpCodes.Beq_S))
            .MatchStartBackwards(new CodeMatch(OpCodes.Ldc_I4_1))
            .MatchStartBackwards(new CodeMatch(OpCodes.Ldloc_2));

        var firstInstruction = new CodeInstruction(OpCodes.Ldstr, baseString);
        codeMatcher.Instruction.MoveLabelsTo(firstInstruction);

        codeMatcher
            .CreateLabel(out Label skiplabel)
            .Insert(
                firstInstruction,
                new CodeInstruction(OpCodes.Ldloc_1),
                CodeInstruction.Call(static (string baseStr, string name) => CheckForNamedString(baseStr, name)),
                new CodeInstruction(OpCodes.Brfalse_S, skiplabel),
                new CodeInstruction(OpCodes.Ldstr, baseString),
                new CodeInstruction(OpCodes.Ldloc_1),
                CodeInstruction.Call(static (string baseStr, string name) => NamedString(baseStr, name)),
                new CodeInstruction(OpCodes.Br_S, label)
            );
        return codeMatcher;
    }

    private static IEnumerable<CodeInstruction>? Transpiler(IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        try
        {
            var codeMatcher = new CodeMatcher(instructions, generator);

            // Housemates
            codeMatcher = PatchRelationship(
                codeMatcher,
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Housemate_Male",
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Housemate"
            );

            // Spouses
            codeMatcher = PatchRelationship(
                codeMatcher,
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Husband",
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Spouse"
            );

            // Partners
            codeMatcher = PatchRelationship(
                codeMatcher,
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Boyfriend",
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Partner"
            );

            // Ex-Spouses
            codeMatcher = PatchRelationship(
                codeMatcher,
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_ExHusband",
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_ExSpouse"
            );

            // Singles
            codeMatcher = PatchRelationship(
                codeMatcher,
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Single_Male",
                "Strings\\StringsFromCSFiles:SocialPage_Relationship_Single"
            );

            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex)
        {
            Monitor.Log("SocialPage Error", LogLevel.Error);
            Monitor.Log(ex.Message, LogLevel.Error);
        }

        return null;
    }
}

/// <summary>
/// Type token for a consumer mod's Harmony field. A consumer harness may explicitly resolve its
/// optional Harmony reference here for type loading only. No constructor, patch or unpatch member
/// is declared, so this cannot silently simulate a successful patch operation.
/// </summary>
namespace HarmonyLib
{
    public class Harmony
    {
    }
}

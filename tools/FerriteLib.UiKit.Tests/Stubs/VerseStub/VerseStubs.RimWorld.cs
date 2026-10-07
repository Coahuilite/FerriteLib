/// <summary>
/// Game definition type tokens for consumer production projections. They live in their own
/// stub file because the Verse stub file uses a file-scoped namespace; the types must sit in the
/// REAL namespace so a product TypeRef resolves against them at harness runtime.
/// </summary>
namespace RimWorld
{
    /// <summary>
    /// Biotech's xenotype def, stubbed as a bare Def: a catalog snapshot's static Empty instance
    /// names the type in its generic collections, so the type must exist for the class initializer to
    /// run; no harness path reads a member of it (the fixture catalogs carry no real XenotypeDefs).
    /// </summary>
    public class XenotypeDef : Verse.Def
    {
    }
}

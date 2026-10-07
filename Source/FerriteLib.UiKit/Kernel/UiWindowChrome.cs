using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One window shell's own geometry, in window (screen) space, as the shell computed it (DT1).
/// <para>
/// <b>Why this exists.</b> The shell paints its frame, title band and close affordance from its
/// <see cref="UiWindowHost"/> chrome virtuals, and the Dev instrument outlines and records those same
/// rects; a consumer that needs the shell's numbers used to have to mirror the constants, and a mirror is
/// a second copy of an arithmetic that can drift. This struct is the shell answering once. Two channels,
/// deliberately different in tense: <c>UiWindowHost.ShellChrome</c> is the PRESENT measurement (act on the
/// window as it is now, retire your mirrors with it), while
/// <c>UiDevGeometrySnapshot.ShellChrome</c> / the dump's <c>chrome</c> line is the record of a COMPLETED
/// pass, written into the same capture buffer as the node samples when the chrome drew - a report of a
/// pass quotes the record, never a present-tense read spliced onto a finished dump.
/// </para>
/// <para>
/// <b>Read-only, and built only by the shell.</b> The constructor is internal, so no caller can hand out
/// geometry the shell did not compute. A value is a snapshot: reading the accessor again after a drag or
/// resize answers the new numbers, and a captured record keeps the numbers of the pass it was written in
/// - which is why a report reads it per pass rather than caching one read.
/// </para>
/// </summary>
public readonly struct UiWindowChrome
{
    internal UiWindowChrome(
        Rect outer, Rect titleBand, Rect closeButton, Rect content, float sidePadding, float titleBarHeight)
    {
        Outer = outer;
        TitleBand = titleBand;
        CloseButton = closeButton;
        Content = content;
        SidePadding = sidePadding;
        TitleBarHeight = titleBarHeight;
    }

    /// <summary>The window's own rect on screen (<c>windowRect</c> as the game moved or resized it).</summary>
    public Rect Outer { get; }

    /// <summary>The band reserved for title, subtitle and close affordance: the face top down to the shell's title height.</summary>
    public Rect TitleBand { get; }

    /// <summary>The close affordance's rect, at the size the shell measured for it.</summary>
    public Rect CloseButton { get; }

    /// <summary>The page box the tree is arranged and drawn in - the shell's <c>ContentRect</c> answer.</summary>
    public Rect Content { get; }

    /// <summary>The side/bottom inset the shell applied. Values, so a report can name them without re-spelling them.</summary>
    public float SidePadding { get; }

    /// <summary>The title height the shell applied. The same number <see cref="TitleBand"/> was built with.</summary>
    public float TitleBarHeight { get; }
}

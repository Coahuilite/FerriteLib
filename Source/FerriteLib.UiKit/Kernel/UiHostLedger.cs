using System;
using System.Collections.Generic;
using System.Text;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Which manifest sources have actually built a page tree in this process. The runtime half of the D-1
/// dependency-reality rule (FL→US round 1, item E): a mod can declare the prerequisite and compile
/// against the carrier, and the only thing that distinguishes a live consumer from a decorative one is
/// whether a <see cref="UiHost"/> was ever constructed. <see cref="FerriteLibVersion.Require"/> reports
/// what it holds, so "declared the dependency but does not use the runtime" is visible in a log instead
/// of being a convention.
/// <para>
/// Deliberately minimal: append-only, deduplicated, insertion-ordered, no removal path. There is no
/// <c>Clear</c> here for the same reason <c>UiWidgetRegistry.Clear</c> went internal — a public lever
/// that wipes process-wide state belongs to whoever created it, and this record is read by a diagnostic
/// another mod may trigger before this one ever opens a window.
/// </para>
/// <para>
/// BCL-only on purpose, like <see cref="FerriteLibVersion"/>: the two are read together from a consumer
/// constructor, before any UnityEngine or Verse type has to be loaded.
/// </para>
/// </summary>
internal static class UiHostLedger
{
    private static readonly object Gate = new object();
    private static readonly List<string> Sources = new List<string>();

    /// <summary>Records one manifest source. Called from <see cref="UiHost"/>'s constructor.</summary>
    internal static void Record(string source)
    {
        if (string.IsNullOrEmpty(source)) return;

        lock (Gate)
        {
            if (Sources.Contains(source)) return;
            Sources.Add(source);
        }
    }

    /// <summary>
    /// The diagnostic line, or an empty string when no host has been built yet — which is itself the
    /// answer the rule is looking for, so the caller decides how to phrase the absence.
    /// </summary>
    internal static string Describe()
    {
        lock (Gate)
        {
            if (Sources.Count == 0) return "";

            var report = new StringBuilder();
            report.Append("  page trees built in this process: ").Append(Sources.Count).Append(" [");
            for (int i = 0; i < Sources.Count; i++)
            {
                if (i > 0) report.Append(", ");
                report.Append(Sources[i]);
            }

            report.Append(']');
            return report.ToString();
        }
    }
}

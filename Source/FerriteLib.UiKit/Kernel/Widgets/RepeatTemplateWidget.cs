using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// The <c>Repeat</c> manifest element's declaration contract. The element is not a drawn control: the
/// layout engine claims it as a container before any widget path and materializes one identity per item
/// key from the template it names, so what is registered here is the creation-time validator the Host
/// runs - an element needs an owner for its attribute schema and its binding contract.
/// <para>
/// <b>What it refuses at creation.</b> An <c>Items</c> attribute that is missing, or that names something
/// other than a <c>IReadOnlyList&lt;string&gt;</c> value binding, is a page that cannot produce a single
/// row and is refused here with the element's path - not discovered as an empty band on the first frame.
/// The template reference itself is the manifest parser's contract (the template table is the parser's),
/// and the per-row key refusals are the engine's, because only the current item list can show them.
/// </para>
/// <para>
/// The item list is the consumer's projection of its own collection to stable business keys. The library
/// never reflects over item objects: keys are the only identity it needs, and a row's per-item bindings
/// live in the item-local scope the engine composes from those keys
/// (<c>&lt;Items&gt;.&lt;itemKey&gt;.&lt;declaredKey&gt;</c>).
/// </para>
/// </summary>
internal sealed class RepeatTemplateWidget : IUiWidget
{
    internal const string Kind = "Repeat";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new RepeatTemplateWidget(),
            new[]
            {
                "Id", "Kind", "Items", "Template", "Padding", "Gap", "Height",
                "Hidden", "Tab", "Visible", "VisibleKey"
            });
        // No label set: the rows' labels live in the template's own kinds, and Width="Auto" on the repeat
        // itself would be measuring a box that draws no text.
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string items = AtomVocabulary.Read(spec, "Items").Trim();
        if (items.Length == 0)
        {
            throw new InvalidOperationException(
                "Repeat at '" + elementPath + "' requires an Items attribute naming the value binding that "
                + "holds its ordered item keys; a row set with no identity source cannot render a row.");
        }

        if (!spec.TryGetAttribute("Template", out string template) || template.Trim().Length == 0)
        {
            throw new InvalidOperationException(
                "Repeat at '" + elementPath + "' requires a Template attribute naming the template declared "
                + "in <Templates>; the per-item subtree is declared once and materialized per key.");
        }

        bindings.ValidateValue<IReadOnlyList<string>>(items, elementPath);
    }

    /// <summary>Never reached: the engine arranges a Repeat through its own container path.</summary>
    public float Measure(UiWidgetContext ctx)
    {
        return 0f;
    }

    /// <summary>Never reached: the engine arranges a Repeat through its own container path.</summary>
    public void Draw(Rect rect, UiWidgetContext ctx)
    {
    }
}

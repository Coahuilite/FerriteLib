using System;
using System.Collections.Generic;
namespace FerriteLib.UiKit.Kernel;
/// <summary>The common declaration contract for XML and C# definitions, page roots and item templates.</summary>
internal sealed class UiDefinitionCompiler
{
    internal static UiBindingRole BindingRole(string scope,UiElementSpec spec,string attribute) { var role=UiWidgetRegistry.Schema(scope,spec.Kind)?.Binding(attribute)??UiBindingRole.None;return role!=UiBindingRole.None?role:UiAttributeSchema.StandardBinding(attribute); }
    private readonly string source;
    private readonly IUiBindingReader? bindings;
    internal UiDefinitionCompiler(string source,IUiBindingReader? bindings=null){this.source=source;this.bindings=bindings;}
    internal void ValidateTemplates(IReadOnlyDictionary<string,UiElementSpec> templates)
    { foreach(var entry in templates)ValidateElement(entry.Value,"<Templates>/"+entry.Key,false,null); }    internal void ValidateManifest(UiLayoutManifest candidate)
    {
        if (candidate.Background != null && bindings != null)
            UiTextureBinding.Validate(bindings, candidate.Background.Bind, "window/background");
        foreach (UiElementSpec root in candidate.Roots)
        {
            ValidateElement(root, root.Id.Length > 0 ? root.Id : root.Kind, parentNarrowCapable: false, parent: null);
        }
    }

    // Layout vocabulary belongs to the tree, not to the kind: Width/MinWidth/MaxWidth and the
    // narrow-state visibility variant are read by the engine for every child, so they are allowed
    // on widgets too. Before round 3 this was a live defect: ResolveColumnWidths already read
    // Width on any child, yet no core kind's schema listed it — a manifest could not size a
    // stepper-slider column at all (US->FL round 3, N1's library-side specimen).
    // Scheme and Density are the scope vocabulary: they are allowed on every kind - a widget narrows its
    // own scope, a container is a region its subtree inherits - because the engine reads them for every
    // child of the tree and the style chain, not the kind, is what carries them. Before batch B neither
    // name existed, so an author could not reach the document's schemes at all.
    private static readonly HashSet<string> CommonWidgetAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Kind", "Hidden", "Tab", "Height", "Width", "WidthKey", "MinWidth", "MaxWidth", "NarrowHidden",
        "WideHidden", "SelectedKey", "Scheme", "Density",
        "Visible", "VisibleKey",
        // The element's help identity (0.7.x): engine-wide like Visible/VisibleKey, because the ENGINE -
        // not the kind - claims a hovered element's help. Deliberately NOT in the container list: only
        // widgets are hit surfaces, so a declaration on a container or a template root could never claim,
        // and the unknown-attribute gate refuses it there instead of leaving it silently inert. A
        // binding-resolved sibling (a HelpBind) is CONTINGENT on the Route A decision and deliberately not
        // built: the only two data-dependent sites live in a widget that will not be migrated
        // declaratively unless Route A lands, so the need is not established yet (0.7 contract).
        "HelpKey",
        // CP-1 placement vocabulary (0.7.x, Batch 1): valid only for a child of a placement container.
        // The engine refuses them elsewhere and refuses contradictory combinations; these two lists are
        // the attribute-NAME gate, and UiLayoutEngine keeps a mirrored pair for template subtrees that a
        // lane reflects against these, so the two must move together.
        "AlignX", "OffsetX", "AlignY", "OffsetY", "AnchorX", "AnchorY",
        "Font",
        // FL-IC2 (interaction contract §3.6): the tree's Cancel layer. Engine-wide like VisibleKey and HelpKey,
        // because the ENGINE reads it and publishes it on the node for the ladder to walk - no widget kind owns
        // it, so no kind's attribute schema has to grow for a page to declare a return layer. Unlike HelpKey it
        // IS allowed on a container: a section that draws nothing of its own is still a layer a player can
        // return from, which is the whole difference between a hit surface and a tree node.
        "CancelBind",
        "UpdateScope"
    };

    private static readonly HashSet<string> ContainerAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tab is here because the engine already reads it for every element it decides visibility for - the
        // same IsHidden that reads NarrowHidden and Hidden, and RecordDeclaredKeys already registers the
        // shared active-tab key for any declarer. Omitting it here forbade a capability the engine
        // implemented; the 0.7 contract records the ruling (2026-09-20). A Tab-gated container hides its
        // whole subtree, like every other hidden element.
        "Id", "Kind", "Gap", "Padding", "Height", "Title", "TitleKey", "Hidden", "Tab", "Width", "WidthKey",
        "Fill", "MinWidth", "MaxWidth", "Breakpoint", "Narrow", "Cols", "NarrowCols", "NarrowHidden", "WideHidden",
        "VisibleRows",
        "Scheme", "Density", "Visible", "VisibleKey",
        "AlignX", "OffsetX", "AlignY", "OffsetY", "AnchorX", "AnchorY",
        "Font", "SharedLabels",
        // FL-IC2: see the widget list; UiLayoutEngine's mirrored pair carries the same addition, and the drift
        // lane refuses one-sided edits.
        "CancelBind",
        "UpdateScope"
    };

    private static readonly UiAttributeSchema ContainerSchema=UiAttributeSchema.FromNames(ContainerAttributes), CommonSchema=UiAttributeSchema.FromNames(CommonWidgetAttributes);
    private void ValidateElement(UiElementSpec spec, string path, bool parentNarrowCapable, UiElementSpec? parent)
    {
        // CP-1/CP-2 placement contract, checked here because this is the creation-time door: a placement
        // attribute is valid only inside a placement container, and a flow container's child accepts the
        // cross-axis subset without an offset. The rule lives in UiPlacement so the engine can share it.
        if (spec.TryGetAttribute("UpdateScope", out string updateScope) && !bool.TryParse(updateScope.Trim(), out _))
            throw new UiContractException("UpdateScope must be true or false.", source, spec.Id, spec.Kind, path);
        UiPlacement.ValidateChild(spec, path, source, parent);
        UiPlacement.ValidateSiblingAnchors(spec, path, source);

        bool isWidget = string.Equals(spec.Kind, "Widget", StringComparison.Ordinal)
            || !IsContainerKind(spec.Kind);

        if (isWidget)
        {
            ValidateAttributes(spec, path, isContainer: false);
            IUiWidget widget;
            try
            {
                widget = UiWidgetRegistry.Resolve(source, spec.Kind);
            }
            catch (UiUnknownWidgetKindException ex)
            {
                throw new UiContractException(
                    ex.Message,
                    source,
                    spec.Id,
                    spec.Kind,
                    path,
                    ex);
            }

            try
            {
                widget.Configure(spec);
                if(bindings != null) widget.Validate(bindings, path);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or UiContractException)
            {
                throw new UiContractException(
                    $"Widget validation failed at '{path}': {ex.Message}",
                    source,
                    spec.Id,
                    spec.Kind,
                    path,
                    ex);
            }
        }
        else
        {
            ValidateAttributes(spec, path, isContainer: true);
        }

        bool selfNarrowCapable = spec.TryGetAttribute("Breakpoint", out _);
        ValidateLayoutAttributes(spec, path, isContainer: !isWidget, selfNarrowCapable, parentNarrowCapable);
        ValidateContentHeight(spec, path, parent);
        ValidateViewportRows(spec, path, isWidget);

        foreach (UiElementSpec child in spec.Children)
        {
            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);
            ValidateElement(child, childPath, selfNarrowCapable, spec);
        }
    }

    /// <summary>
    /// Creation-time grammar for the D4 viewport count (same stance as the MatchContent matrix and the
    /// closed <c>Height</c> vocabulary): <c>VisibleRows</c> is Scroll-only - a number another element
    /// would silently ignore is an inert declaration this library refuses at creation, located - and it
    /// must parse to a positive finite decimal (fractions are the point: 4.5 is a legal promise, "abc",
    /// 0 and -1 are authoring errors). The engine's read stays fail-soft for programmatically built
    /// specs; a MANIFEST that ships these is a contract error, caught here where element, attribute and
    /// path are known.
    /// </summary>
    private void ValidateViewportRows(UiElementSpec spec, string path, bool isWidget)
    {
        if (!spec.TryGetAttribute("VisibleRows", out string raw)) return;

        if (isWidget || !string.Equals(spec.Kind, "Scroll", StringComparison.Ordinal))
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares VisibleRows on a <" + spec.Kind
                + ">; the count sizes a SCROLL viewport from its measured rows, and every other element's"
                + " height means something else. Declare it on the <Scroll>.",
                source, spec.Id, spec.Kind, path);
        }

        string value = (raw ?? "").Trim();
        if (value.Length == 0
            || !float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float rows)
            || rows <= 0f || float.IsNaN(rows) || float.IsInfinity(rows))
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares VisibleRows='" + (raw ?? "")
                + "'; expected a positive finite number of rows (fractions allowed, e.g. 4.5).",
                source, spec.Id, spec.Kind, path);
        }
    }

    /// <summary>
    /// The content-relative height mode's creation-time matrix (0.7.x). The mode says "take the height my
    /// parent's content resolved to", so it is accepted exactly where that reference exists <b>before</b> the
    /// declaring element is measured and is not built from it: a child of a <c>Row</c> or an <c>Overlay</c> (both
    /// take the MAXIMUM over their children) that has at least one sibling which does not declare the mode.
    /// A parent that sums its children, a <c>Wrap</c>, a root, and a parent whose every child declares it are
    /// refused here - the same fail-closed shape as the placement matrix, for the same reason: a declaration the
    /// arrangement could never honour is the silent no-op this contract layer exists to stop.
    /// <para>
    /// This is the Host's walk, so it covers page elements. A template subtree is deliberately outside it (its
    /// binding keys are item-scoped and cannot exist as page bindings), which is the known gap the 0.5 contract
    /// records: inside a template the engine degrades the mode to the element's own measured content instead.
    /// </para>
    /// </summary>
    private void ValidateContentHeight(UiElementSpec spec, string path, UiElementSpec? parent)
    {
        if (!UiPlacement.IsMatchContentHeight(spec)) return;

        if (parent == null)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares Height=\"MatchContent\" with no parent container; "
                + "the mode takes the height the parent's content resolved to, so it is valid only on a child of a Row or an Overlay.",
                source, spec.Id, spec.Kind, path);
        }

        if (!UiPlacement.HasContentHeightReference(parent.Kind))
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares Height=\"MatchContent\" inside a <" + parent.Kind + ">; "
                + "that container's content height is built from its own children (this element included), so there is no "
                + "reference to match. A Row or an Overlay takes the maximum over its children and can answer it.",
                source, spec.Id, spec.Kind, path);
        }

        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (!UiPlacement.IsMatchContentHeight(parent.Children[i])) return;
        }

        throw new UiContractException(
            $"Element id=\"{spec.Id}\" at '{path}' declares Height=\"MatchContent\" in a <" + parent.Kind
            + "> whose every child declares the same; the height it would match comes from a sibling, so at least one "
            + "child must not declare it.",
            source, spec.Id, spec.Kind, path);
    }

    /// <summary>
    /// Creation-time grammar for the responsive vocabulary (US->FL round 3, N1+N2). The engine
    /// degrades malformed values to "not declared" so a programmatically built spec cannot crash a
    /// frame; a MANIFEST that ships them is a contract error, caught here where the element, the
    /// attribute and the path are all still known.
    /// </summary>
    private void ValidateLayoutAttributes(
        UiElementSpec spec, string path, bool isContainer, bool selfNarrowCapable, bool parentNarrowCapable)
    {
        if (spec.TryGetAttribute("Width", out string widthRaw))
        {
            string width = widthRaw.Trim();
            bool auto = string.Equals(width, "Auto", StringComparison.OrdinalIgnoreCase);
            if (!auto && (!float.TryParse(width, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsed) || parsed <= 0f))
            {
                throw new UiContractException(
                    $"Element id=\"{spec.Id}\" at '{path}' has invalid Width '{widthRaw}'; expected a positive number or Auto.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        // A2 (0.7): Height belongs to the same creation-time contract as Width. Until now a malformed
        // value survived validation and only exploded at arrange time as an unattributable
        // FormatException from deep inside the engine; the layout engine's own defensive checks stay,
        // this just refuses the value where the element, attribute and path are all still known.
        // Zero and negative finite numbers keep their existing clamp behavior - no new positive-only
        // rule - but NaN and infinities are refused: ResolveHeight would take Math.Max(0f, NaN) down
        // into every downstream rect.
        if (spec.TryGetAttribute("Height", out string heightRaw))
        {
            string height = heightRaw.Trim();
            bool heightAuto = string.Equals(height, "Auto", StringComparison.OrdinalIgnoreCase);
            // The content-relative mode (0.7.x) is the third accepted value. Its own placement matrix - which
            // parents can answer the reference it asks for - is checked in ValidateContentHeight below, where
            // the parent element is in hand.
            bool heightContent = UiPlacement.IsMatchContentHeight(spec);
            bool heightNumber = float.TryParse(height, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsedHeight)
                && !float.IsNaN(parsedHeight) && !float.IsInfinity(parsedHeight);
            if (height.Length > 0 && !heightAuto && !heightContent && !heightNumber)
            {
                throw new UiContractException(
                    $"Element id=\"{spec.Id}\" at '{path}' has invalid Height '{heightRaw}'; expected a number, Auto or MatchContent.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        // Visible is engine-wide and static, so its value is part of the creation-time contract like
        // Width: a malformed value would otherwise be interpreted per frame by the engine, and the one
        // place that still knows the element, the attribute and the path is here. VisibleKey is
        // deliberately NOT resolved against the injected bindings: a page must not fail to exist because
        // a model key is missing or not yet bound. The engine keeps such an element visible and records
        // one deduplicated appearance fallback instead - fail-soft, but not silent.
        if (spec.TryGetAttribute("Visible", out string visibleRaw))
        {
            string visible = visibleRaw.Trim();
            if (!string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(visible, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(visible, "1", StringComparison.Ordinal)
                && !string.Equals(visible, "0", StringComparison.Ordinal))
            {
                throw new UiContractException(
                    $"Element id=\"{spec.Id}\" at '{path}' has invalid Visible '{visibleRaw}'; expected true or false.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        ValidatePositiveNumber(spec, path, "MinWidth");
        ValidatePositiveNumber(spec, path, "MaxWidth");
        if (spec.TryGetAttribute("MinWidth", out string minRaw)
            && spec.TryGetAttribute("MaxWidth", out string maxRaw)
            && float.TryParse(minRaw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float minW)
            && float.TryParse(maxRaw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float maxW)
            && minW > maxW)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares MinWidth {minRaw} above MaxWidth {maxRaw}.",
                source, spec.Id, spec.Kind, path);
        }

        // A responsive-state attribute under no Breakpoint is the silent no-op this library's creation
        // contract exists to stop: the author believes they declared a variant that can never fire.
        // WideHidden is the mirror of NarrowHidden and needs the same parent, for the same reason.
        foreach (string responsive in new[] { "NarrowHidden", "WideHidden" })
        {
            if (spec.TryGetAttribute(responsive, out _) && !parentNarrowCapable)
            {
                throw new UiContractException(
                    $"Element id=\"{spec.Id}\" at '{path}' declares {responsive} but its parent carries no Breakpoint; nothing can ever switch the state it depends on.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        if (!isContainer)
        {
            foreach (string containerOnly in new[] { "Breakpoint", "Narrow", "Cols", "NarrowCols" })
            {
                if (spec.TryGetAttribute(containerOnly, out _))
                {
                    throw new UiContractException(
                        $"'{containerOnly}' at '{path}' is container vocabulary; the engine never reads it on a widget.",
                        source, spec.Id, spec.Kind, path);
                }
            }

            return;
        }

        // A3 (0.7): the grammar check above made Cols/NarrowCols well-formed numbers on every
        // container, but only the Wrap path ever reads them — on any other kind they are an inert
        // declaration of grid intent, which is exactly the silent no-op this contract layer exists
        // to stop. Either remove the attribute or choose Wrap if grid flow was intended.
        if (!string.Equals(spec.Kind, "Wrap", StringComparison.Ordinal))
        {
            foreach (string wrapOnly in new[] { "Cols", "NarrowCols" })
            {
                if (spec.TryGetAttribute(wrapOnly, out _))
                {
                    throw new UiContractException(
                        $"'{wrapOnly}' at '{path}' is Wrap-only vocabulary; a {spec.Kind} never reads it, so the declaration would do nothing.",
                        source, spec.Id, spec.Kind, path);
                }
            }
        }

        ValidatePositiveNumber(spec, path, "Breakpoint");
        ValidatePositiveInteger(spec, path, "Cols");
        ValidatePositiveInteger(spec, path, "NarrowCols");

        if (spec.TryGetAttribute("Narrow", out string narrowRaw) && !selfNarrowCapable)
        {
            throw new UiContractException(
                $"Container id=\"{spec.Id}\" at '{path}' declares Narrow but carries no Breakpoint; the variant could never be selected.",
                source, spec.Id, spec.Kind, path);
        }

        if (spec.TryGetAttribute("Narrow", out narrowRaw))
        {
            string narrow = narrowRaw.Trim();
            bool rowBecomesStacked = string.Equals(spec.Kind, "Row", StringComparison.Ordinal)
                && (string.Equals(narrow, "Column", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(narrow, "Stack", StringComparison.OrdinalIgnoreCase));
            bool stackBecomesRow = (string.Equals(spec.Kind, "Column", StringComparison.Ordinal)
                    || string.Equals(spec.Kind, "Stack", StringComparison.Ordinal)
                    || string.Equals(spec.Kind, "Section", StringComparison.Ordinal)
                    || string.Equals(spec.Kind, "Surface", StringComparison.Ordinal))
                && string.Equals(narrow, "Row", StringComparison.OrdinalIgnoreCase);
            if (!rowBecomesStacked && !stackBecomesRow)
            {
                throw new UiContractException(
                    $"Container id=\"{spec.Id}\" at '{path}' declares Narrow '{narrowRaw}'; only Row may name Column/Stack and only a vertical stack may name Row.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        if (spec.TryGetAttribute("NarrowCols", out _))
        {
            if (!spec.TryGetAttribute("Cols", out _))
            {
                throw new UiContractException(
                    $"Wrap id=\"{spec.Id}\" at '{path}' declares NarrowCols without Cols; the narrow variant needs the wide value to select between.",
                    source, spec.Id, spec.Kind, path);
            }

            if (!selfNarrowCapable)
            {
                throw new UiContractException(
                    $"Wrap id=\"{spec.Id}\" at '{path}' declares NarrowCols but carries no Breakpoint; nothing can ever make it narrow.",
                    source, spec.Id, spec.Kind, path);
            }
        }
    }

    private void ValidatePositiveNumber(UiElementSpec spec, string path, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return;
        if (!float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) || value <= 0f)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' has invalid {attribute} '{raw}'; expected a positive number.",
                source, spec.Id, spec.Kind, path);
        }
    }

    private void ValidatePositiveInteger(UiElementSpec spec, string path, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return;
        if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' has invalid {attribute} '{raw}'; expected a positive integer.",
                source, spec.Id, spec.Kind, path);
        }
    }

    /// <summary>
    /// Creation-time attribute contract check. Containers accept the fixed container vocabulary;
    /// widgets accept the common attributes (Id/Kind/Hidden/Tab) plus the kind's registered schema.
    /// Unknown attributes are rejected before any drawing happens.
    /// </summary>
    private void ValidateAttributes(UiElementSpec spec, string path, bool isContainer)
    {
        (isContainer?ContainerSchema:CommonSchema).Validate(spec,source,path);
        if(!isContainer)UiWidgetRegistry.Schema(source,spec.Kind)?.Validate(spec,source,path);
        IReadOnlyCollection<string>? allowed = isContainer
            ? ContainerAttributes
            : UiWidgetRegistry.GetAttributeSchema(source, spec.Kind);

        if (allowed == null) return;

        foreach (KeyValuePair<string, string> pair in spec.Attributes)
        {
            if (UiAppearanceProperties.IsMetricAttribute(pair.Key))
            {
                if (!UiAppearanceProperties.TryMetric(pair.Value, out _))
                    throw new UiContractException("Invalid " + pair.Key + " at '" + path + "': expected a finite non-negative metric.", source, spec.Id, spec.Kind, path);
                continue;
            }
            bool allowedAttribute = false;
            foreach (string candidate in allowed)
            {
                if (string.Equals(candidate, pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    allowedAttribute = true;
                    break;
                }
            }

            if (allowedAttribute || UiAppearanceProperties.IsPaletteAttribute(pair.Key)) continue;
            if (!isContainer && CommonWidgetAttributes.Contains(pair.Key)) continue;

            throw new UiContractException(
                $"Unknown attribute '{pair.Key}' on {spec.Kind} at '{path}' is not part of the creation-time contract.",
                source,
                spec.Id,
                spec.Kind,
                path);
        }
    }

    private static bool IsContainerKind(string kind)
    {
        return string.Equals(kind, "Stack", StringComparison.Ordinal)
            || string.Equals(kind, "Row", StringComparison.Ordinal)
            || string.Equals(kind, "Column", StringComparison.Ordinal)
            || string.Equals(kind, "Wrap", StringComparison.Ordinal)
            || string.Equals(kind, "Overlay", StringComparison.Ordinal)
            || string.Equals(kind, "Section", StringComparison.Ordinal)
            || string.Equals(kind, "Surface", StringComparison.Ordinal)
            || string.Equals(kind, "Scroll", StringComparison.Ordinal)
            || string.Equals(kind, "Clip", StringComparison.Ordinal);
    }
}





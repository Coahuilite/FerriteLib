using System;
using System.Collections.Generic;
using System.Globalization;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;
public sealed partial class UiLayoutEngine
{
    private int observationLayoutFrame;
    private DateTime? observationLayoutAtUtc;
    internal UiHostObservation Observe(UiWidgetContext ctx, UiObservationBudget budget, string? nodeKey, bool hostLayoutInvalidated)
    {
        var output = new List<UiObservedNode>();
        UiObservationStatus status = UiObservationStatus.Sampled;
        string reason = "";
        bool truncated = false;
        int start = 0;
        int end = lastEntries.Count;
        if (!ctx.Session.IsActive)
        {
            status = UiObservationStatus.Stale;
            reason = "host-closed";
            end = 0;
        }
        else if (cachedSnapshot == null)
        {
            status = UiObservationStatus.NotSampled;
            reason = "no-successful-arrangement";
        }
        else if (hostLayoutInvalidated || ObservationHasPendingLayoutNotification(ctx.Bindings)
            || cachedSnapshot.DefinitionRevision != ctx.Session.DefinitionRevision || ctx.Session.HasDirtyNodes
            || cachedContentRevision != ctx.Session.ContentRevision
            || cachedTranslationRevision != ctx.Translation.TranslationRevision)
        {
            status = UiObservationStatus.Stale;
            reason = "declared-invalidation-newer-than-layout";
        }

        if (nodeKey != null && end > 0)
        {
            start = lastEntries.FindIndex(entry => entry.Id.Key == nodeKey);
            if (start < 0)
            {
                start = end;
                UiNode? retained = ctx.Session.FindObservationNode(nodeKey);
                if (retained == null)
                {
                    status = UiObservationStatus.NotFound;
                    reason = "not-in-runtime-node-table; unmaterialized-declarations-are-not-runtime-nodes";
                }
                else
                {
                    reason = "retained-node-without-current-arranged-entry";
                    var retainedFacts = new List<UiObservedFact>
                    {
                        new("geometry", "", UiObservationStatus.NotSampled, "session", "not-arranged-or-not-in-current-engine"),
                        new("value", "", UiObservationStatus.NotSampled, "current-model", "no-current-arranged-binding-projection")
                    };
                    if (retained.EditingState?.Editor is IUiEditState retainedEdit)
                    {
                        retainedFacts.Add(new UiObservedFact("draft", budget.Limit(retainedEdit.Draft), source: "retained-local-editor", truncated: retainedEdit.Draft.Length > budget.Text));
                        retainedFacts.Add(new UiObservedFact("draft-focused", retainedEdit.Focused.ToString(), source: "retained-local-editor"));
                    }

                    output.Add(new UiObservedNode(retained.Id.Key, retained.Path, retained.Kind, retained.Parent?.Id.Key, 0, retainedFacts.AsReadOnly()));
                }
            }
            else
                end = Math.Min(end, start + lastEntries[start].SubtreeCount);
        }

        int rootDepth = start < end ? ObservationDepth(lastEntries[start].Node) : 0;
        for (int i = start; i < end; i++)
        {
            PlacedEntry entry = lastEntries[i];
            int depth = ObservationDepth(entry.Node) - (nodeKey == null ? 0 : rootDepth);
            if (depth > budget.Depth)
            {
                truncated = true;
                continue;
            }

            if (output.Count == budget.Nodes)
            {
                truncated = true;
                break;
            }

            UiWidgetContext entryCtx = ctx.WithViewWidth(entry.MeasureWidth).WithNode(entry.Node);
            var facts = new List<UiObservedFact>();
            void Fact(string name, string value, string source = "session", UiObservationStatus state = UiObservationStatus.Sampled, string why = "")
                => facts.Add(new UiObservedFact(name, budget.Limit(value), state, source, why, value.Length > budget.Text));
            Fact("identity", entry.Node.ElementId, "declaration");
            Fact("layout-frame", observationLayoutFrame.ToString(CultureInfo.InvariantCulture), "last-arrangement");
            Fact("layout-time-utc", observationLayoutAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "", "last-arrangement");
            Fact("geometry", RectText(entry.Rect), "last-arrangement/page-local", status == UiObservationStatus.Stale ? status : UiObservationStatus.Sampled);
            Fact("hit", "", "platform", UiObservationStatus.NotSampled, "point-and-current-window-origin-required; rectangle-is-not-hit-result");
            Fact("native-focus", "", "platform", UiObservationStatus.NotSampled, "observation-does-not-query-native-event-loop");
            Fact("pointer-allowed", ctx.Session.PointerInputAllowed.ToString());
            Fact("geometry-reliable", ctx.Session.IsInputGeometryReliable.ToString());
            Fact("pointer-can-receive", "", "platform", UiObservationStatus.NotSampled, "current-event-and-window-point-required");
            bool ownsCapture = ctx.Session.Interactions.CaptureOwner?.Node == entry.Id.Key;
            Fact("capture", ownsCapture.ToString());
            if (ownsCapture)
            {
                Fact("capture-part", ctx.Session.Interactions.CaptureOwner!.Value.Part);
                object? item = ctx.Session.Interactions.CaptureOwner.Value.ItemKey;
                if (item is string || item is int)
                    Fact("capture-item", Convert.ToString(item, CultureInfo.InvariantCulture) ?? "");
            }

            if (entry.ContentRect.HasValue)
                Fact("scroll-content", RectText(entry.ContentRect.Value), "last-arrangement/scroll-local");
            for (UiNode? ancestor = entry.Node.Parent; ancestor != null; ancestor = ancestor.Parent)
                if (ancestor.Kind == "Scroll" || ancestor.Kind == "Clip")
                    Fact("viewport-ancestor", ancestor.Id.Key, "last-arrangement/ancestor; native-origin-not-sampled");
            Fact("last-interaction-end", ctx.Session.Interactions.LastEnd.ToString(), "session/global-last-end-not-node-history");
            try
            {
                Fact("command-enabled", (!ResolveDisabled(entry.Spec, entryCtx)).ToString(), "current-command-predicate");
                ObserveWidget(entry, entryCtx, budget, facts);
            }
            catch (Exception ex)
            {
                Fact("widget", "", "current-model", UiObservationStatus.Failed, ex.GetType().Name);
            }

            if (entry.Node.EditingState?.Editor is IUiEditState edit)
            {
                Fact("draft", edit.Draft, "local-editor");
                Fact("draft-modified", edit.Modified.ToString(), "local-editor");
                Fact("draft-conflict", edit.Conflict.ToString(), "local-editor");
                Fact("draft-focused", edit.Focused.ToString(), "local-editor-not-native-selection");
                try
                {
                    Fact("draft-valid", edit.Valid.ToString(), "local-parser");
                }
                catch (Exception ex)
                {
                    Fact("draft-valid", "", "local-parser", UiObservationStatus.Failed, ex.GetType().Name);
                }
            }
            else
            {
                bool editorCapability = entry.Node.Kind == TextFieldWidget.Kind || entry.Node.Kind == NumberFieldWidget.Kind || entry.Node.Kind == StepperSliderWidget.Kind;
                Fact("draft", "", "local-editor", editorCapability ? UiObservationStatus.NotSampled : UiObservationStatus.Unsupported, editorCapability ? "no-editor-created" : "no-editor-capability");
            }

            foreach (var colour in entry.Node.Properties.ResolvedColours)
                Fact("colour." + colour.Key, colour.Value.Value.HasValue ? ColourText(colour.Value.Value.Value) : "none", colour.Value.Origin + ":" + colour.Value.Source, UiObservationStatus.Sampled, "last-property-resolution-not-current-pixels");
            Fact("font", entry.Node.Properties.ResolvedFont.Value.ToString(), entry.Node.Properties.ResolvedFont.Origin + ":" + entry.Node.Properties.ResolvedFont.Source, UiObservationStatus.Sampled, "last-property-resolution");
            foreach (var metric in entry.Node.Properties.ResolvedMetrics)
                Fact("metric." + metric.Key, metric.Value.Value.ToString(CultureInfo.InvariantCulture), metric.Value.Origin + ":" + metric.Value.Source, UiObservationStatus.Sampled, "last-property-resolution");
            output.Add(new UiObservedNode(entry.Id.Key, entry.Id.Path, entry.Node.Kind, entry.Node.Parent?.Id.Key, depth, Array.AsReadOnly(facts.ToArray())));
        }

        var hostFacts = new[]
        {
            new UiObservedFact("popup-binding", budget.Limit(ctx.Session.OpenPopupId ?? ""), source: "session",
                truncated: (ctx.Session.OpenPopupId?.Length ?? 0) > budget.Text),
            new UiObservedFact("popup-owner-node", ctx.Session.PopupTarget?.Node ?? "",
                ctx.Session.PopupTarget.HasValue ? UiObservationStatus.Sampled : UiObservationStatus.NotSampled,
                "session/popup-target", "no-target-recorded; legacy-string-or-no-popup"),
            new UiObservedFact("popup-owner-part", ctx.Session.PopupTarget?.Part ?? "",
                ctx.Session.PopupTarget.HasValue ? UiObservationStatus.Sampled : UiObservationStatus.NotSampled, "session/popup-target"),
            new UiObservedFact("popup-target-scope", ctx.Session.PopupTarget?.TargetScope ?? "", source: "session/popup-target"),
            new UiObservedFact("popup-item-key", ctx.Session.PopupTarget?.ItemKey as string ?? "", source: "session/popup-target"),
            new UiObservedFact("hover-help", budget.Limit(ctx.Session.HoverClaim ?? ""), source: "last-frame",
                truncated: (ctx.Session.HoverClaim?.Length ?? 0) > budget.Text),
            new UiObservedFact("hover-owner", ctx.Session.HoverClaimElement?.Key ?? "", source: "last-frame"),
            new UiObservedFact("last-layout-frame", observationLayoutFrame.ToString(CultureInfo.InvariantCulture), source: "last-arrangement"),
            new UiObservedFact("last-layout-time-utc", observationLayoutAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "", source: "last-arrangement")
        };
        return new UiHostObservation(ctx.Session.Identity, ctx.Source, ctx.Session.Frame, ctx.Session.DefinitionRevision,
            cachedSnapshot?.DefinitionRevision, DateTime.UtcNow, status, reason, truncated,
            Array.AsReadOnly(output.ToArray()), Array.AsReadOnly(hostFacts));
    }

    private bool ObservationHasPendingLayoutNotification(IUiBindings bindings)
    {
        foreach (var committed in committedRevisions)
            if (committed.Value != bindings.GetRevision(committed.Key) && (bindings.GetInvalidation(committed.Key) & (UiInvalidation.Measure | UiInvalidation.Structure)) != 0)
                return true;
        return false;
    }

    private static int ObservationDepth(UiNode node)
    {
        int depth = 0;
        for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent)
            depth++;
        return depth;
    }

    private static string RectText(Rect rect) => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", rect.x, rect.y, rect.width, rect.height);
    private static string ColourText(Color colour) => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", colour.r, colour.g, colour.b, colour.a);
    private static void ObserveWidget(PlacedEntry entry, UiWidgetContext ctx, UiObservationBudget budget, List<UiObservedFact> facts)
    {
        void Add(string name, string value, string source = "current-model", UiObservationStatus status = UiObservationStatus.Sampled, string reason = "")
            => facts.Add(new UiObservedFact(name, budget.Limit(value), status, source, reason, value.Length > budget.Text));
        if (entry.IsContainer)
        {
            Add("role", "structure", "declaration");
            return;
        }

        if (entry.Node.Kind == LineChartWidget.Kind)
        {
            ObserveChart(entry, ctx, budget, facts);
            return;
        }

        if (entry.Widget is IUiDescriptiveWidget descriptive)
        {
            UiDescription description = descriptive.Describe(ctx);
            Add("role", description.Role.ToString(), "wrapper");
            Add("name", description.Name, "wrapper");
            Add("text", description.Text, "wrapper");
            if (description.Count.HasValue)
                Add("count", description.Count.Value.ToString(CultureInfo.InvariantCulture));
            if (description.Available.HasValue) Add("available", description.Available.Value.ToString(), "wrapper-description");
            if (description.Range.HasValue)
            {
                UiRangeObservation range = description.Range.Value;
                Add("value", range.Value.ToString(CultureInfo.InvariantCulture));
                Add("min", range.Min.ToString(CultureInfo.InvariantCulture));
                Add("max", range.Max.ToString(CultureInfo.InvariantCulture));
                Add("finite", range.Finite.ToString());
                Add("fraction", range.Fraction.ToString(CultureInfo.InvariantCulture), "shared-clamped-fill-not-model");
            }
            if (description.UnknownReason != null)
                Add("description", "", "wrapper", UiObservationStatus.NotSampled, description.UnknownReason);
            return;
        }

        string kind = entry.Node.Kind;
        string role;
        switch (kind)
        {
            case ButtonWidget.Kind:
                role = "activation";
                break;
            case CheckboxWidget.Kind:
                role = "boolean";
                break;
            case SliderWidget.Kind:
                role = "range";
                break;
            case NumberFieldWidget.Kind:
                role = "numeric-edit";
                break;
            case TextFieldWidget.Kind:
                role = "text-edit";
                break;
            case StepperSliderWidget.Kind:
                role = "numeric-edit-range-step";
                break;
            case DropdownWidget.Kind:
                role = "selection-popup";
                break;
            case InputModeRowWidget.Kind:
                role = "selection";
                break;
            case TreeWidget.Kind:
                role = "row-activation-selection-expansion";
                break;
            default:
                Add("capability", "", "wrapper", UiObservationStatus.Unsupported, "custom-widget-has-no-read-projection");
                return;
        }

        Add("role", role, "wrapper");
        string labelKey = ReadAttribute(entry.Spec, "LabelKey");
        string label = labelKey.Length > 0 ? ctx.Translation.Translate(labelKey) : ReadAttribute(entry.Spec, "Label");
        if (kind != ButtonWidget.Kind) Add("name", label, "declaration/translation");
        if (kind == ButtonWidget.Kind)
        {
            string textKey = ReadAttribute(entry.Spec, "TextKey");
            Add("name", textKey.Length > 0 ? ctx.Translation.Translate(textKey) : ReadAttribute(entry.Spec, "Text"), "declaration/translation");
            return;
        }

        string binding = ReadAttribute(entry.Spec, "Bind");
        if (binding.Length == 0)
            binding = entry.Spec.Id;
        Add("binding", binding, "declaration");
        if (kind == SliderWidget.Kind || kind == NumberFieldWidget.Kind || kind == StepperSliderWidget.Kind)
        {
            Add("min", AtomVocabulary.ReadFloat(entry.Spec, "Min", 0).ToString(CultureInfo.InvariantCulture), "wrapper/declaration-default");
            Add("max", AtomVocabulary.ReadFloat(entry.Spec, "Max", 1).ToString(CultureInfo.InvariantCulture), "wrapper/declaration-default");
        }

        if (kind != TreeWidget.Kind)
            Add("writable", ctx.Bindings.IsWritable(binding).ToString(), "current-binding");
        if (kind == TreeWidget.Kind)
        {
            foreach (string part in new[]
            {
                "ActionBind",
                "DisclosureBind",
                "CheckBind"
            }

            )
                Add("part." + part, ReadAttribute(entry.Spec, part), "declaration");
        }

        if (kind == DropdownWidget.Kind || kind == InputModeRowWidget.Kind)
        {
            if (entry.Node.Selection.HasValue)
            {
                UiSelectionObservation selection = entry.Node.Selection.Value;
                Add("selection.index", selection.Index.ToString(CultureInfo.InvariantCulture), "last-wrapper-selection");
                Add("selection.missing", selection.Missing.ToString(), "last-wrapper-selection");
                Add("selection.selectable", selection.Selectable.ToString(), "last-wrapper-selection");
            }
            else Add("selection", "", "last-wrapper-selection", UiObservationStatus.NotSampled, "wrapper-has-not-observed-selection");
        }
        if (kind == DropdownWidget.Kind)
        {
            ObserveDropdownOptions(entry, ctx, budget, facts, binding);
        }

        if (kind == InputModeRowWidget.Kind)
        {
            ObserveModeOptions(entry, ctx, budget, facts, binding);
        }

        if (ctx.Bindings is IUiTypedChoices typed && typed.TryGetTypedValue(binding, out object? model))
        {
            if (model == null || model is string || model is bool || model is float || model is int || model is double || model is Enum)
                Add("value", model == null ? "null" : Convert.ToString(model, CultureInfo.InvariantCulture) ?? "");
            else if (model is IReadOnlyList<UiTreeRow> rows)
            {
                Add("count", rows.Count.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < Math.Min(rows.Count, budget.Items); i++)
                {
                    UiTreeRow row = rows[i];
                    string prefix = "row." + i + ".";
                    Add(prefix + "key", row.Key);
                    Add(prefix + "text", row.TextKey.Length > 0 ? ctx.Translation.Translate(row.TextKey) : row.Text);
                    Add(prefix + "selected", row.Selected.ToString());
                    Add(prefix + "expanded", row.Expanded.ToString());
                    Add(prefix + "expandable", row.Expandable.ToString());
                    Add(prefix + "checked", row.Checked.ToString());
                    Add(prefix + "checkable", row.Checkable.ToString());
                    Add(prefix + "level", row.Depth.ToString(CultureInfo.InvariantCulture));
                }
                if (rows.Count > budget.Items)
                    facts.Add(new UiObservedFact("rows", "", reason: "item-budget", truncated: true));
            }
            else
                Add("value", "", "current-model", UiObservationStatus.Unsupported, "non-scalar-model; no-arbitrary-ToString-or-object-walk");
        }
        else
        {
            bool providerSupportsRead = ctx.Bindings is IUiTypedChoices;
            Add("value", "", "current-model", providerSupportsRead ? UiObservationStatus.NotFound : UiObservationStatus.Unsupported,
                providerSupportsRead ? "value-binding-not-found" : "binding-provider-has-no-typed-read-seam");
        }
    }

    private static void ObserveChart(PlacedEntry entry, UiWidgetContext ctx, UiObservationBudget budget, List<UiObservedFact> facts)
    {
        void Add(string name, string value, string source = "current-model", UiObservationStatus status = UiObservationStatus.Sampled, string reason = "")
            => facts.Add(new UiObservedFact(name, budget.Limit(value), status, source, reason, value.Length > budget.Text));
        Add("role", "chart", "wrapper");
        Add("editable", ((LineChartWidget)entry.Widget!).AllowsPointEditing.ToString(), "wrapper/declaration");
        string key = ReadAttribute(entry.Spec, "Bind");
        if (key.Length == 0)
            key = entry.Spec.Id;
        string staticPoints = ReadAttribute(entry.Spec, "Points");
        bool useBinding = ReadAttribute(entry.Spec, "Bind").Length > 0 || staticPoints.Length == 0;
        if (useBinding && ctx.Bindings.TryGet(key, out IReadOnlyList<Vector2> points))
        {
            Add("count", points.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Math.Min(points.Count, budget.Items); i++)
                Add("point." + i, string.Format(CultureInfo.InvariantCulture, "{0},{1}", points[i].x, points[i].y));
            if (points.Count > budget.Items)
                facts.Add(new UiObservedFact("points", "", reason: "item-budget", truncated: true));
            string keysBinding = ReadAttribute(entry.Spec, "PointKeysBind");
            if (keysBinding.Length > 0 && ctx.Bindings.TryGet(keysBinding, out IReadOnlyList<string> keys))
                for (int i = 0; i < Math.Min(keys.Count, budget.Items); i++)
                    Add("point-key." + i, keys[i]);
        }
        else
            Add("points", ReadAttribute(entry.Spec, "Points"), "static-declaration");
    }

    private static void ObserveOption(UiWidgetContext ctx, UiObservationBudget budget, List<UiObservedFact> facts,
        string binding, int index, string text, object? value, string source)
    {
        string prefix = "option." + index + ".";
        facts.Add(new UiObservedFact(prefix + "text", budget.Limit(text), source: source, truncated: text.Length > budget.Text));
        facts.Add(new UiObservedFact(prefix + "type", value?.GetType().FullName ?? "null", source: "actual-value-type"));
        bool scalar = value == null || value is string || value is bool || value is float || value is int || value is double || value is Enum;
        string scalarValue = scalar ? (value == null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "") : "";
        facts.Add(new UiObservedFact(prefix + "value", budget.Limit(scalarValue),
            scalar ? UiObservationStatus.Sampled : UiObservationStatus.Unsupported, source,
            scalar ? "typed-value-not-label; read-only-no-roundtrip" : "opaque-typed-value; no-detached-key-seam; not-guessed-from-label",
            scalarValue.Length > budget.Text));
        facts.Add(new UiObservedFact(prefix + "selectable", (AtomVocabulary.WritableOf(ctx, binding) != false).ToString(),
            source: "shared-selection-writability"));
    }

    private static void ObserveDropdownOptions(PlacedEntry entry, UiWidgetContext ctx, UiObservationBudget budget, List<UiObservedFact> facts, string binding)
    {
        string optionsKey = ReadAttribute(entry.Spec, "OptionsBind");
        bool typedValue = ctx.Bindings is IUiTypedChoices choiceProvider
            && choiceProvider.TryGetValueType(binding, out Type declaredType) && declaredType != typeof(string);
        bool dynamicOptions = optionsKey.Length > 0 || typedValue;
        if (optionsKey.Length == 0) optionsKey = binding;
        if (dynamicOptions && ctx.Bindings is UiBindings concrete
            && concrete.TryReadObservationOptions(optionsKey, budget.Items, out var options, out int count))
        {
            facts.Add(new UiObservedFact("option-count", count.ToString(CultureInfo.InvariantCulture), source: "current-model"));
            for (int i = 0; i < options.Count; i++)
            {
                object? option = options[i];
                if (option is string text) ObserveOption(ctx, budget, facts, binding, i, text, text, "current-options");
                else if (option is UiOption pair)
                    ObserveOption(ctx, budget, facts, binding, i, pair.Text.Length > 0 ? pair.Text : pair.Value, pair.Value, "current-options");
                else if (option is IUiChoice choice)
                    ObserveOption(ctx, budget, facts, binding, i, choice.Text, choice.BoxedValue, "current-typed-options");
                else facts.Add(new UiObservedFact("option." + i, "", UiObservationStatus.Unsupported, reason: "unknown-option-shape"));
            }
            if (count > options.Count) facts.Add(new UiObservedFact("options", "", reason: "item-budget", truncated: true));
        }
        else if (!dynamicOptions)
        {
            int total = 0;
            int kept = 0;
            for (int slot = 1; slot <= 16; slot++)
            {
                string text = ReadAttribute(entry.Spec, "Option" + slot);
                if (text.Length == 0) continue;
                total++;
                if (kept == budget.Items) continue;
                string value = ReadAttribute(entry.Spec, "Value" + slot);
                ObserveOption(ctx, budget, facts, binding, kept, text, value.Length > 0 ? value : text, "static-options");
                facts.Add(new UiObservedFact("option." + kept + ".slot", slot.ToString(CultureInfo.InvariantCulture), source: "declaration"));
                kept++;
            }
            facts.Add(new UiObservedFact("option-count", total.ToString(CultureInfo.InvariantCulture), source: "static-options"));
            if (total > kept) facts.Add(new UiObservedFact("options", "", reason: "item-budget", truncated: true));
        }
        else
        {
            bool providerSupportsRead = ctx.Bindings is UiBindings;
            facts.Add(new UiObservedFact("options", "", providerSupportsRead ? UiObservationStatus.NotFound : UiObservationStatus.Unsupported,
                "current-model", providerSupportsRead ? "options-binding-not-found" : "provider-has-no-bounded-options-read"));
        }
    }

    private static void ObserveModeOptions(PlacedEntry entry, UiWidgetContext ctx, UiObservationBudget budget, List<UiObservedFact> facts, string binding)
    {
        bool isTyped = ctx.Bindings is IUiTypedChoices choices && choices.TryGetValueType(binding, out Type type) && type != typeof(string);
        if (isTyped && ctx.Bindings is UiBindings concrete
            && concrete.TryReadObservationOptions(binding, budget.Items, out var options, out int count))
        {
            facts.Add(new UiObservedFact("option-count", count.ToString(CultureInfo.InvariantCulture), source: "current-model"));
            for (int i = 0; i < options.Count; i++)
            {
                string suffix = (i + 1).ToString(CultureInfo.InvariantCulture);
                string titleKey = ReadAttribute(entry.Spec, "TitleKey" + suffix);
                string title = ReadAttribute(entry.Spec, "Title" + suffix);
                if (options[i] is not IUiChoice choice)
                {
                    facts.Add(new UiObservedFact("option." + i, "", UiObservationStatus.Unsupported, reason: "unknown-typed-option-shape"));
                    continue;
                }
                if (titleKey.Length > 0) title = ctx.Translation.Translate(titleKey);
                else if (title.Length == 0) title = choice.Text;
                ObserveOption(ctx, budget, facts, binding, i, title, choice.BoxedValue, "current-typed-options");
            }
            if (count > options.Count) facts.Add(new UiObservedFact("options", "", reason: "item-budget", truncated: true));
        }
        else if (!isTyped)
        {
            int total = 0;
            int kept = 0;
            for (int slot = 1; slot <= 8; slot++)
            {
                string value = ReadAttribute(entry.Spec, "Value" + slot);
                if (value.Length == 0) continue;
                total++;
                if (kept == budget.Items) continue;
                string titleKey = ReadAttribute(entry.Spec, "TitleKey" + slot);
                string title = titleKey.Length > 0 ? ctx.Translation.Translate(titleKey) : ReadAttribute(entry.Spec, "Title" + slot);
                ObserveOption(ctx, budget, facts, binding, kept, title.Length > 0 ? title : value, value, "static-options/translation");
                facts.Add(new UiObservedFact("option." + kept + ".slot", slot.ToString(CultureInfo.InvariantCulture), source: "declaration"));
                kept++;
            }
            facts.Add(new UiObservedFact("option-count", total.ToString(CultureInfo.InvariantCulture), source: "static-options"));
            if (total > kept) facts.Add(new UiObservedFact("options", "", reason: "item-budget", truncated: true));
        }
        else facts.Add(new UiObservedFact("options", "", UiObservationStatus.Unsupported, "current-model", "provider-has-no-bounded-options-read"));
    }
}
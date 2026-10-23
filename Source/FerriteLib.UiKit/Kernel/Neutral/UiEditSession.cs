using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Parsing is a value seam; character editing and selection remain native responsibilities.</summary>
public delegate bool UiEditParser<T>(string text, out T value);
public interface IUiEditState
{
    string Draft { get; }

    bool Focused { get; }

    bool Modified { get; }

    bool Conflict { get; }

    bool Valid { get; }

    void Suspend();
}

/// <summary>One local editor's draft and observed model basis. It never owns the business value.</summary>
public sealed class UiEditSession<T> : IUiEditState
{
    private Func<T, string> format;
    private UiEditParser<T> parse;
    private bool observed;
    private T model = default !;
    private string basisText = "";
    private string? publishedText;
    private bool automaticSuspended;
    public UiEditSession(Func<T, string> format, UiEditParser<T> parse)
    {
        this.format = format ?? throw new ArgumentNullException(nameof(format));
        this.parse = parse ?? throw new ArgumentNullException(nameof(parse));
    }

    public string Draft { get; private set; } = "";
    public bool Focused { get; private set; }
    public bool Modified => publishedText != null || !string.Equals(Draft, basisText, StringComparison.Ordinal);
    public bool Conflict { get; private set; }
    public bool Valid => parse(Draft, out _);
    public bool HasDraft => Modified || Conflict;

    public void Configure(Func<T, string> formatter, UiEditParser<T> parser)
    {
        format = formatter ?? throw new ArgumentNullException(nameof(formatter));
        parse = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public void Observe(T current)
    {
        bool changed = observed && !EqualityComparer<T>.Default.Equals(model, current);
        model = current;
        observed = true;
        if (!Modified && !Conflict)
        {
            Draft = basisText = format(current);
            publishedText = null;
        }
        else if (changed)
        {
            Conflict = true;
            publishedText = null;
        }
    }

    public void Focus() => Focused = true;
    public void Suspend() => Focused = false;
    /// <summary>Native composition can update the visible draft, but cannot publish it.</summary>
    public void Input(string text, bool writable, bool composing, bool live, Action<T> request, Func<T> readModel)
    {
        if (!writable)
            return;
        bool changed = !string.Equals(Draft, text, StringComparison.Ordinal);
        Draft = text ?? "";
        if (changed)
            automaticSuspended = false;
        if (live && !composing && !Conflict && !automaticSuspended)
            Publish(false, request, readModel);
    }

    /// <summary>Explicit confirmation may replace an observed conflicting model; blur may not.</summary>
    public bool Confirm(bool explicitConfirmation, Action<T> request, Func<T> readModel)
    {
        Focused = false;
        if (!Valid || (!explicitConfirmation && (Conflict || automaticSuspended)))
            return false;
        Publish(true, request, readModel, explicitConfirmation && automaticSuspended);
        return true;
    }

    public void Cancel(T latestModel)
    {
        model = latestModel;
        observed = true;
        Draft = basisText = format(latestModel);
        publishedText = null;
        automaticSuspended = false;
        Conflict = false;
        Focused = false;
    }

    /// <summary>A local combination's explicit selection replaces only this editor, after one request.</summary>
    public void TakeOver(T value, Action<T> request, Func<T> readModel)
    {
        // The attempted takeover value is not the old draft. Suppress automatic work after failure,
        // but leave the draft's own publication history intact for a later explicit confirmation.
        automaticSuspended = true;
        request(value);
        EndSubmission();
        Cancel(readModel());
    }

    public bool TryStepBase(T currentModel, out T value)
    {
        if (HasDraft)
            return parse(Draft, out value);
        value = currentModel;
        return true;
    }

    private void Publish(bool end, Action<T> request, Func<T> readModel, bool explicitNewAttempt = false)
    {
        bool pending = explicitNewAttempt || ((Modified || Conflict) && (Conflict || !string.Equals(publishedText, Draft, StringComparison.Ordinal)));
        if (pending && parse(Draft, out T candidate))
        {
            publishedText = Draft;
            automaticSuspended = true;
            request(candidate);
            // Dispatch completed. A later display read is not a failed business request.
            automaticSuspended = false;
            observed = false;
            if (end)
            {
                EndSubmission();
                Cancel(readModel());
                return;
            }
            // A normal return is not acknowledgement. Read actual model without replacing a Live draft.
            model = readModel();
            observed = true;
            Conflict = false;
            automaticSuspended = false;
        }

        if (end)
        {
            EndSubmission();
            Cancel(readModel());
        }
    }

    private void EndSubmission()
    {
        basisText = Draft;
        publishedText = null;
        automaticSuspended = false;
        Conflict = false;
        Focused = false;
        observed = false;
    }
}

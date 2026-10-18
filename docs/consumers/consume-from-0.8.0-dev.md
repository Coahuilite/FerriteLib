# Consume the 0.8.0 S0/S1 development candidate

Use a fixed Dev package from this line, not a moving sibling build output. Only FerriteLib ships the
runtime DLL; a consumer references it with `Private=false` and does not bundle another copy.

```csharp
if (!FerriteLibVersion.Require(new Version(0, 8, 0), new Version(0, 9, 0),
    "your.package.id", out string diagnostic))
    throw new InvalidOperationException(diagnostic);
```

The old `[0.7.0,0.8.0)` range excludes this candidate. Explicitly adopt the new development input, compile,
then run the consumer's own harness and native UI checks; widening the range alone is not a migration.

Existing Button/Slider manifest and typed binding entries remain usable. Button now activates on valid
primary release rather than delegating timing to the native compound button. Slider selects immediately
through shared range behavior and does not edit a neighboring NumberField draft. Keyboard/edit defaults
are unchanged by this slice. Other 0.8 requirements are not implied by this candidate.

See [S0/S1 contract](../development/0.8/s01-contract.md) for per-property remove/following, independent
composition, headless entry points, capture ownership and error handling. New types are public-unstable;
keep the exact selected source/configuration/package identity with your development evidence.

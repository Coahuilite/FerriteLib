# Layered input, images and window decoration

## Overlap works with ordinary default controls

Put a large ordinary button and a smaller button later in an Overlay. The later button is visually above
and receives a qualified primary gesture where its actual input region overlaps. No declaration-order
workaround, custom subclass or input registration is needed. Pure image/text decoration does not acquire an opaque hit box. L2 is approved: a visible configured operation keeps its actual region as an obstruction while disabled/unavailable, but cannot activate or begin capture. Removing/hiding it or omitting an operation does not add obstruction. Existing capture is never handed to an underlying
target; release outside its own region, lost eligibility, removal or Close follows the common end rules.

The engine projects current declared input geometry through the same arranged scope/scroll transform and
effective clip before native Draw. The neutral production UiPointerLayers decision resolves content
coverage; UiControls reads that result and the existing interaction session remains the sole capture owner.
IMGUI calls keep their original order on every event. There is no second Draw, fake Event replay or new
per-widget dispatch algorithm. Popup/F3 priority stays above content, and content coverage does not turn an
ordinary nonoverlapping outside click into popup-style edit suspension. A covered native editor cannot
steal that pointer input, but still paints on Repaint and retains its existing keyboard/draft contract.

## Precise custom overlap needs real geometry

IUiWidget still has only its existing required members. A custom that wants the new precise content
coverage guarantee implements optional IUiPointerLayout and returns its actual operation regions in
local coordinates. Reuse the same geometry in Draw; configured operation geometry remains present during temporary disable. Execution eligibility goes separately to the shared interaction, not into a second behavior state machine. For example, if a custom has one30px control100px from its left edge:

```csharp
private static UiLayoutRect ButtonBounds(UiLayoutRect outer)
    => new(outer.X + 100, outer.Y + 10, 30, 30);

public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
{
    yield return new UiPointerRegion(ButtonBounds(bounds));
}
```

A ctx.Child keeps its independent interaction/capture identity; layer queries resolve its owning widget through the real Parent chain rather than treating the child as a new paint layer. Draw uses ButtonBounds with its allocated origin and supplies current execution eligibility to the existing UiControls activation/shared
request path. It does not implement topmost selection or capture. An empty sequence means decoration;
UiRowBand.PointerRegions exposes the same part geometry used by the retained row visual for custom row
wrappers. All default interactive wrappers provide their real field/track/option/row/point geometry.

An old custom without the optional capability keeps its previous entry points, nonoverlap and popup
behavior, without extra registration. Its unknown private/native input cannot be inferred on a first
overlapping input frame: precise occlusion against it is not promised. The library neither guesses its
whole outer rectangle is opaque nor claims to intercept arbitrary raw GUI. This is a condition of the
new precise-overlap capability, not a new prerequisite for existing custom use.

## Verification boundary

The fixed baseline realHost test recorded the lower large button stealing the upper lock gesture. The
same realEvent route now selects the upper target. Tests also cover first-frame live eligibility,
decoration pass-through, shared custom geometry, release/cancel/removal/Close, nested clip and current
scroll offsets, native-editor coverage/Repaint and ordinary outside-click publication. Existing full
popup/F3 and all control regressions remain. Pure tests compile the actual neutral decision source.
Native1.6 paint/input acceptance remains separate; fixture geometry and event records do not certify pixels.

## Use the default image before writing a custom renderer

Register supplier-owned Texture/Texture2D/RenderTexture objects in the actual execution.Images scope.
Only the resulting UiImage token crosses into neutral bindings. The supplier creates/updates/destroys
textures; the adapter owns references and drops them without Destroy. Tokens from another scope do not
resolve. Source replacement uses a new token; notify ordinary layout dependencies when dimensions change.
Default/empty tokens preserve the missing slot and unavailable description; invalid sizes do not paint.
A live render target can update its pixels without changing layout or adding a second resource.

```csharp
var portrait = execution.Images.Add(suppliedRenderTarget);
var bindings = new UiBindings();
bindings.BindReadOnly("portrait", () => portrait);
var image = new UiElementSpec("portrait-view", "display/image", new Dictionary<string, UiAttributeValue>
{
    ["Bind"] = UiAttributeValue.Literal("portrait"),
    ["AspectHeight"] = UiAttributeValue.Boolean(true),
    ["Fit"] = UiAttributeValue.Literal("Contain")
});
var page = UiLayoutManifest.Create("consumer/portraits", new[] { image });
```

The equivalent ordinary declaration is:

```xml
<UiPage Schema="2" Source="consumer/portraits">
  <Widget Id="portrait-view" Kind="display/image" Bind="portrait"
          AspectHeight="true" Fit="Contain" ImageAlignX="Center" ImageAlignY="Top" />
</UiPage>
```

`Fit` is `Contain` (complete image), `Cover` (fill and clip to slot), or `Stretch` (explicit distortion).
`ImageAlignX` is `Left|Center|Right`; `ImageAlignY` is `Top|Center|Bottom`. These align the picture **inside**
its allocated rectangle, not the widget in its parent. Defaults remain Contain/Center/Center. Unsupported
names, including numeric enum aliases, are authoring errors. `AspectHeight=true` measures exactly
`allocated width × source height / source width`, with no added padding; explicit `Height` wins. Without
this opt-in the old source-height-plus-two-padding measure remains. AspectHeight uses the density row fallback for a missing or invalid-size source; the legacy measure still follows a positive source height (even when its width is invalid). Explicit Height reserves its authored slot regardless of source state.

`UiImageLayout` is the actual platform-free geometry used by `UiThemeDraw.Image`, not a fixture copy.
Custom visuals use the same UiImageOptions geometry with an explicit IUiDrawing. Image and FitImage
keep their Contain/Center defaults. Cover uses a balanced paint-only GUI group and
restores the tint even if drawing throws. The image is decoration: no input region or capture is added.
All image bindings now carry UiImage; there is no parallel Texture2D core binding route.

## Add a background to the standard window

The normal sealed `UiPageWindow` accepts immutable configuration, without replacing its structure:

```csharp
var backdrop = new UiWindowBackground("backdrop"); // approved Contain + Center
var page = UiLayoutManifest.CreateWithBackground("consumer/page", roots, backdrop);
var window = new UiPageWindow(key, page, bindings, theme, translation,
    title, closeText, noticeText);
window.Background = new UiWindowBackground("backdrop", new UiImageOptions(UiImageFit.Cover));
window.Background = null; // restore the original shell, not the manifest's backdrop
```

```xml
<UiPage Schema="2" Source="consumer/page" BackgroundBind="backdrop"
        BackgroundFit="Contain" BackgroundAlignX="Center" BackgroundAlignY="Center">
  <!-- ordinary content -->
</UiPage>
```

Bind `backdrop` through the same typed value binding as the image above. Without an instance override, Background reads the Host's current committed definition: successful UpdateDefinition and document reload replace or remove it. Prepare validates its binding type; a refused or rolled-back candidate never becomes the effective background. The instance Background setter overrides the definition, including explicit null to close it; subsequent definition updates do not undo that override. Instance changes do not
mutate that immutable definition or another window. The supplier can replace or clear the bound value;
no texture is destroyed on swap, unbind, host recovery, or window Close. An unavailable source leaves the
original base and chrome visible. Application animation/resource scheduling remains application-owned.

BG1 is user-approved: **Contain, centered, expose the existing base colour when aspect ratios differ**.
Cover is explicit. The shell paints workspace base → image → accent/title/subtitle/close chrome → page
content → popup. No later opaque whole-window base hides the image. Ordinary content surfaces may still
cover their own areas. The image covers the whole zero-margin window face, is clipped there for Cover,
and never adds an input target; the existing title, close, content and popup keep their behavior.
Unconfigured windows are unchanged. Subclass authors can override the narrow `DrawBackground` paint hook;
they do not replace default definitions or acquire a new layout/input authority.

## Added versus retained entry points

| Use | This package | Caller responsibility |
| --- | --- | --- |
| Default controls overlap | Shared precise geometry and topmost arbitration; L2 approved unavailable-operation obstruction | Ordinary declarations unchanged |
| Custom private hit parts | Optional IUiPointerLayout, shared neutral arbitration, Child-owner resolution | Describe actual regions; keep input on shared interaction |
| Existing Image | Same defaults, source-height measure and empty slot; neutral scoped token | Existing bindings remain legal |
| Image variants | Fit, inner alignment, explicit width-aspect measure; native resources via scoped tokens | Supply a current resource and normal invalidation |
| Standard window background | Manifest/program/instance configuration, approved Contain default | Supply UiImage from the window.Images scope; own texture lifetime |
| Animation/media/whole-skin replacement | Not introduced | Application/existing extension boundary |

The actual Host/native-outlet fixture asserts fit/clip/group cleanup, old versus explicit measurement,
RenderTexture identity, swap/null/invalid size, standard-window instance isolation, base→image→chrome
order, real content input, popup paint order and normal Close. These records validate calls and geometry,
not a screenshot. Native 1.6 pixels, texture material behavior and IMGUI clipping remain pending an
authorized render/game acceptance surface; this task does not launch the game to obtain one.

A supplier getter exception is not a missing image. It enters the existing shell failure boundary with its original exception retained, releases the page host, restores GUI font/colour and preserves chrome/close. The failed pass does not draw a notice or continue page content; the next pass shows the existing terminal page-unavailable notice without retrying the getter. TryRead=false or a null source remains the normal no-image case. Cover's local group is balanced even when native drawing throws.

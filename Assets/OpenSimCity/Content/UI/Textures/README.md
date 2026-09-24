# HUD texture provenance

`hud-panel-grain.png` is a first-party RGBA texture generated for OpenSimCity with
OpenAI ImageGen on 2026-09-06. It contains no third-party marks or source imagery.

Prompt summary: seamless near-black blue-gray game HUD panel surface, transparent
background, extremely subtle fine grain and faint brushed-metal micro-texture, no
text, icons, borders, gradients, lighting or vignette.

SHA-256: `2ba3df232950bd6352972a8c145aff17d7e7e3944b2da109b92dec42bd81d724`

## Graphite material revision — 2026-09-09

`hud-graphite-v2.png` is a first-party material tile generated with the built-in OpenAI ImageGen tool. It replaces the blue-black surface in runtime USS without changing square geometry.

Prompt: square seamless matte graphite powder-coated aluminum UI material, neutral charcoal around RGB 40/42/43, fine restrained micrograin and barely visible horizontal machining, evenly lit and opaque; no objects, panels, borders, rounded corners, stripes, text, icons, logos, vignette or highlights.

SHA-256: `8f08f08504c874922e011449b152f4cd271c8697d6445f3f4f336014d0431599`

Displayed with a 640px repeat, neutral tint and distinct 1px upper/lower edge lighting. The accent side rule belongs to contextual notifications only.

### Exact built-in generation prompt

Generate a square, seamless tileable UI MATERIAL TEXTURE ONLY for a premium realistic city-building game's flat square-corner HUD panels. An even matte graphite powder-coated aluminum surface, neutral warm charcoal gray (approximately RGB 40,42,43), very fine restrained tactile micrograin, a faint horizontal machining grain barely perceptible, tiny luminance variation around 3 percent. Orthographic flat scanned material, evenly lit, opaque edge-to-edge square. It must read as a refined material underneath crisp white typography, not as a navy window. NO objects, NO mockup, NO panels, NO borders, NO rounded corners, NO side stripes, NO icons, NO typography, NO text, NO logos, NO vignette, NO dramatic scratches, NO glossy highlights, NO directional lighting, NO large dark patches or gradients. A clean uniform material swatch only.

## Title city render

`title-city.png` is an unedited 2560×1440 screenshot rendered by the actual OpenSimCity macOS player with `-osc-showcase -osc-hide-ui`. It uses the project's own 3D assets and lighting. It is a title-screen art backdrop, not a loaded save or a representation of current simulation population. The directional text scrim is drawn separately in UI Toolkit by `TitleScreenShade`.

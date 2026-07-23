# Icon Authoring

`assets/icon.svg` is the editable source for the package icon.
`assets/icon.png` is the 256×256 Thunderstore asset generated from that SVG.
Do not edit the PNG by hand.

## Regenerating the PNG

Render the SVG in a browser at 1024×1024, then resize it to 256×256 with
high-quality bicubic interpolation. The icon uses only vector geometry and
does not depend on an external font.

## Verification

1. Confirm the SVG viewport and background are 256×256.
2. Confirm the PNG is 256×256 RGBA.
3. Inspect the PNG at native size for smooth edges and clear separation between
   the waveform, route, and headphones.
4. Keep the SVG and generated PNG together in the same commit.

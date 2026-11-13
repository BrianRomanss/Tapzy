# Tapzy art: what to generate and where to put it

Style: **Bold Outline**. Saturated flat colours, thick black outlines, hard shadows.

## How it works

1. Generate an image with the prompt below.
2. Save it as a PNG with **exactly** the file name shown.
3. Drop it into `C:\UnityProjects\Tapzy\Assets\Tapzy\Resources\Art\`.
4. Open Unity (or rebuild). The game picks the file up by name. Anything you have not added yet keeps its code-drawn stand-in, so you can add art a few files at a time.

Rules that make the art usable:

- **Transparent background** for everything except the two backgrounds. If your tool cannot do transparency, generate on a flat pure green (`#00FF00`) background and tell Claude; it can cut the green out.
- **No text in any image** except the logo. The game draws all labels itself.
- **One object per image, centred, nothing cropped**, with a little empty margin.
- Generate everything in **one session with the same style block**, so the set matches. If one image looks different from the rest, regenerate it.

## Style block (paste at the start of every prompt)

> Mobile game asset, bold cartoon style, very thick uniform black outline, flat saturated colours with one simple lighter highlight and one darker shade, no gradients, no texture, no text, single object centred, front view, transparent background.

Colour palette to mention when a prompt names a colour: yellow `#FFD400`, pink `#FF3D7F`, cyan `#3DE0FF`, green `#7CFF4D`, orange `#FF9A1F`, purple `#B07CFF`, blue `#1F5BFF`, navy `#16214F`.

---

## Batch 1: interface (do these first, 22 images)

Generate these, drop them in, and look at the game before doing batch 2. If the style is wrong it is cheaper to find out now.

### Backgrounds (opaque, no transparency)

| File | Size | Prompt (after the style block, but opaque) |
|---|---|---|
| `ui_bg_hub.png` | 1080 × 1920 | Portrait background pattern, bright blue `#1F5BFF` with slightly lighter diagonal stripes and a few faint large star and circle shapes, calm and low contrast so buttons stand out, no objects, no outline. |

### Logo

| File | Size | Prompt |
|---|---|---|
| `ui_logo.png` | 1024 × 400 | The word "TAPZY" as a game logo, chunky slanted cartoon letters, yellow `#FFD400` fill, thick black outline, hard black drop shadow, slight upward tilt. |

### Buttons and panels (blank, no text, no icon)

These get stretched to many sizes, so keep them plain: a simple rounded rectangle with even corners and nothing in the middle.

| File | Size | Prompt |
|---|---|---|
| `ui_button_yellow.png` | 512 × 192 | Blank rounded-rectangle game button, yellow `#FFD400`, thick black outline, thin lighter strip along the top edge, darker strip along the bottom edge, completely empty middle. |
| `ui_button_green.png` | 512 × 192 | Same button, green `#7CFF4D`. |
| `ui_button_red.png` | 512 × 192 | Same button, pink-red `#FF3D7F`. |
| `ui_button_blue.png` | 512 × 192 | Same button, navy blue `#2A3A7A`. |

### Icons

| File | Size | Prompt |
|---|---|---|
| `ui_coin.png` | 256 × 256 | Gold coin facing forward, yellow `#FFD400` with orange rim, a simple star embossed in the centre. |
| `ui_gift.png` | 512 × 512 | Closed gift box, pink with a yellow ribbon and bow. |
| `icon_home.png` | 256 × 256 | Simple house icon, white. |
| `icon_ranks.png` | 256 × 256 | Trophy cup icon, yellow. |
| `icon_redeem.png` | 256 × 256 | Gift card icon with a ribbon, green. |
| `icon_profile.png` | 256 × 256 | Round smiling face avatar icon, cyan. |

### Game tiles (the big pictures on the home screen)

Square, 512 × 512, object fills most of the frame.

| File | Prompt |
|---|---|
| `tile_lane_rush.png` | Small yellow cartoon car seen from behind, racing up a three-lane road toward the viewer's top, two pink road barriers ahead, speed lines. |
| `tile_colour_snap.png` | A cartoon finger tapping a pink circle, with a cyan square and a yellow triangle floating beside it, small impact stars. |
| `tile_juice_slash.png` | An orange sliced cleanly in two halves by a white slash streak, juice droplets flying. |
| `tile_bubble_burst.png` | Cluster of five shiny bubbles in pink, cyan, yellow and green, one bubble popping with a small burst. |
| `tile_blade_spin.png` | Round wooden target log seen from the front with three knives stuck in its edge, one more knife flying toward it. |
| `tile_block_fit.png` | Three chunky puzzle blocks made of squares (an L shape, a square, a line) in pink, cyan and yellow, fitting together. |
| `tile_gem_rush.png` | Three cut gems in a row: pink heart-cut, cyan diamond-cut, green square-cut, with sparkle stars. |

---

## Batch 2: in-game art (after batch 1 looks right)

These names are reserved. Claude wires them into each game once you confirm the batch 1 style. Each is 256 × 256 unless noted.

**Game backgrounds** (opaque, 1080 × 1920, dark and low contrast so objects stand out). These are already wired: drop them in and they show.

| File | Prompt |
|---|---|
| `bg_lane_rush.png` | Night city road scene, dark navy, simple building silhouettes at the sides, empty middle. |
| `bg_colour_snap.png` | Dark navy backdrop with faint scattered shape outlines. |
| `bg_juice_slash.png` | Dark wooden wall backdrop, simple planks, navy-tinted. |
| `bg_bubble_burst.png` | Dark underwater backdrop, navy to deep blue, a few faint bubbles. |
| `bg_blade_spin.png` | Dark circus-tent backdrop, navy with faint stripes radiating from the centre. |
| `bg_block_fit.png` | Dark navy backdrop with a faint grid. |
| `bg_gem_rush.png` | Dark cave backdrop, navy, faint crystal shapes at the edges. |

**Lane Rush:** `lane_car.png` (yellow car from above, facing up), `lane_barrier.png` (pink and white striped road barrier from above, 512 × 256).

**Colour Snap:** `snap_circle.png`, `snap_square.png` (plain **white** shapes with black outline; the game tints them).

**Juice Slash:** `fruit_orange.png`, `fruit_apple.png`, `fruit_watermelon.png`, `fruit_lemon.png`, `fruit_grape.png`, `fruit_kiwi.png` (whole fruit), and `fruit_bomb.png` (round black bomb with a lit fuse and a pink warning skull mark).

**Bubble Burst:** `bubble.png` (one plain **white** glossy bubble with a highlight; the game tints it), `bubble_shooter.png` (small cannon seen from above, pointing up).

**Blade Spin:** `blade_knife.png` (knife pointing up, 128 × 512), `blade_target.png` (round wooden log slice from the front, 512 × 512).

**Block Fit:** `block_cell.png` (one plain **white** bevelled square block; the game tints it).

**Gem Rush:** `gem_0.png` to `gem_5.png` (six different cut gems: pink heart, cyan diamond, green square, yellow star, purple hexagon, orange teardrop).

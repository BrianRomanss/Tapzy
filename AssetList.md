# Tapzy asset list

Everything Tapzy needs to stop looking and sounding like a prototype: 2D art, 3D models and sound, for the hub and all 20 games. Written 13 November 2026.

Today the whole app is drawn in code (boxes, discs, text) and every sound is a generated beep. Nothing on this list is required for the game to run; each file replaces a stand-in.

## How to use this list

- **2D art** goes in `Assets/Tapzy/Resources/Art/` as PNG, named exactly as shown. Prompts and the style block for the first batches are in `ArtPrompts.md`; this list uses the same names.
- **Status** tells you what happens when you drop the file in:
  - **Wired**: shows up straight away.
  - **Reserved**: the name is agreed, but the game does not load it yet. Ask Claude to wire that game.
  - **New**: proposed in this list; needs wiring.
- **Tint** means draw it plain white with a black outline; the game colours it.
- All sprites: transparent background, one object, centred, no text. Backgrounds are opaque 1080 × 1920.
- Default sprite size is 256 × 256 unless a size is given.

## Totals

| Kind | Files |
|---|---|
| Interface art | 51 |
| Game tiles (home screen) | 20 |
| Game backgrounds | 20 |
| In-game sprites | 150 |
| 3D models | 0 required, 20 optional |
| Sound effects | 133 |
| Music and jingles | 9 |

---

# Part 1: Interface (2D)

## Already listed in ArtPrompts.md (wired)

| File | Size | What it is |
|---|---|---|
| `ui_bg_hub.png` | 1080 × 1920 | Hub and splash background |
| `ui_logo.png` | 1024 × 400 | Tapzy logo |
| `ui_button_yellow.png` | 512 × 192 | Main button (Play again, Resume) |
| `ui_button_green.png` | 512 × 192 | Positive button (Watch ad, Claim) |
| `ui_button_red.png` | 512 × 192 | Danger button (Quit, Delete) |
| `ui_button_blue.png` | 512 × 192 | Secondary button |
| `ui_coin.png` | 256 × 256 | Coin |
| `ui_gift.png` | 512 × 512 | Daily reward gift box |
| `icon_home.png` | 256 × 256 | Tab bar: Home |
| `icon_ranks.png` | 256 × 256 | Tab bar: Ranks |
| `icon_redeem.png` | 256 × 256 | Tab bar: Redeem |
| `icon_profile.png` | 256 × 256 | Tab bar: Profile |

## New interface art

Panels are stretched, so keep them plain rounded rectangles with an empty middle. The importer already treats any `ui_panel_*` file as stretchable.

| File | Size | Where it is used |
|---|---|---|
| `ui_panel_dark.png` | 256 × 256 | Popups, list rows, pause and result panels |
| `ui_panel_light.png` | 256 × 256 | Raised rows, input fields |
| `ui_panel_tile.png` | 256 × 256 | Frame behind each game tile on Home |
| `ui_pill.png` | 256 × 128 | Coin balance pill, small tags |
| `ui_tabbar.png` | 1080 × 220 | Bottom tab bar strip |
| `ui_topbar.png` | 1080 × 240 | Top bar strip |
| `ui_bar_back.png` | 512 × 48 | Empty progress or timer bar |
| `ui_bar_fill.png` | 512 × 48 | Bar fill (tint) |
| `ui_splash.png` | 1080 × 1920 | Splash screen picture behind the logo |
| `ui_coin_stack.png` | 256 × 256 | Pile of coins for reward popups |
| `ui_coin_burst.png` | 512 × 512 | Ring of flying coins behind a reward |
| `ui_glow.png` | 512 × 512 | Soft round glow (tint) behind prizes and winners |
| `ui_star.png` | 128 × 128 | Sparkle (tint) |
| `ui_confetti.png` | 128 × 128 | One confetti piece (tint) |
| `ui_ribbon.png` | 768 × 192 | Banner behind "YOU WIN!", "New best!" |
| `ui_streak_flame.png` | 256 × 256 | Daily streak flame |
| `ui_day_done.png` | 256 × 256 | Daily reward: claimed day (tick stamp) |
| `ui_day_locked.png` | 256 × 256 | Daily reward: future day (padlock) |
| `ui_mission_check.png` | 128 × 128 | Mission completed tick |
| `ui_medal_1.png`, `ui_medal_2.png`, `ui_medal_3.png` | 128 × 128 | Ranks: gold, silver, bronze |
| `ui_avatar_0.png` to `ui_avatar_5.png` | 256 × 256 | Six player avatars (faces or animals) |
| `ui_avatar_cpu.png` | 256 × 256 | Robot face shown next to every "(CPU)" opponent |
| `ui_giftcard.png` | 512 × 320 | Redeem: blank gift card |
| `ui_lock.png` | 128 × 128 | Not enough coins / locked |
| `icon_pause.png` | 128 × 128 | In-game pause button |
| `icon_sound_on.png`, `icon_sound_off.png` | 128 × 128 | Sound toggle |
| `icon_ad.png` | 128 × 128 | "Watch ad" play symbol |
| `icon_close.png` | 128 × 128 | Close popup |
| `icon_timer.png` | 128 × 128 | Clock next to round timers |
| `icon_trophy.png` | 128 × 128 | Personal best |
| `app_icon.png` | 1024 × 1024 | Store and launcher icon (opaque) |

---

# Part 2: Games (2D)

Every game takes two files that are **already wired** by name:

- `tile_<id>.png` (512 × 512): the picture on the Home screen.
- `bg_<id>.png` (1080 × 1920, opaque, dark and low contrast): the backdrop.

The seven original games have theirs listed in `ArtPrompts.md`. The thirteen versus games below are new. All in-game sprites for the versus games are **New**.

Shared by the versus games:

| File | Size | What it is |
|---|---|---|
| `dice_1.png` to `dice_6.png` | 256 × 256 | Die faces (Ludo, Snakes & Ladders) |
| `vs_banner.png` | 940 × 300 | Panel behind end-of-match messages |
| `vs_turn_arrow.png` | 128 × 128 | "Your turn" pointer (tint) |
| `aim_dot.png` | 64 × 64 | Aiming dot (tint; Carrom, Pool) |

## Original games (names from ArtPrompts.md)

| Game | Tile and background | In-game sprites (Reserved) |
|---|---|---|
| Lane Rush | `tile_lane_rush`, `bg_lane_rush` | `lane_car`, `lane_barrier` (512 × 256) |
| Colour Snap | `tile_colour_snap`, `bg_colour_snap` | `snap_circle`, `snap_square` (tint) |
| Juice Slash | `tile_juice_slash`, `bg_juice_slash` | `fruit_orange`, `fruit_apple`, `fruit_watermelon`, `fruit_lemon`, `fruit_grape`, `fruit_kiwi`, `fruit_bomb` |
| Bubble Burst | `tile_bubble_burst`, `bg_bubble_burst` | `bubble` (tint), `bubble_shooter` |
| Blade Spin | `tile_blade_spin`, `bg_blade_spin` | `blade_knife` (128 × 512), `blade_target` (512 × 512) |
| Block Fit | `tile_block_fit`, `bg_block_fit` | `block_cell` (tint) |
| Gem Rush | `tile_gem_rush`, `bg_gem_rush` | `gem_0` to `gem_5` |

Extra sprites worth adding to the originals (New):

| File | Game | What it is |
|---|---|---|
| `lane_road.png` (1080 × 512, tiles vertically) | Lane Rush | Three-lane road strip |
| `lane_coin.png` | Lane Rush | Pickup, if pickups are added |
| `snap_triangle.png`, `snap_star.png` (tint) | Colour Snap | More shapes |
| `fruit_orange_half.png` and one half per fruit (6 files) | Juice Slash | Sliced halves |
| `fruit_splat.png` (tint) | Juice Slash | Juice splash |
| `slash_trail.png` (256 × 64) | Juice Slash | Finger trail |
| `fruit_explosion.png` (512 × 512) | Juice Slash | Bomb blast |
| `bubble_pop.png` (tint) | Bubble Burst | Pop burst |
| `bubble_danger_line.png` (1080 × 32) | Bubble Burst | Danger line |
| `blade_chip.png` | Blade Spin | Wood chip on impact |
| `blade_apple.png` | Blade Spin | Bonus target on the log, if added |
| `block_cell_empty.png` | Block Fit | Empty grid cell |
| `block_clear.png` (tint) | Block Fit | Row-clear flash |
| `gem_sparkle.png` (tint) | Gem Rush | Match sparkle |
| `gem_cell.png` | Gem Rush | Board cell |

## Ludo Dash (`ludo`)

| File | Size | What it is |
|---|---|---|
| `tile_ludo.png`, `bg_ludo.png` | standard | Tile and backdrop |
| `ludo_board.png` | 1024 × 1024 | 15 × 15 cross board, four coloured home yards, centre triangle |
| `ludo_token.png` | 256 × 256 | Pawn seen from above (tint; four player colours) |
| `ludo_safe_star.png` | 128 × 128 | Safe-square star |
| `ludo_home_crown.png` | 256 × 256 | Marker for a token that got home |
| `ludo_capture.png` | 256 × 256 | Capture burst |

## Air Hockey (`air_hockey`)

| File | Size | What it is |
|---|---|---|
| `tile_air_hockey.png`, `bg_air_hockey.png` | standard | Tile and backdrop |
| `hockey_rink.png` | 980 × 1540 | Table from above: centre line, circle, two goal mouths |
| `hockey_paddle_you.png`, `hockey_paddle_cpu.png` | 256 × 256 | Mallets from above (blue, pink) |
| `hockey_puck.png` | 128 × 128 | Puck |
| `hockey_goal_flash.png` | 512 × 256 | Goal light |
| `hockey_spark.png` | 128 × 128 | Wall and paddle hit spark (tint) |

## Penalty Shootout (`penalty_shootout`)

| File | Size | What it is |
|---|---|---|
| `tile_penalty_shootout.png`, `bg_penalty_shootout.png` | standard | Tile; backdrop is a stadium at night |
| `penalty_pitch.png` | 1080 × 1200 | Penalty area seen from behind the spot |
| `penalty_goal.png` | 900 × 420 | Goal frame with net |
| `penalty_ball.png` | 256 × 256 | Football |
| `penalty_keeper_cpu.png` | 320 × 320 | Bot keeper, ready pose, front view |
| `penalty_keeper_cpu_dive.png` | 400 × 256 | Bot keeper diving (the game mirrors it) |
| `penalty_keeper_you.png`, `penalty_keeper_you_dive.png` | same | Your keeper |
| `penalty_gloves.png` | 256 × 256 | Glove marker you drag when saving |
| `penalty_mark_goal.png`, `penalty_mark_miss.png`, `penalty_mark_empty.png` | 96 × 96 | Kick tally dots |
| `penalty_net_ripple.png` | 512 × 256 | Net bulge on a goal |

## Four in a Row (`four_in_a_row`)

| File | Size | What it is |
|---|---|---|
| `tile_four_in_a_row.png`, `bg_four_in_a_row.png` | standard | Tile and backdrop |
| `four_board.png` | 1008 × 864 | Front frame with 7 × 6 round holes (holes transparent) |
| `four_disc_you.png`, `four_disc_cpu.png` | 256 × 256 | Yellow and pink discs |
| `four_win_ring.png` | 256 × 256 | Ring around the four winning discs |
| `four_column_arrow.png` | 128 × 128 | Column pointer (tint) |

## Race Dash (`race_dash`)

| File | Size | What it is |
|---|---|---|
| `tile_race_dash.png`, `bg_race_dash.png` | standard | Tile; backdrop is a stadium stand |
| `race_track.png` | 1080 × 512, tiles vertically | Four-lane running track |
| `race_runner_0.png` to `race_runner_3.png` | 256 × 256 each | Four run frames, plain white kit (tint per lane), seen from above or behind |
| `race_runner_stumble.png` | 256 × 256 | Stumble pose (tint) |
| `race_finish_line.png` | 1080 × 96 | Chequered line |
| `race_stride_bar.png` | 900 × 120 | Timing bar with good and perfect zones |
| `race_stride_marker.png` | 96 × 160 | Moving marker |
| `race_dust.png` | 128 × 128 | Foot dust puff |
| `race_podium.png` | 768 × 384 | Result podium, 1-2-3 |

## Carrom (`carrom`)

| File | Size | What it is |
|---|---|---|
| `tile_carrom.png`, `bg_carrom.png` | standard | Tile and backdrop |
| `carrom_board.png` | 1024 × 1024 | Wooden board, four corner pockets, centre circle, two striker lines |
| `carrom_coin_white.png`, `carrom_coin_dark.png`, `carrom_queen.png` | 128 × 128 | The three coin types |
| `carrom_striker.png` | 160 × 160 | Striker |
| `carrom_slider.png` | 720 × 100 | Striker slider track |
| `carrom_pocket_puff.png` | 128 × 128 | Pocketing puff |

## Pool (`pool`)

| File | Size | What it is |
|---|---|---|
| `tile_pool.png`, `bg_pool.png` | standard | Tile and backdrop |
| `pool_table.png` | 840 × 1220 | Table from above: green cloth, wooden rails, six pockets |
| `pool_ball_cue.png`, `pool_ball_you.png`, `pool_ball_cpu.png`, `pool_ball_black.png` | 128 × 128 | White, blue solid, pink stripe, black |
| `pool_cue_stick.png` | 64 × 768 | Cue shown behind the white ball while aiming |
| `pool_ghost_ball.png` | 128 × 128 | Pale outline ball for the aim guide |
| `pool_chalk_puff.png` | 128 × 128 | Shot puff |

## Snakes & Ladders (`snakes_ladders`)

Snakes and ladders are placed differently every round, so they have to be built from pieces. A single painted board will not work.

| File | Size | What it is |
|---|---|---|
| `tile_snakes_ladders.png`, `bg_snakes_ladders.png` | standard | Tile; backdrop is a jungle |
| `snake_cell_a.png`, `snake_cell_b.png` | 256 × 256 | Two alternating board squares |
| `snake_cell_win.png` | 256 × 256 | Square 49 |
| `snake_head.png` | 128 × 128 | Snake head from above, facing up (tint) |
| `snake_body.png` | 64 × 64 | One body segment (tint) |
| `snake_tail.png` | 64 × 96 | Tail tip (tint) |
| `ladder_rail.png` | 256 × 24, stretches | Ladder side rail |
| `ladder_rung.png` | 24 × 64 | Ladder rung |
| `snake_token.png` | 256 × 256 | Player piece (tint; three colours) |
| `snake_target_ring.png` | 256 × 256 | Ring showing where a die takes you (tint) |

## Tic-Tac-Toe (`tic_tac_toe`)

| File | Size | What it is |
|---|---|---|
| `tile_tic_tac_toe.png`, `bg_tic_tac_toe.png` | standard | Tile and backdrop |
| `ttt_grid.png` | 940 × 940 | 3 × 3 grid |
| `ttt_x.png` | 256 × 256 | X (blue) |
| `ttt_o.png` | 256 × 256 | O (pink) |
| `ttt_win_line.png` | 900 × 64, rotated by the game | Strike-through for the winning line |
| `ttt_pip_won.png`, `ttt_pip_empty.png` | 96 × 96 | Match score pips |

## Dots & Boxes (`dots_boxes`)

| File | Size | What it is |
|---|---|---|
| `tile_dots_boxes.png`, `bg_dots_boxes.png` | standard | Tile and backdrop |
| `dots_dot.png` | 96 × 96 | Grid dot |
| `dots_line_free.png` | 256 × 32, stretches | Faint guide for a line not yet drawn |
| `dots_line.png` | 256 × 32, stretches | Drawn line (tint; blue for you, pink for the bot) |
| `dots_box_you.png`, `dots_box_cpu.png` | 256 × 256 | Fill for a box you own, and one the bot owns |

## Cricket Chase (`cricket_chase`)

| File | Size | What it is |
|---|---|---|
| `tile_cricket_chase.png`, `bg_cricket_chase.png` | standard | Tile; backdrop is a floodlit ground |
| `cricket_pitch.png` | 320 × 1240 | Pitch strip from above with both creases |
| `cricket_ball.png` | 128 × 128 | Red ball |
| `cricket_batter.png`, `cricket_batter_swing.png` | 320 × 320 | Batter from above: ready, and mid-swing |
| `cricket_bowler.png` | 256 × 256 | Bowler from above |
| `cricket_stumps.png`, `cricket_stumps_broken.png` | 128 × 128 | Stumps standing, and knocked over |
| `cricket_hit_line.png` | 320 × 120 | Glowing band that marks when to tap |
| `cricket_ball_slot.png` | 96 × 96 | Box for each ball in the over-by-over row |

## Tap Battle (`tap_battle`)

| File | Size | What it is |
|---|---|---|
| `tile_tap_battle.png`, `bg_tap_battle.png` | standard | Tile; backdrop is a sandy arena |
| `tap_rope.png` | 48 × 512, stretches | Rope segment |
| `tap_flag.png` | 256 × 96 | Marker flag tied to the middle of the rope |
| `tap_puller_you.png`, `tap_puller_cpu.png` | 256 × 256 | The two pullers from above (blue, pink) |
| `tap_line.png` | 720 × 32 | Win line (tint) |
| `tap_dust.png` | 128 × 128 | Dust kicked up on each tap |

## Quiz Duel (`quiz_duel`)

| File | Size | What it is |
|---|---|---|
| `tile_quiz_duel.png`, `bg_quiz_duel.png` | standard | Tile; backdrop is a quiz-show stage |
| `quiz_question_panel.png` | 512 × 256, stretches | Panel behind the question |
| `quiz_answer.png` | 512 × 128, stretches | Answer button, not yet chosen |
| `quiz_answer_locked.png` | 512 × 128, stretches | Your chosen answer before the reveal |
| `quiz_answer_right.png`, `quiz_answer_wrong.png` | 512 × 128, stretches | Green and red buttons at the reveal |
| `quiz_cpu_tag.png` | 128 × 64 | Tag marking the answer the bot chose |

---

# Part 3: 3D models

**None are required.** Tapzy is built entirely as a flat 2D interface: there is no 3D scene, camera or lighting in any game, so a model cannot be dropped in the way a PNG can.

If you want a 3D look, there are two routes:

1. **Render models to PNG** and use them as the sprites in Part 2. No code changes beyond the normal wiring. This is the cheap route and it suits a top-down or front-on game.
2. **Real 3D in the scene.** Each game that uses it needs its drawing rewritten, and the physics games (Air Hockey, Carrom, Pool) need their camera angle designed. Treat this as a rebuild of that game.

Models that would give the most for either route (FBX or GLB, under 5,000 triangles each, one 1024 × 1024 texture):

| Model | Game | Notes |
|---|---|---|
| `dice` | Ludo, Snakes & Ladders | With a roll animation, or six rendered faces |
| `ludo_pawn` | Ludo | One mesh, four material colours |
| `ludo_board` | Ludo | |
| `hockey_table`, `hockey_mallet`, `hockey_puck` | Air Hockey | |
| `football`, `goal_with_net` | Penalty Shootout | |
| `goalkeeper` | Penalty Shootout | Rigged; idle, dive left, dive right, catch |
| `runner` | Race Dash | Rigged; run cycle, stumble, celebrate |
| `carrom_board`, `carrom_coin`, `carrom_striker` | Carrom | |
| `pool_table`, `pool_ball`, `pool_cue` | Pool | Ball: one mesh, four textures |
| `four_frame`, `four_disc` | Four in a Row | |
| `coin` | Hub | Spinning coin for reward popups |
| `gift_box` | Hub | Daily reward; open animation |

The seven original games (Lane Rush, Colour Snap, Juice Slash, Bubble Burst, Blade Spin, Block Fit, Gem Rush), Tic-Tac-Toe, Dots & Boxes, Quiz Duel, Tap Battle and Snakes & Ladders gain little from 3D and are best left 2D.

---

# Part 4: Sound

All sound today is a generated beep from `Sfx.cs`; it has no way to play an audio file yet. **Every file below is New and needs wiring**, which is one change in `Sfx.cs` plus naming the sound at each call.

- **Effects:** WAV, 44.1 kHz, 16-bit, mono, under one second unless noted, no silence at the start. Proposed folder: `Assets/Tapzy/Resources/Audio/`.
- **Music:** OGG, stereo, seamless loops of 30–60 seconds. There is no music in the app today.
- The existing sound on/off setting will cover all of it. A separate music toggle would be a small addition.

## Music and jingles (9)

| File | Where |
|---|---|
| `music_hub.ogg` | Hub screens; relaxed and bright |
| `music_arcade.ogg` | Lane Rush, Colour Snap, Juice Slash, Blade Spin; fast |
| `music_puzzle.ogg` | Bubble Burst, Block Fit, Gem Rush; light |
| `music_board.ogg` | Ludo, Four in a Row, Carrom, Snakes & Ladders, Tic-Tac-Toe, Dots & Boxes, Quiz Duel; easy-going |
| `music_sports.ogg` | Air Hockey, Penalty Shootout, Race Dash, Pool, Cricket Chase, Tap Battle; upbeat |
| `jingle_win.wav` | Match won (2 s) |
| `jingle_lose.wav` | Match lost (1.5 s) |
| `jingle_new_best.wav` | New personal best (1.5 s) |
| `jingle_reward.wav` | Daily reward and mission payout (1.5 s) |

## Interface and shared effects (24)

| File | When |
|---|---|
| `ui_click.wav` | Any button |
| `ui_tab.wav` | Switching hub tab |
| `ui_popup_open.wav`, `ui_popup_close.wav` | Popups |
| `ui_toast.wav` | Toast message |
| `ui_error.wav` | Not enough coins, invalid action |
| `ui_coin.wav` | One coin landing |
| `ui_coin_count.wav` | Tick while a coin total counts up (very short) |
| `ui_mission_done.wav` | Mission completed |
| `ui_streak.wav` | Daily streak day claimed |
| `ui_redeem.wav` | Gift card redeemed |
| `game_ready.wav` | "Tap to start" appears |
| `game_start.wav` | Round begins |
| `game_tick.wav` | Last five seconds of a timed round |
| `game_time_up.wav` | Timer runs out |
| `game_over.wav` | Round ends in a fail |
| `game_pause.wav`, `game_resume.wav` | Pause menu |
| `game_revive.wav` | Continue after watching an ad |
| `game_score_small.wav`, `game_score_big.wav` | Generic good and great (today's `Sfx.Good` and `Sfx.Great`) |
| `game_bad.wav` | Generic mistake (today's `Sfx.Bad`) |
| `vs_your_turn.wav` | Your turn in any versus game |
| `vs_timer_low.wav` | Turn timer nearly out |

## Per-game effects (109)

| Game | Files | When |
|---|---|---|
| Lane Rush (5) | `lane_swipe`, `lane_engine_loop` (loop), `lane_pass`, `lane_crash`, `lane_speed_up` | Change lane, driving, near miss, hit a barrier, speed step |
| Colour Snap (4) | `snap_right`, `snap_wrong`, `snap_target_change`, `snap_spawn` | Correct tap, wrong tap, new target colour, shapes appear |
| Juice Slash (6) | `slash_swing`, `slash_cut`, `slash_combo`, `slash_launch`, `slash_miss`, `slash_bomb` | Swipe, slice, several in one swipe, fruit thrown, fruit dropped, bomb |
| Bubble Burst (6) | `bubble_shoot`, `bubble_bounce`, `bubble_stick`, `bubble_pop`, `bubble_drop`, `bubble_danger` | Fire, wall bounce, attach, match pops, loose cluster falls, near the line |
| Blade Spin (5) | `blade_throw`, `blade_stick`, `blade_clash`, `blade_stage_clear`, `blade_log_break` | Throw, hit wood, hit another blade, stage done, log breaks |
| Block Fit (5) | `block_pick`, `block_place`, `block_reject`, `block_line_clear`, `block_multi_clear` | Lift, drop in place, will not fit, one line, several lines |
| Gem Rush (6) | `gem_swap`, `gem_swap_back`, `gem_match`, `gem_cascade` (rising pitch set of 4), `gem_land`, `gem_big_match` | Swap, bad swap, match, chain steps, gems land, four or more |
| Ludo Dash (7) | `dice_roll`, `dice_land`, `ludo_hop`, `ludo_enter`, `ludo_capture`, `ludo_home`, `ludo_six` | Shared dice sounds, token step, token leaves yard, capture, token home, rolled a six |
| Air Hockey (6) | `hockey_hit`, `hockey_wall`, `hockey_goal_you`, `hockey_goal_cpu`, `hockey_horn`, `hockey_slide_loop` (loop) | Paddle hit, cushion, your goal, their goal, final horn, puck gliding |
| Penalty Shootout (8) | `penalty_whistle`, `penalty_kick`, `penalty_net`, `penalty_save`, `penalty_post`, `penalty_wide`, `penalty_crowd_cheer`, `penalty_crowd_groan` | Each kick starts, strike, goal, save, woodwork, miss, crowd reactions |
| Four in a Row (4) | `four_drop`, `four_land`, `four_win`, `four_column_full` | Disc released, disc lands, four connected, full column tapped |
| Race Dash (7) | `race_ready`, `race_gun`, `race_step_perfect`, `race_step_good`, `race_stumble`, `race_finish`, `race_crowd_loop` (loop) | "Set", start gun, stride timing results, stumble, cross the line, crowd |
| Carrom (5) | `carrom_strike`, `carrom_coin_hit`, `carrom_wall`, `carrom_pocket`, `carrom_foul` | Striker released, coins collide, cushion, pocketed, foul |
| Pool (6) | `pool_cue_hit`, `pool_ball_hit`, `pool_cushion`, `pool_pocket`, `pool_break`, `pool_foul` | Shot, balls collide, cushion, pot, opening break, foul |
| Snakes & Ladders (3 + shared dice) | `snake_hop`, `snake_ladder_climb`, `snake_slide` | Token step, climbing a ladder, sliding down a snake |
| Tic-Tac-Toe (4) | `ttt_place_x`, `ttt_place_o`, `ttt_board_win`, `ttt_board_draw` | Marks placed, a board won, a board drawn |
| Dots & Boxes (3) | `dots_line`, `dots_box`, `dots_chain` | Line drawn, box closed, second and later boxes in one turn |
| Cricket Chase (7) | `cricket_bowl`, `cricket_hit`, `cricket_four`, `cricket_six`, `cricket_edge`, `cricket_bowled`, `cricket_crowd_loop` (loop) | Ball released, bat on ball, boundary, six, thin edge, stumps hit, crowd |
| Tap Battle (6) | `tap_count`, `tap_go`, `tap_pull`, `tap_false_start`, `tap_strain_loop` (loop), `tap_pull_won` | Countdown beep, "GO!", each tap, tapping too early, rope creaking, a pull won |
| Quiz Duel (6) | `quiz_question`, `quiz_lock`, `quiz_right`, `quiz_wrong`, `quiz_timeout`, `quiz_clock_loop` (loop) | New question, answer locked in, right, wrong, out of time, ticking clock |

All per-game files are `.wav`. The two shared dice sounds (`dice_roll`, `dice_land`) are counted once, under Ludo.

---

# Suggested order

1. Batch 1 of `ArtPrompts.md` (interface and the seven original tiles) to settle the style.
2. The thirteen new tiles (`tile_<id>.png`): they are wired already and are what a new player sees first.
3. Interface and shared sounds plus `music_hub`: 25 files that touch every screen.
4. In-game art and sound one game at a time, starting with the games you expect to be played most.

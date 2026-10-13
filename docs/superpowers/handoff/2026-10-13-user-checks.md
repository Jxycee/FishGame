# FishGame - Every manual check owed by the user (2026-10-13)

The single source for the next session's opening: the agent walks the user through this list and records results. Results go on a db-backed notes board, one row per check: PASS / FAIL / SKIP plus free text, saved server-side so nothing blanks.

**Where to test:** use the M3 worktree `C:\Dev\FishGame-m3` (branch `m3-fish-ecology`). It has all of M2 (merged into main) plus M3 so far.
- In Unity Hub: "Add project from disk", then pick that folder.
- Tell the agent before opening it, because agents run Unity in that folder. Close the editor afterwards.

**M3 renames:** the M2 species names changed in M3. perch is now `yellow_perch`, carp is `grumpy_carp`, trout is `sunny_trout` and catfish is `mudcat`; bluegill stays. F10 cycles all 25 species.

**What a solo session looks like now:** it opens at dawn, in autumn, in fog. That comes from the fixed dev seed (M3-D57) and is expected.

**Already passed (2026-10-12, first 9 checks):** 1 menu smoke, 2 settings, 5 first catch on keyboard+mouse, 7 pause mid-fight, 8 dev keys. Not passing (now reworked): 3 (dark water, now smooth R73), 4 (ladders, now R70), 6 (fight too easy, now R72), and 9 (catfish impossible, now R72). The 9th check's models were fine.

## 0. Blocking: unlocks the rest of M3 (do first, about 10 min)

- **W1 - WorldState two-window sync (Direct4P, 2 players).** Steps:
  1. The client Console shows no error or warning mentioning NetworkObject, GlobalObjectIdHash, in-scene, soft synchronization or WorldState.
  2. In the client Hierarchy, `World` is active and has a NetworkObjectId.
  3. In both windows, WorldState's NetSeason and NetWeather match.
  4. Late joiner: after the host has run for 60 s or more, a second client joins, and steps 1-3 hold for it.
  5. A client leaves and rejoins with no errors, and World spawns again.
  6. When the host leaves, the clients return to the menu with no errors.

## 1. Solo in the editor

The original steps 3, 4, 6 and 9, re-run after the rework:

- **S3 - Water and look.** Walk the dock. Judge:
  - the smooth water in motion (ripple drift, caustics, sparkles);
  - the new realistic-toon look (dock, trees, rock, reeds, brass bell, rod, bobber);
  - the player's pillow body. The user already said it will be redesigned later.

  Note the frame time in the Stats window at the dock end. The old baseline was 2.8-3.0 ms, and the scene now has about 3x the triangles.
- **S4 - Ladders (R70).**
  - Swim to each dock ladder, look up and hold W: you climb out with no button, in about 1.25 s (2 s at most).
  - Climb down from the deck.
  - Walk up the shore ramp out of the water (a small boost at the foot).
  - E never rings the bell from the water or from a ladder.
- **S6 - HUD during fights.**
  - Mid-fight, switch the gauge between Arc and Bar: it runs into a red zone past full, up to the snap point.
  - With captions off, nothing shows.
  - Assist OFF and gauge Off, then assist ON: the gauge becomes Arc.
- **S9 - Species and reveal.** Use F10 to land a mix of species, including mudcat (the old catfish, which should now be catchable). Each reveal shows the right model, size and colours.

The rest of the original list, never run:

- **S10 - Creel and bell.**
  - At 80% full, the count turns bold amber.
  - When full it shows "Full - sell!", and the next fish says "released".
  - Ringing the bell resets the count.
- **S11 - Edge cases.** Each must be back to Idle within 3 s with no rumble left:
  - Cast at the dock or shore: a thunk, and the bait count is unchanged.
  - Cast about 15 m out, then walk back to the shore: the rod auto-reels at about 32 m and the bait is refunded.
  - Let a side run swing the fish toward the dock.
  - Twig rod, raw meat, long cast, never reel: the tension drains to 0 and the fish escapes at 120 s.
  - Alt-tab away for 5 s mid-fight.
- **S12 - Readability without HUD or sound.**
  - Gauge Off, captions off, master volume 0. Fight 3 fish and tell a side run (side splash only) from a dive (side splash, plus the bobber plunges, the line points down and a deep thrum), and spot a near-snap, from the rod and line alone.
  - Then set camera motion to 0: no shake, no zoom kick, no rumble.
- **S13 - UI scale.** At a 1280x720 Game view, set UI scale to 80%, 100% and 150%: nothing clips or covers the crosshair.
- **S14 - Tuning lives in assets.** In Play mode, set FishingTuning.asset `castChargeTime` from 1.0 to 0.6: the ring fills faster. Revert it.
- **S15 - Gamepad-only catch.**
  - Controls: North to equip, LT to ready, RT to cast and reel, left stick to steer.
  - Rumble is 0 on every terminal state.
- **S16 - Menu leftovers.**
  - Pause > Leave returns to the menu with no errors.
  - Join code `abc` gives "code not found".
  - Dev Host Direct, then Cancel at once; then the same with Esc.
  - Stop Play during the loading screen.
- **S17 - Settings persist.** Quit and restart the editor: the gauge, captions and assist settings are kept.

The R72 fight (acceptance doc section K.2). Turn the gauge on, and use F10 to force species:

- **F1 - Gauge.** It fills while reeling and drains only when you let go of everything. It has a full mark, a red zone up to the snap, and blinks past full.
- **F2 - Greed on a bluegill.** Reel non-stop without steering: it snaps in about 5-8 s.
- **F3 - Counter a mudcat.** Steer without reeling: the fish comes back toward the centre, its runs shorten, and reeling gets faster. Watch for jitter near the centre line.
- **F4 - Greed on a mudcat.** It snaps in about 3.4 s.
- **F5 - Creak.** Silent below 10% and steep near the top. After you let go it follows the strain down for about 3.4 s; only the last tail fades over 0.25 s.
- **F6 - Bobber.** It leans toward the pull, and less on a dive.
- **F7 - Unattended.** An unattended fish escapes at 2 minutes; junk clears at 12 s.
- **F8 - Assist.** With assist on, the same greedy reel snaps later.
- **F9 - Five styles, blind.** F10 on None and F3 overlay off. Name each style before the reveal: Steady, Darter, Thrasher, Bulldog, Runner. Best done with a friend pressing F10.
- **F10 - Feel verdict.** What is too harsh? Bulldog is the harshest, at about 31 s. Answer the K.3 questions:
  - Steady strainMod 0.8 or 0.6;
  - should an unattended fish only escape at 120 s?
  - the Steady Nerves x1.15 placeholder.

  This feedback starts the friendlier tuning pass. After any retune, re-run EcologyBalanceReports: V5 sits near a cliff.

Ladder and ramp feel (acceptance J, ladders plan):

- **L1 - Look-up angle.** Climb out at a shallow look-up (30 deg) and at a steep one (60 deg).
- **L2 - Top-out bob.** Top out looking almost straight up: the bob must not make the rod flicker in or out.
- **L3 - Deck-edge grab.** Walk to the deck edge in front of a ladder. Does it grab you too early (ladders Q7)?
- **L4 - Descend and hang.** Descend looking down, let go mid-ladder (you hang), then leave sideways into the water.
- **L5 - Ramp kick.** The ramp kick at the foot when walking out; no push when walking back down.
- **L6 - Rod animation.** The rod stow/unstow animation on key 1, in the water and on a ladder, with the rod out and with the line out.
- **L7 - Wading beside the ladders.** The bed there sits right at swim depth. Does the rod flick in and out as the water bobs?

Bad-luck nudge (acceptance H):

- **N1 - Nudge.** Fail 3 casts in a row: the next cast shows "<name> has a lucky line!" and snaps later (headroom x1.5). Decide: keep / tune / remove.

Dev and debug:

- **D1 - F3 overlay.** It updates through a cast, a bite and a fight. F4/F5 cycle rods and baits while Idle, and say "Refused" while waiting. F6 gives a fast bite, F7 a "Click!" bite, and F9 ripples water (on the dock it says "No water"). With Pause open, no F key does anything.
- **D2 - Rod geometry.** Cast from the deck end at minimum and maximum charge, at camera pitch -60, 0 and +60. The rod tip stays in front of the camera, and there are no Invalid escapes on honest casts.

## 2. M3 looks (solo; some need tasks not built yet)

- **M1 - Fish batches 1-4.** Pictures were sent in chat and saved under `docs/superpowers/handoff/img/2026-10-13-m3-fish-batch{1..4}.png` and `...-m3-fish-ingame.png`. Keep or adjust each fish.
- **M2 - Silver Mullet journal icon.** The icon paints the belly in the accent colour, which is slate on the mullet even though its real belly is white. Keep it, or change the accent to white?
- **M3 - Seasonal trees in game.** Needs the T22 dev panel season switch, so for now use the picture `2026-10-13-seasonal-trees.png`.
  - Spring: blossoms. Autumn: mixed colours. Winter: bare, with snowy pines.
  - Bare winter trees keep their round collider, so you can bump into "air". Acceptable?
- **M4 - Day-night and weather** (after T22's F8 scrub).
  - A full-day scrub has no pops; dusk is warm and night is cool.
  - Night and max fog stay readable at 25 m (bobber, marker, dock edge).
  - Rain reads as rain at 1080p, its patter sits under the wind, and the 4 s fade has no pop.
  - Wind sounds like M2 in Summer and louder in Autumn/Winter.
  - Known: rolling past minute 1200 pops (dev clock only).
  - Frame time under full rain (reduce MaxRainRate if rain costs more than 10%).
- **M5 - Default session look.** Dawn, autumn and fog for the first ~6 minutes, with an 18 s sun fade-in. Fine as a testing default?
- **M6 - Harbour Pier look, journal UI look, dev panel:** after T17, T21 and T22. The M3 acceptance doc (T25) will list them.

## 3. Two windows (Direct4P / MPPM)

Network Simulator at 150 ms RTT and 2% loss on the clients.

- **P1 - MP1-MP11 (acceptance section B).**
  - MP1: cast, bite and fight cues reach both peers.
  - MP2: remote rod, line and bobber trail by 250 ms or less, and the remote rod angle matches.
  - MP3: the cast arc plays once (none for a late joiner).
  - MP4: the reveal plays for all, and crew toasts are limited to one per 3 s.
  - MP5: a client disconnecting mid-fight leaves no stuck hook, and the rod vanishes within 1 s.
  - MP6: the host alt-tabbing for 10 s doesn't stall anyone.
  - MP7: a late joiner mid-fight sees the bent rod with no species shown.
  - MP8: no felt latency for the owner.
  - MP9: online join code (Unity Cloud is linked now).
  - MP10: the landing marker is owner-only.
  - MP11: land 10 clean fish and compare with the log's `clean` column.
- **P2 - Remote fight presentation (R72).**
  - The bobber tilt matches on the other peer.
  - A Bulldog's dive shows a side splash plus the plunge.
  - Darter and Thrasher mid-run flips show the side-run cue on the remote too.
- **P3 - Remote ladders.** A second window sees a climber move smoothly. They float up with no pose for now.
- **P4 - M3 world on two windows** (after T15 and T22).
  - The hotspot's bubbles and ripples show on the client.
  - The world look "snaps" once on join (actually up to 3 quick steps within about 0.5 s, behind the loading screen).
  - A limit-1 special can't be double-landed.
- **P5 - M1 multiplayer criteria 3-9 and 12** (`docs/superpowers/plans/2026-10-09-m1-acceptance.md`). Never run. Note the real join-code format and the LobbyFull / LobbyLocked messages.
- **P6 - Online cancel.** On a real join-code join, press Cancel. "Cancelling..." shows for about a frame, then the menu with greyed buttons for up to 30 s. Acceptable, or should there be a message?

## 4. Builds (editor closed)

- **B1 - Development build.** Fog is visible, the bobber sits on the visible waves, and note the frame time at 1080p.
- **B2 - Normal (non-development) build.** F3-F10 do nothing at all.

## 5. No editor needed

- **X1 - Sounds pack.** Listen to `_ALL_SOUNDS_in_order.wav` (sent in an earlier chat; re-send if missing) and note anything off. New since then: Rain_Patter and the seasonal wind.
- **X2 - Fallback fish shape.** Now only junk or a missing model uses it, so this is low priority.

## 6. With a friend (after the friendlier tuning pass)

- **G1 - Fun gate.** 30+ logged fights (acceptance section D). Bites in 6-12 s; snaps feel like greed rather than bad luck; the five styles are told apart blind; you still want to cast after 10 minutes. Also MP9 over the internet.

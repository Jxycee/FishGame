# Research: voice-mimic mods and "seagulls steal your voice" (2026-10-14)

Question: how does Lethal Company's voice stealing work, and can FishGame give it to
seagulls (gulls record snippets of what players say and squawk them back, positionally)?
Method: public pages only (Thunderstore, GitHub raw source of Mirage, Unity docs, Steam
threads). Nothing downloaded or run. Page text was read through a summariser, so exact
constants below are as reported from the source files and should be re-checked in the
repo before we copy any number.

## 0. Summary and decision needed

- Verdict: feasible. Recommended design is "Mirage-style, on-demand, owner-sourced
  clips" with a host-chosen playback time (section 4). Size: M if Vivox (spec 16.1) and
  the M5 seagull thief exist; L if we also have to build the Vivox integration itself.
- Decision needed (spec conflict): spec section 6.8 says "Voice: Vivox, never through
  NGO". Our design sends short, capped, game-triggered CLIPS over NGO (not live voice).
  Live voice stays Vivox-only. CLAUDE.md says not to re-litigate agreed spec items, so
  this needs a user OK as a narrow amendment. A no-amendment fallback (design B,
  section 4.5) exists but is weaker on consent and fidelity.
- Spike first: whether `VivoxCaptureSourceTap` delivers usable mic samples in the
  pinned package (16.12.1) and what it does while muted (section 3.2).

## 1. Lethal Company's own voice chat

- Library: almost certainly Dissonance Voice Chat (Placeholder Software, Asset Store,
  paid) on Netcode for GameObjects. Evidence is a community post that found Dissonance
  in the extracted game plugins, plus Mirage's source hooking `DissonanceComms`
  directly. No statement from the developer found, so "confirmed by modders, not by
  Zeekerss".
  - https://steamcommunity.com/app/1966720/discussions/0/4364627964252165209
  - https://github.com/qwbarch/mirage (Hook/Dissonance.fs, Hook/Microphone.fs)
- Proximity: positional voice, you cannot hear crewmates who are too far away unless
  you use a walkie-talkie. Exact falloff numbers not found in public sources.
- Walkie-talkie: bought for 10 credits, voice and text, works beyond the proximity
  radius, ~820 s battery, you hear others while it is in your inventory, static if a
  holder dies with theirs on. Audio is deliberately compressed/degraded (guide claim).
  - https://lethal.miraheze.org/wiki/Walkie-Talkie
- Dead players: sources conflict (one says the dead hear but cannot speak, another says
  they are cut off, a Steam thread describes a bug about hearing the dead). Treat as
  unconfirmed.
- Vanilla enemies using player voices: none found. The wiki lists only a distorted
  laugh for the Masked (and the comedy-mask item's laugh); "mimic" there means
  behaviour and appearance, not speech. So the premise is right: voice stealing is a
  mod feature, not vanilla.
  - https://lethal.miraheze.org/wiki/Masked

## 2. The voice-mimic mods

### 2.1 Mirage (qwbarch) - the one with readable source

Real and popular: ~6.3M Thunderstore downloads, 165 likes, 2,259 dependants, v1.29.1,
open-source repo (licence not checked), F# code. Needs to be on the host and all clients; only the host's config
is used.
- https://thunderstore.io/c/lethal-company/p/qwbarch/Mirage/
- https://github.com/qwbarch/mirage (package/mirage-lc/src/Mirage/...)
- https://raw.githubusercontent.com/qwbarch/mirage/main/CHANGELOG.md

How it captures audio
- Hooks `DissonanceComms.Start` (MonoMod-style `On.` hook), then calls
  `SubscribeToRecordedAudio(MicrophoneSubscriber())`. `IMicrophoneSubscriber` is a
  documented Dissonance extension point; it receives each mic buffer OFF the main
  thread. So: a tap on the same processed mic stream that is sent to peers.
  - https://dissonance.readthedocs.io/en/latest/Tutorials/UsingIMicrophoneSubscriber.html
- Buffers are copied (ArrayPool) onto a channel, then gated by game state: ready
  (round started), alive (unless `RecordWhileDead`), not muted, push-to-talk state,
  `allowRecordVoice`.
- Voice activity: Silero VAD (an ML model, since 1.9.1) on 2048-sample windows. Start
  threshold 0.35, end 0.2, `MinSilenceDurationMs` 2000, `MinAudioDurationMs` 150.
  Push-to-talk forces "voice" on, mute forces it off. No hard max clip length was
  found: a clip runs from voice start until silence, so natural sentences.
- Storage: segments are written to LOCAL files by the owner (WAV -> MP3 -> Opus in
  1.13.0). Deleted at game close/startup unless `neverDeleteRecordings`.

How clips reach other clients - the key design lesson
- Clips are NOT pre-distributed. Each owner keeps its own clips. When a mimic enemy
  wants to speak, only the client whose mic is being mimicked picks one of its own
  recordings and STREAMS it as Opus packets: client -> host (`ServerRpc`, host checks
  the sender is the allowed sender) -> all clients (`ClientRpc`). Each receiver decodes
  packets into an `AudioSource` as they arrive. Changelog 1.13.0: "audio packet sizes are
  extremely tiny now".
- Exact Opus bitrate/frame size not visible in the files read (not stated).
- Host picks WHOSE voice an enemy mimics, once at spawn, using a shuffled no-repeat
  pool (`MimicPlayerClientRpc`), so each enemy has a consistent voice.

When and where it plays
- A per-enemy loop on each client waits a random 7-12 s (config "Minimum/Maximum delay",
  7000/12000 ms, separate masked/non-masked values), then streams one random clip, if
  the enemy is alive, has a mimicked player, and this client is that player.
- Position/spatial: a clone of Dissonance's own playback prefab (so it gets the same
  spatial settings, low-pass filter and mixer group as that player's real voice) is
  moved to the enemy's position every `LateUpdate`. Volume follows that player's voice
  volume. Mutes for dead enemy, hiding, wrong inside/outside, and per-player mute.
- Sync is approximate: no timestamps or clock sync; "same moment" comes from live
  streaming, not scheduling. Timers use an unseeded `Random` per client.

Privacy / consent options
- Per player: `allowRecordVoice` (default true; false = nothing is recorded),
  `muteVoiceMimic` (default false), `localPlayerVolume` (default 0.5),
  `neverDeleteRecordings` (default false). Host/global: mimic chance, delays, "hear own
  voice while alive" (default true), "record while dead" (default false).
- Gaps: opt-out is local to the owner, but the host does not know about it beyond the
  owner simply never sending; no in-lobby consent screen.

Known problems
- Conflicts with Skinwalkers (unsynced voice lines, both mods play at once).
- Early versions lagged (hot-path allocations; fixed with pooling and off-thread decode
  in 1.13.0) and over-recorded (VAD tuning, PTT bugs, 1.12.x).
- Needs the mod on every client (no desync tolerance); netcode patch maintenance
  (changelog notes updates for game version bumps).

### 2.2 Skinwalkers (RugbugRedfern)

Real, widely covered, now marked deprecated on Thunderstore. No public source repo
found, so details come from the Thunderstore FAQ only.
- https://thunderstore.io/c/lethal-company/p/RugbugRedfern/SkinwalkersExperimental/
- Model is the opposite of Mirage: CLIENT-SIDE. Each client records the voices it
  hears locally and plays them back at random times from chosen enemies, so every
  player hears different things (and Mirage's author documents the conflict).
- FAQ: since v3.0.0 nothing is written to disk, audio is memory-only and wiped at game
  close, data "is never sent to anyone". Clip cap 500 (v3.0.0) reduced to 200 (v3.0.1).
  Config: which creatures, how often (host config syncs).
- Clip length and capture method are not stated publicly.
- Lesson: zero-network, listener-side recording is simple and private-ish, but loses the
  shared "everyone hears it at once" moment.

## 3. Translating it to our stack

### 3.1 What the spec already has
- Vivox 16.12.1, M6, one positional channel per session id, independent of NGO,
  online sessions only, audible 20 m / conversational 4 m (spec 16.1, line 2196).
- Server-simulated seagull thief (`ThiefController`, `NetThiefState`) with a 4-6 s
  circling/squawking telegraph (spec 15.2) - a natural place to hide clip transfer
  latency and to host the "voice gull".
- `AudioBus` has a `Voice` category with ducking (spec line 1521).

### 3.2 Vivox feasibility
Sources: Unity Vivox docs (audio taps, positional channels, privacy), pricing page.
- Positional channels: yes. `Channel3DProperties`: AudibleDistance (default 32),
  ConversationalDistance (default 1), AudioFadeModel (default InverseByDistance),
  AudioFadeIntensityByDistance (1.0). Units are arbitrary but must match.
  - https://docs.unity.com/en-us/vivox-unity/developer-guide/channels/positional-channel-properties
- Free tier: Vivox Voice/Text free up to 5,000 peak concurrent users, then billed in
  5,000-PCU blocks (page dated Nov 2024; recheck at M6). Fine for a co-op game.
  - https://unity.com/products/gaming-services/pricing
- Tap SDK (v16): three taps in `Unity.Services.Vivox.AudioTaps`:
  `VivoxCaptureSourceTap` (local mic "as captured by Vivox" while in a channel),
  `VivoxParticipantTap` (one remote participant's audio as received; cannot target
  yourself), `VivoxChannelAudioTap` (all remote audio). Each is a MonoBehaviour
  that requires an `AudioSource`; raw samples are read by adding a script with
  `OnAudioFilterRead(float[] data, int channels)` on the same GameObject.
  - https://docs.unity.com/en-us/vivox-unity/developer-guide/audio-taps/audio-tap-components
  - https://docs.unity.com/en-us/vivox-unity/developer-guide/audio-taps/access-audio-data
  - https://docs.unity3d.com/Packages/com.unity.services.vivox@16.3/api/Unity.Services.Vivox.AudioTaps.VivoxCaptureSourceTap.html
  - Unity staff confirm mic access shipped with v16:
    https://discussions.unity.com/threads/tracking-developer-interest-in-access-to-audio-buffers-for-the-vivox-unity-sdk.1234000/
- Caveats from the docs (treat as spike checks):
  - Taps only work while connected to a channel.
  - Tap audio plays through Unity's audio engine, not Vivox's; Vivox keeps using its own
    selected capture device regardless of Unity mic settings.
  - Adding a tap adds an AudioSource, so the local mic could be heard back by the
    player; the capture script must copy samples and zero the output buffer.
  - `OnAudioFilterRead` runs on the audio thread at Unity's output rate, interleaved:
    needs downmix, resample to 8 kHz and a lock-free handoff.
  - Behaviour of the capture tap while the mic is muted or in push-to-talk "off" is NOT
    documented. We must gate recording ourselves on PTT/mute state, not trust the tap.
  - The doc pages I could read were for 16.0 and 16.3; the project pins 16.12.1.
    Confirm `VivoxCaptureSourceTap` still exists there (spec item V18 already covers
    API-name verification).
- Speech detection: `VivoxParticipant.SpeechDetected` / `AudioEnergy` and events
  `ParticipantSpeechDetected`, `ParticipantAudioEnergyChanged` exist. Not confirmed
  for the LOCAL participant; our own RMS gate on the tap samples is simple, testable
  pure C# and avoids relying on it.
- Playing a clip on demand: Vivox cannot be fed arbitrary audio for the channel (none
  found) and we do not need it. Playback is an ordinary Unity path: decode to float,
  `AudioClip.Create` + `SetData` (main thread), 3D `AudioSource` on the gull, spatial
  blend 1, routed through `AudioBus` as `Voice`.
- Verdict: capture via `VivoxCaptureSourceTap` is probable (documented purpose "react
  to the player's mic"), playback is fully in our control. Needs a 1-day spike (4.7).
  NOT blocked by Vivox. Listener-side capture (`VivoxParticipantTap`) also works but
  only for audio that actually reached you.

### 3.3 Alternatives if the tap fails
- Unity `Microphone` API (`Microphone.Start(device, loop, lengthSec, frequency)`): free
  and simple, but two components opening one mic can clash (reported for Photon/Meta
  SDKs; not documented for Vivox) and Vivox keeps its own device. Test on target
  hardware before depending on it.
  - https://discussions.unity.com/t/microphone-input-conflict/815225
- Dissonance: has the exact hook Mirage uses (`IMicrophoneSubscriber`) but is a paid
  Asset Store product; project rule is no paid assets, so mention only.
- Steam voice (`GetVoice`): only relevant if we move to Steam transport (spec M6
  evaluation); keep out of scope.
- Opus in managed code if we want smaller clips: Concentus (pure C#, BSD-style
  licence, ~40-50% of native speed per its README). Optional; mu-law is enough (4.4).
  - https://github.com/lostromb/concentus

## 4. Recommended design: "gulls borrow your voice"

### 4.1 Overview (design A, owner-sourced, on-demand)
1. Each opted-in client captures ITS OWN mic via `VivoxCaptureSourceTap` +
   `OnAudioFilterRead`, gated by an RMS voice gate and game state.
2. Clips live only in that client's RAM ring buffer. Nothing is sent until used.
3. Host decides a gull event, picks a donor (opted-in player with >=1 clip), and asks
   that player's client for a clip.
4. The donor sends one clip to the host, host forwards to the others.
5. Host schedules playback at a shared network time; every client plays it from the
   gull's position through a squawky filter.

Authority statement (for the class comment): host = event timing, donor choice,
placement, playback time. Owner = whether to record and which clip to offer.
Clients = playback only.

### 4.2 Capture on the owner
- Pure C# `VoiceClipRecorder` (no MonoBehaviour, EditMode-testable): input float PCM +
  flags (muted, pttHeld, alive, consent, othersInEarshot); output finished clips.
- Downmix to mono, resample to 8 kHz, store PCM16 locally.
- Gate: RMS threshold with ~100 ms attack and ~350 ms hangover; clip ends after 350 ms
  silence or 3.0 s max; discard < 0.4 s; target 1-3 s. Keep best-N by loudness.
- Record only when it adds no new exposure: at least one other opted-in crew member is
  within the audible radius when the words are spoken. Then a replay reveals nothing the
  crew did not already hear live (important privacy rule, cheap to enforce, the host
  supplies the nearby count from positions it already has).
- Never record while muted, PTT not held, dead/downed (optional), in menus, or when the
  setting is off. Wipe on session end; nothing on disk, nothing in logs.

### 4.3 Sizes and caps
| Item | Value |
|---|---|
| Local format | PCM16 mono 8 kHz = 16 KB/s |
| Wire format | 8-bit mu-law (G.711) mono 8 kHz = 8 KB/s |
| Clip | 1-3 s = 16-24 KB on the wire (fits one `ReliableFragmentedSequenced` message, cap 64,000 bytes) |
| Ring per player | 8 clips x 3 s max x 16 KB/s = 384 KB worst case (192 KB if stored mu-law) |
| Crew worst case | 4 players = ~1.5 MB RAM |
| One gull event | donor -> host 24 KB, host -> 3 peers 72 KB, i.e. under 100 KB per event |
| Event rate | 1 per ~60-120 s per crew => about 1-2 KB/s average, bursty |
Optional later: Concentus Opus at 16 kbps = 2 KB/s, ~4-6 KB per clip. Not needed for v1.
NGO facts used: non-fragmented messages are capped at ~1300 bytes, fragmented delivery
up to 64,000 bytes (NGO custom messages docs; verify against the pinned NGO version).
- https://mp-docs.dl.it.unity3d.com/netcode/2.3.2/advanced-topics/message-system/custom-messages

### 4.4 Gull side and sync
- Host (inside the thief/DayDirector logic) rolls a "voice gull" event, spawns or
  repurposes a perched gull near a target player, and sends `VoiceRequestRpc(requestId,
  donorId)` to the donor.
- The telegraph (circle and squawk 4-6 s, spec 15.2) hides the 0.5-1.5 s transfer.
- Donor sends `NamedMessage` (requestId + mu-law bytes + sample count) to host; host
  validates (sender == donor, size <= cap, request still open) and forwards to peers.
  Peers may include the donor (hearing yourself squawked back is the joke; Mirage
  defaults to this at 50% volume).
- Host then sends `PlayVoiceClientRpc(requestId, gullRef, playAtServerTime)` with
  `playAtServerTime = NetworkManager.ServerTime + ~0.3 s`. Each client decodes and
  schedules with `AudioSource.PlayScheduled`/delayed play. A client that lacks the clip
  by the deadline skips it (no catch-up). Mirage has no such scheduling; this is the
  improvement for "everyone hears it at the same moment".
- Playback: AudioSource on the gull, spatial blend 1, rolloff ~2-25 m, `AudioHighPassFilter`
  + `AudioLowPassFilter` + pitch +20..40% (squawky), small distortion/chorus, then
  `AudioBus` category `Voice`. Gull may repeat its clip 1-3 times during its stay, then
  the clip is freed on all receivers.
- Donor choice: random with no-repeat pool (like Mirage), weighted to avoid the nearest
  player so the squawk is not just an echo of who is standing there.
- Frequency: tied to existing gull spawn cadence plus a cooldown (suggest >= 45 s per
  crew), host-tunable difficulty setting.

### 4.5 Design B, listener-side (no amendment, weaker)
Each client records OTHER players with `VivoxParticipantTap` (what it already received)
and the host only sends "gull plays player P, clip k, at time T". Pros: zero audio over
NGO, spec rule untouched, Skinwalkers-like. Cons: each client plays a DIFFERENT clip;
cannot mimic yourself; clips exist in everyone else's RAM regardless of the donor's
wishes; opt-out must be enforced by every listener honouring a replicated flag (weak
consent); only audible range gets captured. Use only if the amendment is refused.

### 4.6 Consent / opt-out
- Setting: "Let gulls borrow my voice" (Settings > Audio and in the lobby), per player,
  NOT overridable by the host. When off: the tap script never subscribes/records, the
  ring is empty, the host never picks that player. Default: ON after a one-time
  first-launch notice that explains the feature; consider default OFF/opt-in for regions
  where law requires it (see 5).
- Visible state: small gull-ear icon on the HUD whenever recording is armed, and the
  lobby roster shows who is opted in.
- Local only: clips are never saved, uploaded to any service, or logged; wiped at session
  end or when the setting is turned off mid-session.

### 4.7 Spike plan (about 1 day, do before committing)
1. In a scratch scene with Vivox joined, attach `VivoxCaptureSourceTap` + capture script;
   confirm samples arrive, rate/channels, latency, and no self-echo.
2. Check behaviour while Vivox input is muted and with PTT.
3. Round-trip a 2 s mu-law clip over NGO to a second editor and play it at a world
   position; confirm size/limits against the pinned NGO and UTP versions.
4. Fallback test: `Microphone.Start` alongside Vivox on the dev machine.

## 5. Licensing and terms

- Vivox (Unity privacy overview): voice data is "ephemeral" on Vivox's side; privacy and
  wiretapping laws may require consent depending on end-user region; the DEVELOPER is
  the data controller, must implement client-side opt-in AND opt-out, and must write
  their own privacy policy; children need verified parental consent where law requires.
  - https://docs.unity.com/en-us/vivox-core/privacy-overview
- Recording for moderation ("Safe Voice") is a separate Unity product that requires
  explicit, informed, affirmative consent. We are NOT using it; it is the closest
  published precedent for what consent should look like.
  - https://docs.unity.com/ugs/en-us/manual/safe-voice/manual/requirements
- I did not find a Vivox/Unity ToS clause that forbids reading the local mic via the
  capture tap, but I did not read the full ToS; confirm before shipping. Our recording
  is our own processing of local audio, separate from Vivox's service.
- Our obligations: memory-only storage, wipe on session end, no transmission outside the
  crew's session, clear disclosure in the privacy policy and store page, easy opt-out.
  Some regions may require opt-in before ANY recording, so a first-launch consent screen
  (default off in those regions) is the safe choice. This is research, not legal advice.
- Concentus (if used): BSD-style licence, compatible with "no paid assets". Dissonance
  and Steam voice: paid / Steam-only, mention only.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Capture tap unavailable/unreliable in 16.12.1 or unmuted-state unknown | Spike first; own PTT/mute gating; fall back to `Microphone` API |
| Spec conflict ("never through NGO") | User approves narrow amendment, or use design B |
| Harassment / offensive replay, esp. with strangers via join codes | Proximity-witness rule, opt-out, clip cap, rate limit, host can disable gulls voice; consider friends-only default |
| Legal: wiretap/GDPR/children | Consent screen, memory-only, privacy policy, legal check before launch |
| Desync/latency | Host-scheduled play time, 0.3 s lead, drop-if-late, telegraph hides transfer |
| Abuse of the clip message (oversized/forged) | Host validates sender, request id, size cap, rate limit |
| Audio-thread bugs (allocations, locks) | Pre-allocated ring, lock-free handoff, pure-C# tested recorder |
| Voice quality (8 kHz, mu-law) | Fine behind a squawk filter; raise to 12-16 kHz if needed |
| Local echo of mic through tap's AudioSource | Zero the buffer after copying |
| Steam transport later | Vivox still needs UGS sign-in (spec 16.1); design is transport-agnostic |
| Mirage-style sync issues with other voice mods | N/A to us (no mods), but keep one playback path |

## 7. Size estimate
- Prereqs: M5 seagull thief + M6 Vivox positional voice (spec 16.1).
- Voice-gull feature on top of those: M (recorder + codec + named-message transfer +
  host scheduling + playback filters + settings/consent UI + EditMode tests for recorder,
  gate, ring and codec; PlayMode test for two-client delivery).
- If Vivox must also be built from scratch for this: L.
- Suggested slot: M6, after Vivox proximity voice is stable (a stretch goal there).

## 8. Source list
- https://github.com/qwbarch/mirage and raw files (CHANGELOG.md; package/mirage-lc/src/Mirage/Hook/Microphone.fs, Hook/Dissonance.fs, Unity/MimicVoice.fs, Unity/AudioStream.fs, Unity/MimicPlayer.fs, Domain/Config.fs, Domain/Setting.fs)
- https://thunderstore.io/c/lethal-company/p/qwbarch/Mirage/
- https://thunderstore.io/c/lethal-company/p/RugbugRedfern/SkinwalkersExperimental/
- https://steamcommunity.com/app/1966720/discussions/0/4364627964252165209
- https://lethal.miraheze.org/wiki/Masked and https://lethal.miraheze.org/wiki/Walkie-Talkie
- https://dissonance.readthedocs.io/en/latest/Tutorials/UsingIMicrophoneSubscriber.html
- Vivox docs listed in section 3.2 and 5; https://unity.com/products/gaming-services/pricing
- https://github.com/lostromb/concentus
- Our spec: docs/superpowers/specs/2026-10-09-fishgame-design.md (lines 594, 1521, 2110-2120, 2196-2201)

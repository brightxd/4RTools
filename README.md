<p align="center">
  <img src="/assets/images/combo-tools.png">
</p>

# 4RTools
This is an all-in-one tool for **Ragnarök Online** servers. You will be able to Autopot, Spam Skill, Use Macro Songs and much more. Hope you enjoy it and collaborate with us. If you have found a bug or want to request a feature, feel free to open an issue!

**THIS IS A FREE TOOL, YOU CAN MAKE EVERYTHING YOU WANT UNDER MIT LICENSE. WE ARE OPEN FOR IDEAS AND COLLABORATIONS**

*Made from community TO community*.

Your Feedback is Welcome!

**Discord:** https://discord.gg/HRWvG5ut
**Website:** https://www.4rtools.com.br/

<img src='assets/images/ragnarok-icon.png' width='40'>

## Running the project
This project was created using Visual Studio 2022, just open `4RTools.sln` in Visual Studio, and you'll be able to run and generate your own releases.

## Features
- [x] ON/OFF Button (with shortcut key)
- [x] Autopot
- [x] Autobuff status
- [x] Manage Profiles
- [x] AHK Spammer
- [x] Auto Refresh Spammer
- [x] Autobuff Stuffs
- [x] Autobuff skills
- [x] Song Macro
- [x] Macro Switch/Macro Chain
- [x] ATK x DEF Mode switch

## Conditional macro chains

Macro Switch steps can be guarded without changing existing profiles:

- `CD(ms)` and `Cast(ms)` prevent a step from being sent during its local
  cooldown or cast animation.
- `W/Next` makes a setup step fire only when the immediately following step is
  ready.
- `Wait CD` keeps a step pending while its cooldown is active instead of
  restarting the chain and replaying earlier setup skills.
- `Status ID` plus `Has status` gates a step against the status buffer read from
  the selected client. Use `-1` to disable the status gate.

For a strict local sequence such as `Skill 1 -> Lex -> Skill 3`, configure:

1. Slot 1: `Skill 1`.
2. Slot 2: `Lex`, with `W/Next` enabled.
3. Slot 3: `Skill 3`, with its real `CD(ms)` and `Wait CD` enabled.

The chain then advances to `Lex` only when slot 3 is locally ready, sends
`Skill 3` immediately after `Lex`, and waits on slot 3's cooldown without
replaying slot 1. These guards guarantee the order in which 4RTools dispatches
the input messages; they cannot prove server acceptance. For that, the client
must expose an observable success status in the character status buffer or the
server must provide an authorized acknowledgement/API. Target-only effects
cannot be confirmed through the existing self-status buffer.

#### References
https://github.com/k1ngJ/dtAP

## Collaborators
<a href="https://github.com/4RTools/4RTools/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=4RTools/4RTools" />
</a>

# Use Cinemachine for the systemic third-person camera

RB Project will replace its semi-isometric gameplay camera with a Cinemachine 3 third-person follow rig, while project-owned services remain responsible for camera state, Aim Point resolution, character and weapon profiles, cursor ownership, and cinematic handoff. This keeps collision, blending, and impulse behavior on the installed camera framework instead of expanding the bespoke isometric controller, while preserving the existing combat, skill, party, and save-system boundaries.

The first release targets mouse and keyboard, uses a fixed right shoulder, does not add snap-to-cover or modify level geometry, and falls back to camera push-in plus actor fading in tight spaces.

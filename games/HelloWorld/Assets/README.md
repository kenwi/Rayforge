# Hello World assets

All three runtime files are small, redistributable placeholders generated for this demo.

| File | Origin | License |
|---|---|---|
| `logo.png` | Generated for the M0 spike (64x64 RGBA) | MIT, same as Rayforge |
| `blip.ogg` | `ffmpeg -f lavfi -i "sine=frequency=440:duration=0.15"`, fade + volume 0.4, mono 44.1 kHz, Vorbis | MIT, same as Rayforge |
| `ui.ttf` | Subset of **Liberation Sans 2.1.5 Regular** (ASCII 0x20-0x7E, no hinting), renamed `Rayforge Sans` per the OFL reserved-name rule | SIL OFL 1.1, see `licenses/OFL-LiberationSans.txt` |

`ui.ttf` is a modified version: subset and renamed. It is not the original Liberation Sans and must not be
distributed under that name. The OFL requires the license to travel with the font; it lives in
`licenses/OFL-LiberationSans.txt` and is not shipped to the web bundle.

Only files directly under `Assets/` (and its subfolders, excluding `*.md` and `licenses/`) are copied into the
host. See `../Assets.props`.

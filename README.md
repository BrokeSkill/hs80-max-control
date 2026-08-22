<p align="center">
  <img src="src/Hs80.App/Assets/app_banner.png" alt="HS80 MAX Control" width="96"/>
</p>

<p align="center">
  <b>HS80 MAX Control</b><br/>
  Your headset, without iCUE.<br/>
  <i>LED control · battery &amp; ETA · firmware sound warnings · tray · 33 MB idle</i>
</p>

# HS80 MAX Control

A lightweight Windows desktop app for the Corsair HS80 MAX wireless headset. No iCUE, no bloat, no cloud, no telemetry. Just the headset, controlled directly over its USB dongle.

## What it does

- **LED control** - set independent colors for the earcups and the mic tip, with brightness. (The last applied color is restored on launch and re-asserted automatically when the headset reconnects after standby.)
- **Event warnings** - pick any of the headset's own firmware sounds (all 46 voice prompts and tones, dumped live from the device) and bind them to events: battery low/critical, charging started/complete, headset connect/disconnect, mic mute/unmute. Per-event volume, custom WAV/MP3 files, tray toasts, and LED color reactions are all available.
- **Battery card** - live percentage from the headset meter, charge state, ETA until drained or full (linear fit over persisted history, may be inaccurate)
- **Now playing** - title, artist, album, and thumbnail from the Windows media session, read-only.
- **System tray** - minimize/close to tray so warnings keep working; tray icon reflects charge level; quick LED on/off from the tray menu.
- **Start with Windows** - optional, via a plain HKCU Run key. No admin needed.
- **Developer mode** - optional loopback HTTP server (127.0.0.1, off by default) for scripting LED colors and reading battery status. (Useful for OpenRGB etc.)

## Why not iCUE

iCUE is heavy. This app is one self-contained process with an idle footprint around 33 MB private working set, no child processes, and no background services. It targets a single 2-second poll tick for battery and an instant 150 ms mic check; nothing else runs when idle. The published exe is a single self-contained file with no runtime install.

## How it talks to the headset

The app speaks the dongle's HID control protocol directly (iface 4, usage page 0xFF42). The battery meter is read from the headset child property 0x0F (raw LE16 / 10), which is the live value. LED writes use the verified open-write-close handle sequence on resource 0x22, with the render mode held in software so colors persist.

## Credits

- **ToastKiste21** - original reverse engineering of the HS80 MAX protocol and firmware ([corsair-hs80-max-re](https://github.com/ToastKiste21/corsair-hs80-max-re), MIT). His work documented the RACE channel and the voice-prompt partition.
- This project independently re-verified the protocol against the current firmware after noticing his build was older than my own unit in use. All 46 voice prompt files shipped in this app were dumped live from the headset over the RACE channel (RFCOMM 21) and extracted from the ROFS partition, not copied from his release.

## Building

Requires .NET 10 SDK on Windows:

    dotnet publish Hs80.App/Hs80.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o out

The exe lands in `out/Hs80.App.exe`. No runtime install needed.

## Layout

- `Hs80.Core` - protocol layer, LED controller, battery reader, event poller, ETA, config
- `Hs80.Audio` - NAudio playback, preset catalog
- `Hs80.Http` - loopback LED/battery HTTP server
- `Hs80.App` - WPF shell
- `Hs80.App/Assets` - the 46 firmware voice prompts + icons

## License

MIT. The firmware audio files are Corsair's; they are shipped for interoperability with a device you own.

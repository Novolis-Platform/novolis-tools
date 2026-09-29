# Novolis.Tools.Android.Cli

`novolis-android` is the host-side Android deployment and diagnostics command
for Novolis developers. It uses `Novolis.IO.Mobile.Android` and the local
Android SDK platform-tools installation.

## Prerequisites

- Android SDK platform-tools with `adb`
- USB debugging enabled and this computer authorized on the phone
- Use `--serial` when more than one ready device is connected

## Common workflow

```powershell
novolis-android doctor
novolis-android devices
novolis-android app install d:\build\ReadAloud.apk --package com.novolis.readaloud --launch --yes
novolis-android logcat --package com.novolis.readaloud
novolis-android screen shot d:\evidence\readaloud.png
novolis-android ui dump d:\evidence\readaloud.xml
novolis-android diagnostics collect d:\evidence\readaloud --package com.novolis.readaloud
```

Use `--json` for automation. Destructive actions require `--yes`, and
diagnostic output is redacted by default. The `info` command also redacts
device identifiers; use `--raw` only for local troubleshooting.

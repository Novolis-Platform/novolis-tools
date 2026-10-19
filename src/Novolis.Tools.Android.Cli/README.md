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
novolis-android inventory d:\evidence\android-inventory --serial R58M12ABCDE
novolis-android privacy audit d:\evidence\android-privacy --serial R58M12ABCDE
```

Use `--json` for automation. Destructive actions require `--yes`, and
diagnostic output is redacted by default. The `info` command also redacts
device identifiers; use `--raw` only for local troubleshooting. The
`inventory` command writes `inventory.json`, `device-info.txt`,
`packages.json`, `launchers.json`, `accounts.json`, `settings.json`, and
`network.json`; `privacy audit` additionally writes a compact
`privacy-audit.json`. Network evidence retains only transport, VPN policy,
and hostnames observed in Android connectivity probes; it does not retain
IP addresses or Wi-Fi names.
transport and hardware identifiers are redacted unless `--raw` is supplied.

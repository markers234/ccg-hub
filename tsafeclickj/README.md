# TsafeclickJ

A keyboard and mouse auto clicker for Windows 10 and 11. It's one small `.exe` and needs nothing else installed, because it uses the .NET Framework 4 that comes with Windows.

## Features
- **Mouse clicker**: set the interval in hours, minutes, seconds and milliseconds (default **2 ms**, about 500 clicks per second). You can click with the left, right, middle or side buttons, and do single, double or triple clicks. You can also set a hold time, a repeat count or run until stopped, a fixed position with a 3-second picker, a random pixel offset and a random delay to make clicking look more human.
- **Keyboard clicker**: presses any key, with or without Ctrl, Shift, Alt or Win. It has the same interval, hold, delay and repeat options as the mouse clicker.
- **Sequence**: build a chain of steps. A step can press, hold down or release a key; press or release a mouse button (at the cursor or at fixed X/Y); move the mouse; scroll; type text; or wait. Each step has its own hold time, delay after and repeat count. You can loop the chain forever or N times, with a gap between loops. Steps can be reordered and duplicated, saved to and loaded from `.tsq` files, and the last sequence is saved automatically.
- **Recorder**: records your real key presses and mouse clicks, with their timing, and turns them into sequence steps.
- **Global hotkeys** (you can change them): F6 mouse, F7 keyboard, F8 sequence, F9 record, **F10 stops everything**.
- Precise timing: uses 1 ms timer resolution plus a short spin-wait.
- Game mode (sends scan codes), start delay, auto-stop timer, always on top, minimize on start, beep, dark and light themes, a live clicks-per-second counter, and saved settings.

## Build it yourself
On Windows: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:TsafeclickJ.exe TsafeclickJ.cs`

With Mono: `mcs -target:winexe -optimize+ -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:TsafeclickJ.exe TsafeclickJ.cs`

Windows SmartScreen may warn about an unsigned exe the first time you run it. Click "More info", then "Run anyway".

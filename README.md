# SmoothAutoRun

Windows 10/11 system utility combining SmoothScroll, AutoRun manager, Firewall control, FPS overlay, and mouse gestures.

## Features

- **SmoothScroll** - Smooth mouse wheel scrolling with easing functions and per-app profiles
- **AutoRun** - Manage startup entries from Registry, Startup folders, Task Scheduler, and Services
- **Firewall** - Block internet for specific apps or toggle all internet
- **Overlay** - FPS/CPU/GPU overlay for games (auto-disables for Vanguard)
- **Gestures** - Mouse gesture recognition (hold right mouse button + move)

## Installation

1. Download `SmoothAutoRun.zip` from [Releases](https://github.com)
2. Extract to any folder
3. Run `SmoothAutoRun.exe` (requires Administrator rights)
4. Confirm UAC prompt

## System Requirements

- Windows 10/11 64-bit
- .NET 8 Runtime (included in self-contained build)
- Administrator privileges

## Build from Source

```bash
git clone https://github.com/yourrepo/SmoothAutoRun
cd SmoothAutoRun
dotnet restore
dotnet build -c Release
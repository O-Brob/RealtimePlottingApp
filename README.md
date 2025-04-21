# RealtimePlottingApp

[![License](https://img.shields.io/github/license/O-Brob/RealtimePlottingApp)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-ff69b4)](#)
[![AvaloniaUI](https://img.shields.io/badge/UI-AvaloniaUI-lightgrey)](https://avaloniaui.net)
[![Release](https://img.shields.io/github/v/release/O-Brob/RealtimePlottingApp?include_prereleases)](https://github.com/O-Brob/RealtimePlottingApp/releases)

## Overview

RealtimePlottingApp is a Linux & Windows application written in C# for real-time plotting of CAN and UART data. It is designed for engineers and developers working with embedded systems, and provides an intuitive interface to visualize data streams with high performance and low latency.

---

## 🗂️ Table of Contents

1. [🎉 Features](#-features)  
2. [📦 Self-contained Releases](#-self-contained-releases)
3. [💾 Manual Installation](#-manual-installation)    
4. [📄 License](#-license)  

---

## 🎉 Features

- 🖥️ **Multi‑Platform CAN/UART Support**  
  - SocketCAN interface on Linux
  - Peak Systems CAN drivers on Windows
  - C# SerialPort w/ BaseStream for efficient UART

- 📈 **Real‑Time Plotting**  
  - Live CAN & UART data streams with sub‑millisecond latency
  - Supports plotting of multiple variables at once
  - Threaded architecture for smooth rendering at high data rates

- ⚙️ **Highly Configurable UI**  
  - ⏱️ Adjust update frequency on‑the‑fly  
  - 🎚️ Change how many variables display simultaneously  
  - 👁️‍🗨️ Toggle visibility of individual variables  
  - ✏️ Rename variables, and see them in a legend
  - 🎨 Color-coded variables using the [coloropt](https://tsitsul.in/blog/coloropt/) palette — optimized for print and colorblind accessibility

- 🔍 **Interactive Plot Controls**  
  - Right‑click drag to zoom X and/or Y axes independently
  - 📐 Auto‑scale view to fit all points with middle mouse button 
  - 🔄 Double‑click graph to display current render time and FPS
  - 📷 Right click to save current view as an image file 
  - 🔎 Box zoom by holding down middle mouse button

- 🔄 **History Mode**  
  - Automatically enter “History Mode” on disconnect  
  - Pan & zoom through all buffered data

- 🎯 **Triggering**  
  - Rising‑edge triggers in **Single Trigger** & **Normal Trigger** modes  
  - 💎 Diamond marker highlights the trigger point  
  - ✅ Choose which variables can fire triggers
  - 🎛️ Draggable trigger‑level line on the Y-Axis with live feedback 

- 🛠️ **Robust Communication Interface**  
  - Footer‑mounted connection status & error messages  
  - Built‑in input validation for CAN/UART parameters  
  - 📡 Seamless handling of UART timestamp overflows  

- 🏗️ **Reactive MVVM Architecture**  
  - Built with AvaloniaUI & ReactiveUI  
  - Sidebar for interface & plot configuration  
  - Header toggle to show/hide sidebar or plots  
  - 💡 Tooltips on nearly every control element

---

## 📦 Self-contained Releases

Self‑contained binaries for the latest release.

| Platform       | Download                                                                 |
| -------------- | ------------------------------------------------------------------------ |
| Windows x64    | Not available yet. |
| Linux x64      | Not available yet. |

---

## 💾 Manual Installation

### 🔧 Prerequisites

- [.NET 9.0 SDK or later](https://dotnet.microsoft.com/download)  
- Supported platforms: **Linux** and **Windows**

### 📥 Clone & Build
```bash
git clone https://github.com/O-Brob/RealtimePlottingApp.git
cd RealtimePlottingApp
dotnet restore
dotnet build -c Release
```

### 🚀 Publish & Run
Linux x64:
```bash
dotnet publish RealtimePlottingApp \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o publish/linux-x64
```
```bash
chmod +x publish/linux-x64/RealtimePlottingApp
./publish/linux-x64/RealtimePlottingApp
```

Windows x64:
```powershell
dotnet publish RealtimePlottingApp `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o publish\win-x64
```
```powershell
cd .\publish\win-x64
.\RealtimePlottingApp.exe
```

---

## 📄 License

This project is licensed under the MIT License.  
See the full text in the [LICENSE.md](LICENSE.md) file in the repository root.

### 🔗 Third‑Party Licenses & Notices

Bundled dependencies and their licenses are listed in [third-party-licenses.md](third-party-licenses.md).

---

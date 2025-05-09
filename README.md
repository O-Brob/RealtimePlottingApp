# RealtimePlottingApp

[![License](https://img.shields.io/github/license/O-Brob/RealtimePlottingApp)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-d069b4)](#)
[![AvaloniaUI](https://img.shields.io/badge/UI-AvaloniaUI-blue)](https://avaloniaui.net)
[![Release](https://img.shields.io/github/v/release/O-Brob/RealtimePlottingApp?color=dark-green&include_prereleases)](https://github.com/O-Brob/RealtimePlottingApp/releases)

## Overview

RealtimePlottingApp is a Linux & Windows application written in C# for real-time plotting of CAN and UART data. 
It is designed for engineers and developers working with embedded systems, 
and provides an intuitive interface to visualize data streams with high performance and low latency.

The included UART C library is an efficient solution for STM32 microcontrollers.
It works seamlessly with the C# application to enable timestamped transmission of data variables.
Note that the required LL drivers and STM32 firmware are not included and must be installed separately to use this library.

---

## 🗂️ Table of Contents

1. [🎉 Features](#-features)  
2. [📦 Self-contained Releases](#-self-contained-releases)
3. [💾 Manual Installation](#-manual-installation)
4. [🙋 User Guide](#-user-guide)
5. [📄 License](#-license)  

---

## 🎉 Features

- 🖥️ **Multi‑Platform CAN/UART Support**  
  - SocketCAN interface on Linux
  - Peak Systems CAN on Windows ([PEAK-System CAN drivers](https://www.peak-system.com/Drivers.523.0.html?&L=1) required)
  - C# SerialPort w/ BaseStream for efficient UART

- 📈 **Real‑Time Plotting**  
  - Live CAN & UART data reading streams with sub‑millisecond latency
  - CAN data is timestamped at receival, to allow connecting to a can-bus without extra configuration
  - UART data is timestamped at transmission using the format in the provided C library, to allow higher accuracy
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

- 🧱 **Block Diagram Visualization**  
  - Each plotted variable can be shown in a block diagram as its own block on the X-axis  
  - Block height on the Y-axis represents the most recently read value for the variable  
  - Provides a simpler comparison of current values at a glance, without tracing the line graph

- 💾 **Communication Interface Config Persistence**  
  - Save CAN and UART configuration parameters to a config file  
  - Load saved configs via a file-explorer dialog to quickly ready the application for connection  
  - Eliminates repetitive manual entry of parameters when working in the same microcontroller traffic environment

- 🏗️ **Reactive MVVM Architecture**  
  - Built with AvaloniaUI & ReactiveUI  
  - Sidebar for interface & centralized plot configuration  
  - Header toggle to show/hide sidebar or plots  
  - 💡 Tooltips on nearly every control element

---

## 📦 Self-contained Releases

Self‑contained binaries for the latest release.

| Platform       | Download                                                                 |
| -------------- | ------------------------------------------------------------------------ |
| Windows x64    | [Download v1.0.0 (.zip)](https://github.com/O-Brob/RealtimePlottingApp/releases/download/v1.0.0/RealtimePlottingApp-v1.0.0-win-x64.zip) |
| Linux x64      | [Download v1.0.0 (.tar.gz)](https://github.com/O-Brob/RealtimePlottingApp/releases/download/v1.0.0/RealtimePlottingApp-v1.0.0-linux.x64.tar.gz) |

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

## 📝 User Guide

### 1. Quick Start
0. If UART is being used, configure the embedded system to store and flush data using the provided UART C library.
   The baud rate, number of variables transmitted, and variable data size will need to be input in the application to match!

1. Launch the app (`RealtimePlottingApp.exe` or `./RealtimePlottingApp`)
2. Select your interface (CAN / UART) and fill the parameters. Hover the input fields or checkboxes for tooltips!
3. Once config parameters have been input in the right format, press **Connect**
4. See whether the connection succeeded or if an error is displayed in the footer
5. On successful connection, the line plot should plot incoming data. Right-click the graph and Auto-Scale if it is not visible!
6. To enable the **Block Diagram**, press "Toggle Block Diagram" via the View dropdown in the header

### 2. Saving and Loading configuration parameters
- **Saving a Config**  
  1. Click the “Save Config” option in the header's File dropdown  
  2. Choose filename & location in the file-explorer dialog
  3. Any current configurations for CAN and UART has now been saved  
- **Loading a Config**  
  1. Click the “Load Config” option in the header's File dropdown  
  2. Select your previously saved file

### 3. Plot Modes and Plot Configuration
- **Plot Configuration**
  - Press the "Plot Config" button at the top of the sidebar to configure the plot.
    - Change the number of points to show in the line graph using the "Number of points to show" slider
    - Change the update frequency (in ms) of the graph using the "Update Frequency" slider
    - Set a trigger mode and enable a horizontal trigger under "Trigger Mode"
    - Once a connection has been made; rename, toggle visibility or triggerability of variables under "Plotted Variables"
- **Line Plot** 
  - While plotting, the graph will follow the last `n` points on the x-axis, as configured in the Plot Config
  - On disconnect, history mode will be enabled, allowing full zoom and pan control over all historical data from the most recent plotting
  - Zoom: left-click drag to pan the graph, right-click-drag to zoom on a specific axis or box-zoom with middle-mouse
  - Middle-mouse click: Automatically scales the plot view to all current data in the graph 
- **Block Diagram**  
  - Enable via Toggle Block Diagram in the header's View dropdown menu  
  - Each variable has its own x-axis block: height represents the latest received value  

### 4. Triggering & History
- **Set a Trigger**  
  1. Select “Single” or “Normal” mode in the Plot Config window of the sidebar
  2. Press "Enable Trigger", to make a trigger level appear in the line graph 
  3. Drag the trigger level up/down with your mouse to position it as needed
  4. Enable triggerability of a variable that is being plotted under "Plotted Variables" in the Plot Config to add it to the trigger channel
  5. Firing points are centered in the plot and marked with a black diamond on the rising edge
- **History Mode**  
  - Auto-activates on disconnect, allowing full view and control of the graphs 
  - Pan/zoom through past data with scroll & drag  

### 5. Saving & Exporting a plot snapshot
- **Image Snapshot**: Right-click plot --> "Save Image" to save the currently visible plot as a file

### 6. Troubleshooting
- _My plot is flickering!_: Double click the plot to enable debugging mode, and ensure the configured update frequency of the plot is longer than the time it takes to render the current frames.  
- _The Connect button is always greyed out for UART!_: Make sure the COM Port is set to "COMx" for Windows, where x is a number, and "/dev/*" for Linux, where * is any subsequent substring.
- _The footer says access to my serial port is denied_: This can happen not only when a port does not exist, but also when the application is run by a user or group that lack permissions to access the port. Try launching the application with super user privileges.

---

## 📄 License

This project is licensed under the MIT License.  
See the full text in the [LICENSE.md](LICENSE.md) file in the repository root.

### 🔗 Third‑Party Licenses & Notices

Bundled dependencies and their licenses are listed in [third-party-licenses.md](third-party-licenses.md).

---

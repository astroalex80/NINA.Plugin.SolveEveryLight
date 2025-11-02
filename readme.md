# N.I.N.A. Plugin Solve Every Light

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![License: MPL](https://img.shields.io/badge/License-MPL-green.svg)](LICENSE.txt)

A plugin for [N.I.N.A.](https://nighttime-imaging.eu/) that plate solves automatically every light frame (optionally snapshots) and writes the astrometric solution to the header of FITS or XISF files.

---

### Features
- plate solves each frame on runtime and stores WCS in header   
- multiple options available  
- Optimized solver settings can be used without changing the general solver settings
---
### Description
When enabled, the plugin automatically plate solves each light frame (optionally for snapshots) at runtime.
If no telescope/target coordinates or focal length are provided the blind solver defined under Options > Plate Solving is used. 
When plate solving is successful, the astrometric solution is written to the image header of FITS or XISF files.

This is particular useful for applications such as variable star or other photometric observations and their processing, each frame to have an astrometric solution already stored. 
NOTE: Using the plugin may slightly increases the time to save a frame, as the image is plate solved before being written to disk.

---

### Usage
- Enable the plugin in the N.I.N.A. plugin settings.
- Configure options as needed.
- Start imaging as usual; the plugin will automatically plate solve each light frame and write the astrometric solution to the image header.

---

### Requirements
- [N.I.N.A. 3.x](https://nighttime-imaging.eu/)  

### Installation
1. Download the latest release from [GitHub Releases](../../releases)  
2. Copy the `.dll` into the N.I.N.A. plugins folder  
3. Restart N.I.N.A.
4. Check plugin options and enable the plugin

---

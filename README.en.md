# Curio - Mouse Cursor Installer

[![Latest Release](https://img.shields.io/github/v/release/Ford614/Curio?label=Latest%20Release)](https://github.com/Ford614/Curio/releases/latest)
[![GitHub Stars](https://img.shields.io/github/stars/Ford614/Curio?style=flat)](https://github.com/Ford614/Curio/stargazers)
[![GitHub Downloads](https://img.shields.io/github/downloads/Ford614/Curio/total?label=Downloads)](https://github.com/Ford614/Curio/releases)

[🇯🇵 日本語](README.md) | [🇺🇸 English]

**[⬇️ Download Curio](https://github.com/Ford614/Curio/releases/latest)**　**[⭐ Star on GitHub](https://github.com/Ford614/Curio)**　**[❤️ Support / Donate](https://ko-fi.com/ford614)**

---

`Curio` is a mouse cursor management and editing application for Windows 10 / 11.

It supports importing `.cur`, `.ani`, and `.zip` files through various methods, including downloads from web distribution sites, drag and drop, and Windows' "Open with" functionality. Curio automatically detects and categorizes cursor files, then registers and manages them as cursor schemes that can be selected from Windows.

Curio also includes a built-in Paint Editor for editing CUR / ANI cursors and creating cursors from PNG / GIF files.

---

## 🔥 Main Features

### 1. ✨ Modern / OLD UI Styles

* Supports two UI styles: **Modern** and **OLD**.
* Switch between UI styles at any time from the Settings screen.
* Supports three color themes: **System**, **Light**, and **Dark**.
* UI style and theme settings are saved to `%LOCALAPPDATA%\Curio\settings.json` and automatically restored when Curio starts.

### 2. 🌐 Add Cursors Directly from the Web

* Browse cursor distribution websites using Curio's built-in web browser.
* Automatically detects `.cur`, `.ani`, and `.zip` downloads and imports them into Curio.
* The default website can be changed from Settings.

### 3. 📥 Drag and Drop

You can drag and drop the following files and folders directly from Windows Explorer into Curio:

* `.cur`
* `.ani`
* `.zip`
* Folders containing cursor files

### 4. 🔗 Windows File Association, "Open with", and "Send to" Support

* Open files in Curio using Windows' "Open with" menu.
* Send files to Curio through Windows file operations such as "Send to".
* Supports loading files through command-line arguments.

### 5. 🛡️ Strict File Import Security

Curio includes several protections to safely handle files obtained from external sources.

* Automatically blocks executable files such as `.exe`, `.msi`, `.bat`, `.cmd`, and `.ps1`
* Protects against directory traversal attacks (Zip Slip) when extracting ZIP files
* Performs file size limit checks
* Automatically excludes unnecessary non-cursor files

Curio does not execute imported executable files.

### 6. 🔍 Automatic Detection & Keyword Mapping

Curio automatically determines the role of a cursor based on its filename and assigns it to the appropriate Windows cursor role.

Examples include:

* Normal Select
* Help Select
* Working in Background
* Busy
* Text Select
* Handwriting
* Unavailable
* Resize
* Move
* Alternate Select
* Link Select
* Location Select
* Person Select

and more.

### 7. 🖱️ Cursor Preview

Preview `.cur` and `.ani` files directly in Curio.

### 8. 🔐 No Administrator Privileges Required

Curio uses per-user settings and storage locations, so administrator privileges are normally not required.

Cursor schemes are managed on a per-user basis.

### 9. 📁 Configurable Storage Location

You can change the storage location for installed cursor files from Settings.

Default location:

```text
%LOCALAPPDATA%\Curio\Schemes\
```

### 10. 📋 Cursor Scheme Management

Manage registered cursor schemes directly from Curio.

* View registered schemes
* Select schemes
* Apply schemes
* Delete schemes

### 11. 🌐 Japanese / English UI

Curio supports the following languages:

* Japanese (ja-JP)
* English (en-US)

You can switch languages from Settings.

The main screen, Settings, Paint Editor, ANI timeline, and major dialogs are localized according to the selected language.

### 12. 🎨 Built-in Paint Editor

Curio includes a built-in Paint Editor for creating and editing cursor files.

Supported features include:

* CUR editing
* ANI frame editing
* PNG import
* GIF import
* Pen
* Eraser
* Eyedropper
* Fill
* Rectangle selection
* Copy / Paste
* Undo / Redo
* Hotspot editing
* Pixel grid
* Canvas resizing
* Adding frames
* Duplicating frames
* Deleting frames
* Reordering frames
* Per-frame duration
* Animation preview
* ANI export

You can load and edit CUR / ANI files or create cursors from PNG / GIF files.

---

## 🛠️ System Requirements

* **OS:** Windows 10 / Windows 11
* **Architecture:** x64
* **Web browser:** Microsoft Edge WebView2 Runtime

The official Curio distributions (MSI / ZIP) are built as **.NET 10 self-contained** applications.

Therefore, you do not need to install the .NET 10 Desktop Runtime separately to run Curio.

The Microsoft Edge WebView2 Runtime is required for Curio's built-in web browser functionality.

---

## 📦 Installation

### MSI

The MSI version is recommended for normal installation.

1. Download `Curio-v1.0.0-win-x64.msi` from GitHub Releases.
2. Double-click the MSI file to start the installer.
3. Select the installation directory.
4. Select whether to create Start Menu and Desktop shortcuts.
5. Run the installation.
6. Launch Curio from the Start Menu or Desktop.

### ZIP

Use the ZIP version if you want to use Curio without installing it.

1. Download `Curio-v1.0.0-win-x64.zip` from GitHub Releases.
2. Extract the ZIP file to any location.
3. Run `Curio.exe` from the extracted folder.

The ZIP version does not require a Windows installation.

---

## 📖 Usage

### 1. Configure UI Style and Theme

1. Open the **⚙️ Settings** tab.
2. Select **Modern** or **OLD** under **UI Style**.
3. Select **System**, **Light**, or **Dark** under **Theme**.
4. The settings are saved to `%LOCALAPPDATA%\Curio\settings.json` and automatically restored on the next launch.

### 2. Change Language

1. Open the **⚙️ Settings** tab.
2. Select **日本語** or **English** under **Language**.
3. The selected language is applied to the Curio UI.

### 3. Register Cursors

1. Open the **📂 Bulk Cursor Installation** tab.
2. Select a folder or drag and drop cursor files or folders into Curio.
3. If necessary, download cursors using Curio's built-in web browser.
4. Review the detected files.
5. Review and adjust the automatically assigned cursor roles.
6. Click **⚡ Bulk Install**.
7. The cursor scheme will be registered with Windows.

### 4. Edit Cursors

1. Load a CUR / ANI file into Curio.
2. Open the Paint Editor.
3. Use tools such as the pen, eraser, eyedropper, fill, and selection tools.
4. Adjust the hotspot or canvas size if necessary.
5. For ANI files, edit individual frames using the timeline.
6. Save the edited cursor as CUR or ANI.

---

## 📁 Settings and Storage

### Settings File

```text
%LOCALAPPDATA%\Curio\settings.json
```

### Cursor Scheme Storage

```text
%LOCALAPPDATA%\Curio\Schemes\<scheme name>\
```

### Windows Cursor Scheme Registry

```text
HKEY_CURRENT_USER\Control Panel\Cursors\Schemes
```

Curio manages cursor schemes on a per-user basis.

---

## 🌐 WebView2

Curio's built-in web browser uses Microsoft Edge WebView2.

If the WebView2 Runtime is not installed, Curio's built-in web browser may not be available.

The main cursor management and editing features of Curio can still be used without the web browser.

---

## 📄 License

Curio itself is released under the **Curio Source Available License Version 1.0**.

Curio includes source code from third-party projects.

For information about third-party components and their respective licenses, see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

---

## 🙏 Third-Party Software

Curio uses source code from third-party projects as part of its cursor editing functionality.

### Cursor-Palette

* Repository: https://github.com/DoomSalat/Cursor-Palette
* License: MIT License
* Copyright: Copyright (c) 2026 Capitan Salat

The applicable MIT License is included in:

```text
ThirdParty/CursorPalette/LICENSE
```

The license of third-party code is separate from Curio's own license.

See `THIRD-PARTY-NOTICES.md` for more information.

---

## 🔗 Links

* **GitHub:** https://github.com/Ford614/Curio
* **Releases:** https://github.com/Ford614/Curio/releases

---

## ⭐ Support Curio

If you find Curio useful, consider giving the project a ⭐ on GitHub. It helps support continued development.

If you would like to support Curio's development, you can donate here:

**[❤️ Support / Donate](https://ko-fi.com/ford614)**

Bug reports, feature requests, and improvement suggestions are also welcome through GitHub Issues.

---

## ⚠️ Notes

Curio modifies Windows cursor schemes and the Windows registry.

When deleting cursor schemes or changing their storage location, review the selected options before proceeding.

For files handled by Curio, we recommend using files obtained from trusted sources.

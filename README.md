# Aeris Browser - Installation & Run Guide

## Prerequisites

- **Operating System:** Windows 10 or Windows 11 (64-bit)
- **Runtime:** Microsoft Edge WebView2 Evergreen Runtime (pre-installed by default on modern Windows)
- **Compiler / Framework:** Built-in Microsoft .NET Framework 4.0/4.8 (pre-installed with Windows)

---

## 1. How to Build

If you need to compile or rebuild the browser from source, run:

```cmd
build.bat
```

This compiles the application and produces `AerisBrowser.exe` with its embedded app icon in under 2 seconds.

---

## 2. How to Run

### Method A: One-Click (File Explorer)
In File Explorer, simply double-click either:
- **`run.bat`** OR
- **`AerisBrowser.exe`**

---

### Method B: Command Prompt / PowerShell
Open your terminal in this directory and execute:

```cmd
run.bat
```

or run the executable directly:

```powershell
.\AerisBrowser.exe
```

---

## 3. Optional Run Flags

- **Open in Private / Incognito Mode:**
  ```cmd
  AerisBrowser.exe --incognito
  ```

- **Open Directly to a Specific URL:**
  ```cmd
  AerisBrowser.exe https://github.com
  ```
  
# webBrower

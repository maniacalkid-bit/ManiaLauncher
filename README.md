# Mania Launcher

**Open Source Minecraft Launcher** by **Mania AI**  
Created by **maniacalkid**

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

---

## Features

- **Offline accounts** — play without Microsoft account
- **Microsoft accounts** — full licensed support (Device Code → Xbox Live → XSTS → Minecraft Services)
- **Version management** — download and manage vanilla Minecraft versions
- **Profiles** — multiple profiles with different accounts & versions
- **Skins** — Microsoft skin URL support
- **Automatic Java detection**
- **Live logs** — real-time log viewer
- **Dark theme** — Black / Gray / White design
- **Custom C# installer** with shortcuts

---

## Requirements

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Java 17+ (Temurin recommended) for launching the game

---

## Building from source

```bash
git clone https://github.com/YOUR_USERNAME/ManiaLauncher.git
cd ManiaLauncher
dotnet restore
dotnet build
```

Run the launcher:
```bash
dotnet run --project src/ManiaLauncher
```

### Publish launcher (for installer)

```bash
dotnet publish src/ManiaLauncher/ManiaLauncher.csproj -c Release -r win-x64 --self-contained false -o publish/App
```

Then publish the installer:
```bash
dotnet publish src/ManiaLauncher.Installer/ManiaLauncher.Installer.csproj -c Release -r win-x64 --self-contained false -o publish/Installer
```

Copy the `publish/App` folder next to the installer executable (or inside it as `App/`) so the installer can copy the files.

---

## Microsoft Authentication Setup (REQUIRED)

To enable Microsoft login you **must** create your own Azure AD application:

1. Go to [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name: `ManiaLauncher` (any name)
3. Supported account types: **Personal Microsoft accounts only** (or both)
4. Redirect URI: leave empty (Device Code flow)
5. Click **Register**
6. Copy the **Application (client) ID**
7. Open file:
   `src/ManiaLauncher.Core/Auth/MicrosoftAuthService.cs`
8. Replace:
   ```csharp
   private const string ClientId = "YOUR_AZURE_CLIENT_ID_HERE";
   ```
   with your real Client ID.

After that Microsoft login will work fully (Device Code → Xbox → XSTS → Minecraft profile).

---

## Project Structure

```
ManiaLauncher/
├── src/
│   ├── ManiaLauncher/              # Main WPF application
│   ├── ManiaLauncher.Core/         # Business logic
│   │   ├── Auth/                   # Offline + full Microsoft auth
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── Logging/
│   │   └── Utils/
│   └── ManiaLauncher.Installer/    # Custom C# installer
├── assets/
├── .github/workflows/              # CI
├── LICENSE
└── README.md
```

---

## What is still simplified / next steps

- Version download currently downloads only the client.jar  
  (full libraries + assets + natives is the next big task)
- Launch arguments are basic (full classpath needed for real play)
- Java auto-download not yet implemented (detection works)

This is a solid foundation with **complete Microsoft authentication**.

---

## License

MIT License — see [LICENSE](LICENSE)

---

Made with ❤️ by **maniacalkid** · **Mania AI**

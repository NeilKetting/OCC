---
description: Create a new release installer for the Desktop Client
---

This workflow compiles the latest code in Release mode and packages it into a standard Velopack installer.

**IMPORTANT: Run these commands from the OCC.Client folder** (e.g. `C:\Users\...\OrangeCircleConstruction\OCC.Client`)

1. **Create Release**
   This command compiles the code and packages it into an installer in one step.
   *Note: Update the `-v` version number as needed.*

```powershell
dotnet publish OCC.Client.Desktop/OCC.Client.Desktop.csproj -c Release -o publish --self-contained true -r win-x64; vpk pack -u "OCC.Beta" -p publish -e "OCC.Client.Desktop.exe" --packTitle "OCC Beta" --packAuthors "Neil Ketting" -v 1.6.13 -o Releases
```


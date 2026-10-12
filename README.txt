Egy Group Scanner - ايجي جروب اسكانر

Two outputs:
  1) Portable : EgyGroupScanner.exe  (single file, no install)
  2) Installer: EgyGroupScanner-Setup.exe (Start menu + desktop shortcut + uninstall)

Build online with GitHub Actions
  - Upload the CONTENTS of this folder to the root of a GitHub repository
    (including the hidden .github folder).
  - Open the Actions tab > "Build Egy Group Scanner" > Run workflow
    (it also runs on every push).
  - When it finishes, download both files from "Artifacts".
  - To publish a release: create a tag like v1.0.0 and the files are attached to the release.

Build locally (Windows, .NET SDK or Visual Studio):
    dotnet build -c Release
Output: bin\Release\net48\EgyGroupScanner.exe  (x86, Windows 7/10/11, 32 and 64 bit)

Requirements on the PC:
  - .NET Framework 4.8 (built into Windows 10/11; Windows 7 SP1 needs it installed)
  - the official Ricoh scanner driver (TWAIN or WIA) for the machine

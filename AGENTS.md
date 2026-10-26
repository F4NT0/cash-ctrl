# Project notes

## Rounding images (README)
- When asked to round an image's corners, use `RoundImage.ps1` in the repo root:
  `.\RoundImage.ps1 images\<name>.png` -> creates `images\<name>-rounded.png` (original kept).
  Optional: `-Output <path>`, `-Radius <0..0.5>` (default 0.04).
- In the README reference the `-rounded.png` file, centered, e.g.
  `<p align="center"><img src="images/<name>-rounded.png" alt="..." width="600" /></p>`
  (GitHub strips `style`/`border-radius`, so rounding must be baked into the PNG).

## Build
- Main app: `dotnet publish CashCtrl\CashCtrl.csproj -c Release -o dist`
- Installer: `dotnet publish CashCtrl.Installer -c Release -o dist`
- Close any running `cash-ctrl.exe` before building (file lock).

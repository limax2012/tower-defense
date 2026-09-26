# Flutter mobile application

Maximal Bastion uses Flutter for the phone interface and bundles the existing C#/WebAssembly game engine and WebGL renderer. Gameplay rules and authored content remain in `../MaximalBastion`. Android and iOS host the bundled runtime through a local WebView; the web target previews the same touch UI in an iframe.

See [mobile architecture and build instructions](../../docs/mobile-architecture.md).

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-mobile.ps1 -Debug
```

For Flutter UI iteration after preparing the engine assets:

```powershell
cd src/MaximalBastion.Mobile
flutter run
```

The runtime asset directory is generated and excluded from version control. Run `scripts/prepare-mobile.ps1` after changing shared C# or JSON content, then rebuild Flutter to include it.

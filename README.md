# NCockpit

A minimalist navigation hub for .NET developers. It centralizes project links, IDE shortcuts, and external URLs into a single structured dashboard. Your daily starting point.

## Stack

A .NET 11 .NET MAUI desktop application for Windows (`net11.0-windows10.0.19041.0`), with SQLite and Dapper for persistence, CommunityToolkit.Maui for popups, and NTokenizers for the Markdown file preview.

## Requirements

- .NET 11 SDK and the `maui-windows` workload (`dotnet workload list` to check)
- Windows 10 version 1809 (build 17763) or later, x64; .NET 11 requires a CPU supporting the x86-64-v2 instruction set

## Build

```bash
dotnet build                                        # Debug
dotnet build -c Release                             # Release
dotnet build --framework net11.0-windows10.0.19041.0   # Windows TFM explicitly
```

Run the app from Visual Studio or with `dotnet run`. If the build fails with `MSB3027`/`MSB3021` on `NCockpit.exe`, the app is still running; close it and build again.

## References

- Product specification: [initial.md](initial.md)
- Design mockup: [docs/prototype.png](docs/prototype.png)
- Contributor License Agreement: [CONTRIBUTING.md](CONTRIBUTING.md)

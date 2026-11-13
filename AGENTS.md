# Agent Guidelines for NCockpit

## Project Overview
NCockpit (Projects Cockpit) is a **.NET 10 MAUI desktop application** — a personal cockpit for quickly finding projects and navigating to their files, folders, solutions, and online services. It is explicitly **not** a project-management tool: no tasks, deadlines, kanban, or status workflows.

The app is primarily Windows. **Do not** introduce WPF, WinUI, or any other UI framework; keep all UI in MAUI XAML. It is built as a single MAUI project using standard MVVM Toolkit patterns, with SQLite + Dapper for persistence.

> The full product specification lives in [`initial.md`](initial.md). Read it before making significant design or feature decisions; it defines the screens (Projects overview, Project details, Folders), the scan vs. refresh distinction, and the UX principles.

## Build, Lint, and Test Commands

### Build Commands
```bash
dotnet build                          # Build the solution
dotnet build -c Release               # Build in Release mode
dotnet build --framework net10.0-windows10.0.19041.0   # Target the Windows TFM explicitly
```

> **Note:** If `dotnet build` fails with `MSB3027`/`MSB3021` "Could not copy ... NCockpit.exe ... being used by another process", the app is currently running. Close NCockpit (or kill the `NCockpit` process) and rebuild. This is not a code error.

### Test Commands
- No unit tests are configured for this project.
- Manual testing and QA validation are the primary testing approach.
- The agent can build to verify compilation but should **not** run the app — the developer handles manual validation.

### Linting
- Rely on .NET compiler warnings; the build currently emits `MVVMTK0045` (see [ViewModel Implementation](#view-model-implementation)) and these are intentionally accepted for this project.
- Ensure code compiles without errors; keep warning output from growing.

## Code Style and Conventions

### .NET MAUI
1. Strictly use the .NET MAUI XAML dialect; never use or reference WPF, UWP, WinUI, Xamarin.Forms, Uno, or Avalonia syntax or patterns.
2. Use `xmlns="http://schemas.microsoft.com/dotnet/2021/maui"` and `xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"` as the root namespaces. Additional namespaces are allowed only when valid in a .NET MAUI project.
3. This project uses `<MauiXamlInflator>SourceGen</MauiXamlInflator>`. Use `x:DataType` on every `DataTemplate` that contains compiled bindings so bindings are type-checked and AOT-friendly.
4. Never introduce runtime-only (string-based) bindings as a fallback.
5. Never use the obsolete `FillAndExpand` attached property — MAUI layout containers fill and expand available space by default.
6. Before outputting XAML, ensure it is consistent with .NET MAUI capabilities and the compiled-binding requirements above.

### .NET and Language Version
- Target .NET 10 exclusively (`net10.0`, with `net10.0-windows10.0.19041.0` on Windows).
- Use modern C# features (primary constructors, records, collection expressions, target-typed `new`).
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`).

### Indentation
- C# files and XAML are indented with **4 spaces** (see `.editorconfig`). XML build files use 2 spaces.
- Match the indentation of the file you are editing.

### Imports and Namespace Organization
- Use explicit `using` statements at the top of the file (before the namespace), in this order:
  1. `System` namespaces
  2. External packages (e.g., `CommunityToolkit.*`, `Microsoft.*`)
  3. Project namespaces
- Alphabetical within each group.

### Naming Conventions
- **Classes/Interfaces**: PascalCase (e.g., `ProjectService`, `IProjectRepository`)
- **Methods/Properties**: PascalCase (e.g., `LoadProjects`, `GetProjectsAsync`)
- **Local variables/parameters**: camelCase (e.g., `folderId`, `project`)
- **Private fields**: `_camelCase` (e.g., `_projectService`, `_scanCts`)
- **Private static fields**: `s_camelCase` (e.g., `s_solutionExtensions`)
- **Constants**: PascalCase
- **Async methods**: suffix `Async` (e.g., `ScanAsync`, `ToggleFavoriteAsync`)
- **ViewModels**: `<Page>ViewModel` (e.g., `OverviewPageViewModel`); **Services**: `<Area>Service` with an `I<Area>Service` interface; **Repositories**: `<Entity>Repository` with an `I<Entity>Repository` interface.

### Code Organization
- Keep methods focused and concise (< 50 lines preferred).
- Place property declarations before method declarations.
- Use file-scoped namespaces (`namespace NCockpit.Services;`).

### Comments and Documentation
- Add `///` XML documentation for public types and public API members (models, service/repository interfaces, view-models).
- Comment non-obvious behavior only; avoid restating the code.

### Error Handling
- ViewModels expose an `ErrorMessage` property and guard long-running operations in `try/catch`, keeping `IsLoading` resets in `finally`.
- For confirmation dialogs, go through `IPageService.ConfirmAsync(...)` — do **not** reach into `Application.Current.Windows[0].Page` from a view-model.
- The scanner is resilient by design: a missing optional file or inaccessible folder must never fail the whole scan (see [Scanning](#scanning)).

### Async/Await
- Use asynchronous APIs for all I/O (filesystem, database).
- Async methods return `Task`/`Task<T>`; avoid `async void` except for framework event handlers (e.g., `OnNavigatedTo`).
- Prefer `async` `[RelayCommand]` methods so CommunityToolkit.MVVM generates `IAsyncRelayCommand`.
- Never block with `.Result`, `.Wait()`, or `GetAwaiter().GetResult()`.
- Do not use `ConfigureAwait(false)` in application code.
- For long-running work (scans), support `IProgress<T>` for UI progress and `CancellationToken` for cancellation, and `Task.Yield()`/`ConfigureAwait`-free async loops so the UI stays responsive.

## Architecture and Patterns

### MVVM Layout
- **Views (Pages)**: XAML in `Pages/` with minimal code-behind.
- **ViewModels**: `ViewModels/` (page view-models) and `ViewModels/Popups/` (popup view-models), built on CommunityToolkit.MVVM.
- **Models**: POCOs in `Models/` (`Project`, `ProjectFolder`).
- **Services**: Business logic in `Services/` behind interfaces (`IProjectService`, `IProjectFolderService`, `IScanService`, `IPageService`).
- **Repositories**: Data access in `Repositories/` behind interfaces (`IProjectRepository`, `IProjectFolderRepository`); `Database` handles SQLite init/migration.
- Keep Windows-specific integration out of view-models; express intent (`OpenFolder`, `OpenSolution`, `OpenFile`, `OpenExternalLink`) and delegate to a platform abstraction.

### Dependency Injection and View-Model Resolution
- All services, repositories, and page view-models are registered as **singletons** in `MauiProgram.cs`; popup view-models are **transient** and registered via `AddTransientPopup<TView, TViewModel>()`.
- Pages resolve their view-model from DI (they do **not** receive it through the constructor) and set it as `BindingContext`:
  ```csharp
  _viewModel = IPlatformApplication.Current!.Services.GetService<OverviewPageViewModel>()
      ?? throw new InvalidOperationException("OverviewPageViewModel not found in services");
  BindingContext = _viewModel;
  ```
- `OverviewPage` reloads data in `OnNavigatedTo` because the view-model is a shared singleton.
- Register any new page view-model in `MauiProgram.cs` before using it.

### View-Model Implementation
- Use `public partial class <Name>ViewModel : ObservableObject`.
- Use `[ObservableProperty]` on **private camelCase fields** (the pattern used throughout this project):
  ```csharp
  [ObservableProperty]
  private List<ProjectListItem> projects = [];
  ```
  The source generator creates the `Projects` property and a private backing field.
- **MVVMTK0045**: applying `[ObservableProperty]` to a field generates code that is not AOT-compatible in WinRT scenarios. This is accepted for NCockpit (it runs unpackaged on Windows), so do **not** treat these warnings as build breakers — but keep new code consistent with the existing field-based style.
- Use `[RelayCommand]` for commands; prefer `async Task` command methods so `IAsyncRelayCommand` is generated.

### Data Flow
- View-models talk to **services**, services talk to **repositories**, repositories talk to **Dapper/SQLite**. View-models never touch the database directly.
- Use `ProjectListItem` (a lightweight display wrapper) for list templates so XAML stays simple; it reuses the underlying `Project` model for favorite/usage state.

### Navigation (Shell)
- Navigation is **Shell-based**. `AppShell` declares the flyout items (`Overview`, `Folders`, `Settings`, `About`); `ProjectDetailsPage` is registered as a hidden route so it does not appear in the flyout:
  ```csharp
  Routing.RegisterRoute("projectdetails", typeof(ProjectDetailsPage));   // in AppShell.xaml.cs
  ```
- Navigate with `await Shell.Current.GoToAsync("projectdetails")` and back with `Shell.Current.GoToAsync("..")`.
- Do not mix Shell with `NavigationPage`/`TabbedPage`/`FlyoutPage`.

### Popups (CommunityToolkit)
- Popups live in `Popups/` (e.g., `AddFolderPopup`, `ScanPopup`) with view-models in `ViewModels/Popups/`, registered via `AddTransientPopup<TView, TViewModel>()`.
- Show and retrieve a result with:
  ```csharp
  var result = await _popupService.ShowPopupAsync<AddFolderPopup, AddFolderResult>(Shell.Current);
  // result.Result / result.WasDismissedByTappingOutsideOfPopup
  ```
- The `ScanPopup` is fire-and-forget: show it, await the scan, then close it with `Shell.Current.ClosePopupAsync()`.

### XAML Guidelines
- Use `Grid` for complex multi-row/column layouts; use `VerticalStackLayout`/`HorizontalStackLayout` rather than `StackLayout`.
- Use `Border` (with `StrokeShape="RoundRectangle 8"`) for cards — preferred over `Frame`.
- Use `CollectionView` for lists. **Never** place a `ScrollView` or `CollectionView` directly inside a `StackLayout`.
- Converters are registered once in `Converters/Converters.xaml` (merged in `App.xaml`) and referenced via `{StaticResource ...}`.
- Reusable styles live in `Resources/Styles/Styles.xaml`; colors in `Resources/Styles/Colors.xaml`.

### Custom Controls
- Custom controls live in `Controls/` and follow the MAUI partial-class XAML pattern.
- Use `ContentView` with `ControlTemplate` for composite controls with fixed chrome + user content (e.g., `SectionCard`).
- Use `x:Name="This"` on the root element and `Source={x:Reference This}` for self-referencing bindings.
- Use `TemplatedParent` for bindings inside a `ControlTemplate`.
- Existing controls: `SummaryCard` (icon + count + label), `SectionCard` (title + content).
- When a control hosts user-provided `Content`, mark the class with `[ContentProperty(nameof(Content))]` and leave the `ContentPresenter` unbound.

## Design Language
Follow the visual direction from the mockup (see `docs/prototype.png` and `initial.md`) — a polished, compact, information-dense developer tool, not a generic admin dashboard:
- Dark navy left navigation/flyout (`#1a2744`) with white text and FontAwesome6 glyphs.
- Light main content area (`#f5f5f5`) with white cards, subtle borders (`#e0e0e0`), and rounded corners.
- Section headers in navy; primary accent in teal (`#0d7377`); errors in red (`#d32f2f`).
- Compact, scannable sections/cards rather than one large form; clear clickable actions.
- Fonts: OpenSans (Regular/Semibold) for text, `FontAwesomeSolid` for icons (both registered in `MauiProgram.cs`).

## Scanning
- A **project** is a folder containing a `.sln` or `.slnx` file; discovery is defined in `ScanService`.
- Scanning is **safe and resilient**: `UnauthorizedAccessException`/`IOException` on any directory are skipped, and common noise folders are pruned (`.git`, `bin`, `obj`, `node_modules`, `.vs`, etc.). A missing file or inaccessible folder must never fail the whole scan.
- Keep **refresh** (lightweight: git status, branch, timestamps) and **scan** (rediscover structure/metadata) separate. Do not trigger an expensive scan when a quick refresh suffices.
- Report progress via `IProgress<ScanProgress>` and respect `CancellationToken`; keep the UI responsive with `Task.Yield()`.

## Persistence
- SQLite file lives at `FileSystem.AppDataDirectory/ncockpit_data.db`; Dapper is used for data access.
- Schema versioning is manual via `PRAGMA user_version` in `Database.cs` (`CurrentUserVersion`). To change the schema, bump `CurrentUserVersion` and add a new `if (userVersion < N)` migration step — never rewrite existing tables.
- Persist configured folders, projects, locations, related projects, external service links, favorites, recent usage, and scan information. The filesystem remains the source of truth for discovered project information where appropriate — do not duplicate data that can be reliably rediscovered.

## Project Structure
```
NCockpit/
├── initial.md            # Full product specification (read before big changes)
├── docs/prototype.png    # UI design reference
├── skills/               # Local CodeAlta skills
└── src/NCockpit/         # Single MAUI project
    ├── MauiProgram.cs    # DI registration, fonts, community toolkit
    ├── App.xaml(.cs)     # Merged resource dictionaries
    ├── AppShell.xaml(.cs) # Shell flyout + hidden "projectdetails" route
    ├── Controls/         # SummaryCard, SectionCard
    ├── Models/           # Project, ProjectFolder
    ├── Pages/            # OverviewPage, ProjectDetailsPage, FoldersPage, SettingsPage, AboutPage
    ├── Popups/           # AddFolderPopup, ScanPopup
    ├── ViewModels/       # OverviewPageViewModel, FoldersPageViewModel, ProjectListItem
    │   └── Popups/       # AddFolderPopupViewModel, ScanPopupViewModel
    ├── Services/         # ProjectService, ProjectFolderService, ScanService, PageService
    ├── Repositories/     # Database, ProjectRepository, ProjectFolderRepository
    ├── Converters/       # Converters.xaml + converter implementations
    ├── Resources/
    │   ├── Styles/       # Colors.xaml, Styles.xaml
    │   ├── Fonts/        # OpenSans, FontAwesome6 Free Solid
    │   ├── Images/       # Image assets
    │   ├── AppIcon/      # App icon (SVG source)
    │   └── Splash/       # Splash screen (SVG source)
    └── Platforms/        # Per-platform native code (Windows, Android, iOS, MacCatalyst)
```

## Git and Version Control
- Commit messages should be clear, descriptive, and in English; use conventional commits when applicable.
- **NEVER** commit or push unless the user explicitly asks.

## Key References
- Product spec: [`initial.md`](initial.md)
- Design mockup: `docs/prototype.png`
- Build/test guidance for the .NET MAUI skill is available locally under `skills/` (custom controls, popups/snackbars, SkiaSharp).

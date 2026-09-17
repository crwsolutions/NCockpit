# Plan: Overview row opens .sln, records last opened, and sorts by opened/solution time

- Status: Approved
- Plan file: `.alta/plans/2026-09-17-overview-row-open-solution-and-details-icon.md`
- Created: 2026-09-17
- Task: Add overview-list navigation and metadata behavior for the "Nu" phase: clicking the main area of a project row opens its `.sln`/`.slnx` file with the associated Windows application and records the project's "last opened" timestamp; a new small FontAwesome `f105` angle-right icon at the row's right end navigates to `ProjectDetailsPage` without recording a "last opened" timestamp; the overview list is sorted first by last opened and second by solution file timestamp.
- Git: not ignored (`.alta/plans/` is not ignored in this repo); commit this plan with the related implementation work.

## Objective
- Change the primary project row click on `OverviewPage` from opening `ProjectDetailsPage` to opening the project's solution file using the Windows shell association.
- Record a "last opened" timestamp when the user opens the solution via row click, because this drives the displayed "last opened" information and the overview ordering.
- Preserve current detail navigation by adding a small, subtle FontAwesome6 angle-right glyph (`f105`) at the right end of each project row; clicking only that glyph opens `ProjectDetailsPage` and does not record a "last opened" timestamp.
- Change overview project ordering to:
  1. most recently opened projects first,
  2. projects that were never opened after projects that were opened,
  3. within opened/never-opened groups, order by solution file timestamp most recent first.
- Persist the solution file timestamp during scanning so it can be used for sorting without requiring the file to exist in the UI at load time.
- Keep the change scoped to the "Nu" phase. Do not implement the "Later" phase items such as VS Code, folder open, GitHub, or bin/debug/release folders.

## Context and evidence
- `OverviewPage.xaml` currently defines a `CollectionView` with `SelectionMode="Single"` and code-behind selection handling at `src/NCockpit/Pages/OverviewPage.xaml:104`.
- `OverviewPage.xaml.cs` handles `SelectionChanged`, clears `SelectedItem`, and invokes `OpenProjectCommand` at `src/NCockpit/Pages/OverviewPage.xaml.cs:26`.
- `OverviewPageViewModel.OpenProject(ProjectListItem?)` currently records usage via `_projectService.TouchAsync(item.Project.Id)` and navigates to `Shell.Current.GoToAsync("projectdetails")` at `src/NCockpit/ViewModels/OverviewPageViewModel.cs:111`.
- `ProjectListItem` exposes the underlying `Project` model, and `Project.SolutionPath` already stores the full `.sln` or `.slnx` path at `src/NCockpit/ViewModels/ProjectListItem.cs:33` and `src/NCockpit/Models/Project.cs:19`.
- `Project.LastUsedUnix` already exists and is documented as "last opened/viewed" at `src/NCockpit/Models/Project.cs:52`; the repository writes it through `TouchAsync` and uses it for current ordering at `src/NCockpit/Repositories/ProjectRepository.cs:109`.
- The overview already displays `LastUsedDisplay`, built from `project.LastUsedUnix ?? project.LastSeenUnix`, at `src/NCockpit/ViewModels/ProjectListItem.cs:21`.
- Existing Windows shell launch pattern is present in `FoldersPageViewModel.OpenFolder(string path)` using `ProcessStartInfo { UseShellExecute = true }` inside `#if WINDOWS` at `src/NCockpit/ViewModels/FoldersPageViewModel.cs:265`.
- AGENTS.md states that Windows-specific integration should be expressed as intent from view-models and delegated to a platform/service abstraction (`OpenFolder`, `OpenSolution`, `OpenFile`, `OpenExternalLink`), while the product spec says a solution file should open using the configured/default associated application (`initial.md`).
- `FontAwesomeSolid` is registered in `MauiProgram.cs` at `src/NCockpit/MauiProgram.cs:27` and is already used by existing FontAwesome `Button` patterns, such as the overview favorite indicator at `src/NCockpit/Pages/OverviewPage.xaml:116`.
- `Database.CurrentUserVersion` is currently `3` at `src/NCockpit/Repositories/Database.cs:14`; the project table currently lacks a solution file timestamp column.
- `ScanService` currently upserts projects with `LastSeenUnix` and `ScanUnix` but does not capture a solution file timestamp at `src/NCockpit/Services/ScanService.cs:70`.
- `.alta/plans/` does not currently exist and is not git-ignored in this working tree.

## Assumptions and decisions
- Decision: the recommended defaults are approved.
- Decision: the "last opened" UI state when `LastUsedUnix` is null is `Never opened`.
- Decision: use `SolutionLastWriteUnix` as the stored solution file timestamp column name.
- Decision: the "last opened" label displays only the last opened state; it does not fall back to `LastSeenUnix` when null.
- Assumption: "last opened" means the solution file was opened from the overview row via the Windows shell association.
- Assumption: the existing nullable `Projects.LastUsedUnix` column is the authoritative "last opened" field for this feature.
- Assumption: the angle-right details icon does not touch `LastUsedUnix`; it navigates to details without changing recent-usage state.
- Assumption: "timestamp of the solution file" means the Windows file last-write time of the `.sln` or `.slnx` file at scan time, stored as a Unix timestamp.
- Assumption: projects whose stored solution file timestamp is null should sort after projects that have one within the same last-opened grouping.
- Assumption: if the solution file is missing from disk when the user clicks the row, the app should surface a non-fatal `ErrorMessage` and should not record "last opened" for a file it could not open.

## Design notes
- Introduce a small service abstraction for Windows file opening rather than adding process-start logic to `OverviewPageViewModel`. A suitable surface is:
  - `IWindowsLauncher` with `bool TryOpenFile(string path)` or `Task<bool> OpenFileAsync(string path)`.
  - `WindowsLauncher` implementation guarded with `#if WINDOWS`, validating `File.Exists(path)` and launching through `Process.Start` with `UseShellExecute = true`.
  - On non-Windows targets, return failure or no-op with a user-facing message. NCockpit is primarily Windows, but keeping the interface makes the view-model portable.
- Register the service as a singleton in `MauiProgram.cs` alongside the existing services.
- Add an `OpenSolutionCommand` to `OverviewPageViewModel` that:
  - takes `ProjectListItem?`,
  - resolves `item.Project.SolutionPath`,
  - calls the launcher,
  - only records "last opened" after the launcher reports success,
  - sets `ErrorMessage` if the file is missing or launch fails.
- The details command should:
  - be named `OpenProjectDetailsCommand(ProjectListItem?)` for clarity,
  - navigate to `projectdetails`,
  - not call `TouchAsync` or any other "last opened" update,
  - keep existing error handling for navigation failures.
- Update `Project` to include a nullable solution file timestamp:
  - add `public long? SolutionLastWriteUnix { get; set; }` to `src/NCockpit/Models/Project.cs`,
  - document it as the solution file's last-write time captured during scan and used for sorting.
- Persist the new timestamp:
  - bump `Database.CurrentUserVersion` to `4`,
  - add migration step `if (userVersion < 4)` that adds nullable column `SolutionLastWriteUnix` to `Projects`,
  - include `SolutionLastWriteUnix` in `CreateProjectsTable` for new databases,
  - update `ProjectRepository.GetAllAsync` SQL to select the new column,
  - update `ProjectRepository.UpsertAsync` to write the new column on both insert and update.
- Update scan metadata capture:
  - in `ScanService`, when a solution file is discovered, read its last-write time using `File.GetLastWriteTimeUtc(solution)`,
  - store it as Unix seconds in `Project.SolutionLastWriteUnix`,
  - wrap the read in the same resilience pattern used elsewhere in scanning so a missing/inaccessible file does not fail the whole scan; a null value is acceptable for sort tie-breaking.
- Update overview ordering in `ProjectRepository.GetAllAsync`:
  - `ORDER BY LastUsedUnix IS NULL, COALESCE(LastUsedUnix, 0) DESC, SolutionLastWriteUnix IS NULL, COALESCE(SolutionLastWriteUnix, 0) DESC, Name`,
  - this places projects with a `LastUsedUnix` first, sorted descending, then never-opened projects sorted by solution file timestamp descending, with stable name tie-breaking.
- Update `ProjectListItem` display:
  - add a `LastOpenedDisplay` property instead of relying solely on the current mixed `LastUsedDisplay` fallback, or rename `LastUsedDisplay` if that is clearer and update XAML accordingly,
  - if `LastUsedUnix` is null, show a concise English UI string such as `Never opened`.
  - keep the existing status text for "last seen" behavior unless the design decision is to replace it; if kept, the row may show both last opened and last seen, but the visible "last opened" area must reflect the new last-opened semantics.
- Update `OverviewPage.xaml` row template:
  - change `CollectionView` selection handling from `SelectionMode="Single"` plus `SelectionChanged` to `SelectionMode="None"` plus `TapGestureRecognizer` on the row `Border`. This avoids a row-wide selection visual and gives a cleaner "click row" gesture.
  - keep the favorite star `Button` with `InputTransparent="True"` as-is.
  - extend the row `Grid` to `ColumnDefinitions="Auto,*,Auto,Auto"` so the last-opened label remains before the new details chevron.
  - add a small `Button` in the last column:
    - `Text="&#xf105;"`,
    - `FontFamily="FontAwesomeSolid"`,
    - `FontSize="12"` or `14`,
    - `TextColor="#999999"` by default, optionally teal on `Pressed`/highlighted if a simple style is warranted,
    - `BackgroundColor="Transparent"`,
    - `Padding="6"` or slightly larger touch target than the glyph,
    - `VerticalOptions="Center"`,
    - `Command="{Binding DataContext.OpenProjectDetailsCommand, Source={x:Reference ProjectListView}}"` with `CommandParameter="{Binding .}"`, where the `CollectionView` gets `x:Name="ProjectListView"`.
  - bind the row tap gesture to `OpenSolutionCommand` with `CommandParameter="{Binding .}"`.
  - if `x:Reference` to a `CollectionView` is awkward due to templating or XAML style, an alternative is to bind the row root `BindingContext` to a row-level wrapper or use a page-level `BindingContext` reference already available on the page, but the direct `x:Reference` pattern is the least invasive.
- The page code-behind `OnProjectSelectionChanged` can be removed or reduced; the row tap handler should call `OpenSolutionCommand` and the chevron button should call `OpenProjectDetailsCommand` through binding.
- Do not modify `ProjectDetailsPage` behavior in this phase beyond the existing route already registered in `AppShell`.
- Do not add VS Code, folder open, GitHub, bin folder, or other "Later" phase actions.

## Risks and challenges
- Gesture conflict: a `Button` inside a tappable `Border` may swallow taps. This is desirable for the chevron; the chevron should not also trigger the row `TapGestureRecognizer` for opening the solution.
- `CollectionView` compiled binding: the chevron command must be reachable from the `DataTemplate` while using the existing `x:DataType="viewmodels:ProjectListItem"`. Using `x:Reference` to the named `CollectionView` is the likely approach, but the builder should verify the generated XAML compiles.
- Missing solution file: if `SolutionPath` is stale, opening it should fail gracefully, set `ErrorMessage`, and not update `LastUsedUnix`.
- Windows-only behavior: the app is Windows-first, but the shared interface should avoid compile-time Windows-only code in the view-model.
- Existing `FoldersPageViewModel.OpenFolder` contains Windows-specific process code. This plan should not refactor that existing behavior unless needed, to avoid unrelated churn.
- Scan timestamp edge cases: a missing or inaccessible solution file should not fail the scan; storing `null` for `SolutionLastWriteUnix` is acceptable but should sort after known timestamps in the relevant grouping.
- Ordering regression: `LastSeenUnix` is no longer the primary ordering input after this change; any UI copy or comments that say "ordered by recent usage" should be updated to match the new opened/solution-time semantics.

## Implementation checklist
- [x] Add `IWindowsLauncher` interface in `src/NCockpit/Services/` with a method to open a local file via Windows association, for example `bool TryOpenFile(string path)` or `Task<bool> OpenFileAsync(string path)`.
- [x] Add `WindowsLauncher` implementation in `src/NCockpit/Services/WindowsLauncher.cs`:
  - guard with `#if WINDOWS` where process launching is required,
  - verify the file exists,
  - use `Process.Start` with `ProcessStartInfo { FileName = path, UseShellExecute = true }`,
  - catch launch exceptions and report failure to the caller.
- [x] Register `IWindowsLauncher` as a singleton in `src/NCockpit/MauiProgram.cs`.
- [x] Add `public long? SolutionLastWriteUnix { get; set; }` to `Project` in `src/NCockpit/Models/Project.cs` with XML documentation.
- [x] Update `src/NCockpit/Repositories/Database.cs`:
  - set `CurrentUserVersion` to `4`,
  - add `SolutionLastWriteUnix` to the new-database project table creation,
  - add migration step from version `3` to `4` adding the nullable column to existing `Projects` tables.
- [x] Update `src/NCockpit/Repositories/ProjectRepository.cs`:
  - select `SolutionLastWriteUnix` in `GetAllAsync`,
  - insert and update `SolutionLastWriteUnix` in `UpsertAsync`,
  - change `GetAllAsync` ordering to `LastUsedUnix IS NULL, COALESCE(LastUsedUnix, 0) DESC, SolutionLastWriteUnix IS NULL, COALESCE(SolutionLastWriteUnix, 0) DESC, Name`.
- [x] Update `src/NCockpit/Services/ScanService.cs`:
  - capture `File.GetLastWriteTimeUtc(solution)` when a solution file is found,
  - store the Unix timestamp in the upserted `Project.SolutionLastWriteUnix`,
  - keep the scan resilient by tolerating missing or inaccessible file metadata and storing null if it cannot be read.
- [x] Inject `IWindowsLauncher` into `OverviewPageViewModel`.
- [x] Add `OpenSolutionCommand(ProjectListItem?)` to `OverviewPageViewModel`:
  - guard null item,
  - read `item.Project.SolutionPath`,
  - call the launcher,
  - on success call `_projectService.TouchAsync(item.Project.Id)` to record last opened,
  - set `ErrorMessage` on missing file or launch failure without recording last opened.
- [x] Preserve or rename the existing details command as `OpenProjectDetailsCommand(ProjectListItem?)`:
  - navigate to `projectdetails`,
  - do not call `TouchAsync` or update last opened,
  - keep existing error handling.
- [x] Update `src/NCockpit/ViewModels/ProjectListItem.cs`:
  - expose `LastOpenedDisplay` from `Project.LastUsedUnix`,
  - show a concise English "never opened" value when `LastUsedUnix` is null,
  - update or preserve `StatusText` so the row does not imply the old "recent usage" ordering.
- [x] Update `src/NCockpit/Pages/OverviewPage.xaml`:
  - add `x:Name="ProjectListView"` to the projects `CollectionView`,
  - set `SelectionMode="None"`,
  - add `TapGestureRecognizer` to the row `Border` bound to `OpenSolutionCommand` with `CommandParameter="{Binding .}"`,
  - change the row `Grid` columns to include a final `Auto` column for the chevron,
  - move the existing last-used/last-opened label into the new third column and bind it to the updated `LastOpenedDisplay`,
  - add a small transparent FontAwesome `Button` with glyph `&#xf105;` in the final column bound to `OpenProjectDetailsCommand` using `x:Reference` to `ProjectListView` and `CommandParameter="{Binding .}"`.
- [x] Update `src/NCockpit/Pages/OverviewPage.xaml.cs`:
  - remove `OnProjectSelectionChanged` and its handler,
  - add a `Border` tap handler if the `TapGestureRecognizer` is attached in code-behind, or keep it XAML-bound if the page's `BindingContext` makes direct command binding reliable.
- [x] Run a full build and verify compiled XAML bindings for the new chevron command and row tap command.
- [x] Self-review the XAML for gesture behavior, column layout, last-opened label semantics, and subtle chevron styling.
- [ ] Commit the updated plan file together with implementation changes only if Colin explicitly asks to commit.

## Verification checklist
- [x] `dotnet build` succeeds without new warnings beyond the accepted `MVVMTK0045` noise.
- [ ] Existing database migrates from version `3` to `4` without data loss; `Projects` contains a nullable `SolutionLastWriteUnix` column.
- [ ] A scan populates `SolutionLastWriteUnix` for discovered `.sln`/`.slnx` files.
- [ ] A scan does not fail when a discovered solution file cannot be accessed for metadata.
- [ ] Manual check on Windows: click the main area of a project row and confirm the associated application opens the `.sln` or `.slnx` file.
- [ ] Manual check: after opening a solution via row click, the project's last-opened timestamp updates and the overview ordering moves it according to the new sorting rules.
- [ ] Manual check: click the new angle-right icon and confirm the app navigates to `ProjectDetailsPage` without changing last-opened ordering.
- [ ] Manual check: projects never opened sort below projects that have been opened, and within each group sort by solution file timestamp most recent first.
- [ ] Manual check: missing/stale `SolutionPath` shows a user-facing `ErrorMessage` and does not update last opened.
- [ ] Manual check: favorite star indicator remains non-interactive and does not block row clicks except where visually transparent.
- [ ] Visual check: the chevron is small and subtle, positioned at the right end of the row without crowding the last-opened label, and matches the existing design language.
- [ ] Visual check: the last-opened label clearly displays opened timestamp or a "never opened" state.

## Handoff notes
- The key behavioral changes are: row click opens the solution and records last opened; chevron opens details without recording last opened; sorting uses last opened first, then solution file timestamp.
- "Last opened" should use the existing `LastUsedUnix` field unless the team decides on a new column; this plan treats that field as the authoritative source.
- The solution file timestamp should be captured during scan and persisted, not read at UI render time.
- Do not expand scope into "Later" phase navigation targets.
- If the `x:Reference` command binding from the `CollectionView` item template is problematic, fall back to the least invasive approach that keeps compiled bindings and avoids code-behind command plumbing.
- The existing `FoldersPageViewModel.OpenFolder` Windows-specific implementation is out of scope; do not refactor it while adding `IWindowsLauncher` unless necessary for shared behavior.

# Plan: Centralize the NCockpit color palette and introduce Label/Button role styles

- Status: Done
- Plan file: `.alta/plans/2026-11-19-centralize-styles-and-palette.md`
- Created: 2026-11-19
- Task: The NCockpit UI was never built on the `Colors.xaml` palette: every `Label` and `Button` across pages, popups, and custom controls carries its own inline `TextColor`/`FontSize`/`FontAttributes`/`FontFamily`/`BackgroundColor`, the template `Gray*`/`Secondary*`/`Tertiary`/`Magenta`/`MidnightBlue`/`OffBlack`/`*Brush` colors are dead template residue, and the implicit `Button` style forces a wrong (purple `Primary`) background and 44×44 minimums on every button. Centralize the palette that the app actually uses into `Colors.xaml`, delete the template residue, fix the implicit base styles (light-only app), and introduce named Label/Button role styles so call sites become `Style="{StaticResource ...}"` and inline attributes drop to layout- and binding-specific properties only.
- Git: `.alta/plans/` is not ignored in this repo; commit this plan with the related implementation work (per the existing plan at `.alta/plans/2026-09-17-…` the convention is to commit the plan file with the implementation).

## Objective

- Make `Colors.xaml` the single source of truth for every color the app actually uses, under semantic names.
- Delete the .NET MAUI template colors that the app never adopted (`Primary`, `PrimaryDark`, `PrimaryDarkText`, `Secondary`, `SecondaryDarkText`, `Tertiary`, `Magenta`, `MidnightBlue`, `OffBlack`, `Gray100`–`Gray950`, and all `*Brush` resources).
- Fix the implicit base styles in `Styles.xaml` for a **light-only** app: replace `AppThemeBinding Light=…/Dark=…` with single light values from the new palette, and make the implicit `Button` style safe (transparent background, no forced 44×44 minimums).
- Introduce named **Label** role styles and named **Button** role styles.
- Migrate every page, popup, and custom control to the role styles; leave only layout- and binding-specific attributes inline.
- Result: one change point per visual concept, ~100 inline `TextColor`, ~90 `FontSize`, ~30 `FontAttributes` removed, and the implicit Label/Button styles finally mean something.

## Context and evidence

**The app is light-only and hardcodes its whole palette inline.** No `Dark=` branch is ever active; the `AppThemeBinding` structures in `Styles.xaml` are half-dead code, and every call site that could be styled by the implicit styles overrides them inline instead.

**`Colors.xaml` was never adopted.** The template keys are referenced only by the implicit `Styles.xaml` base styles (Button, Entry, Label, Switch, Slider, SearchBar, SearchHandler, ProgressBar, RefreshView, Picker, TimePicker, DatePicker, RadioButton, CheckBox), the `Headline`/`SubHeadline` styles, the `Shell`/`TabbedPage`/`NavigationPage`/`Page` styles, the `BoxView`/`Shadow` styles, and the `*Brush` self-references. Outside `Colors.xaml`/`Styles.xaml` the only live reference is `AppWindow.xaml:8` (`TitleBar BackgroundColor="{StaticResource Primary}"`).

**The `Primary` key is a mine.** It is `#1A2744` (navy), but the implicit `Button` style uses `{StaticResource Primary}` as `BackgroundColor` — so any button that does not override `BackgroundColor` gets navy, while the app's real primary action is **teal `#0d7377`**. The `Gray*` series (`#E1E1E1`…`#141414`) and `MidnightBlue #190649` are a different palette than the inline `#e5e7eb`/`#1a2744`/`#6b7280` the UI actually paints.

**The palette in use** (collected across all XAML): navy `#1a2744`, teal `#0d7377`, blue `#3b82f6`, secondary text `#666666` *and* `#6b7280` (inconsistent), muted `#999999` *and* `#9ca3af` (inconsistent), header-muted `#b0b8c8`, error `#d32f2f`, border `#e0e0e0` *and* `#e5e7eb` (inconsistent), surfaces `#f5f5f5`/`#f0f4f8`/`#f5f8fc`, success `#22c55e`, warning `#FFD45D`, and the `#10b981`/`#e3f2fd`/`#fff3e0` SummaryCard accents.

**Files in scope** (every file with inline Label/Button styling):
- `src/NCockpit/Resources/Styles/Colors.xaml`, `src/NCockpit/Resources/Styles/Styles.xaml`
- `src/NCockpit/AppWindow.xaml`
- `src/NCockpit/AppShell.xaml`
- `src/NCockpit/Pages/OverviewPage.xaml`, `ProjectDetailsPage.xaml`, `FoldersPage.xaml`, `SettingsPage.xaml`, `AboutPage.xaml`
- `src/NCockpit/Popups/AddFolderPopup.xaml`, `LinkSecondaryPopup.xaml`, `ScanPopup.xaml`, `ViewFilePopup.xaml`
- `src/NCockpit/Controls/SummaryCard.xaml`, `SectionCard.xaml`, `FileAction.xaml`, `FolderAction.xaml`, `GlyphButton.xaml`, `LinkAction.xaml`, `QuickAction.xaml`, `SolutionAction.xaml`

**Not in scope:** `Badge.xaml` (its `TextColor`/`Background`/`Stroke` are all bound from data — nothing to style), `HoverBorder` (no text styling), dynamic binding-driven colors (`GitStatusColor`, `BoolToFavoriteColorConverter`, `ProjectIconColorConverter`, `SelectedToBackgroundColorConverter`) which stay inline by nature.

## Decision: keep the app palette, fix the naming

The inline palette is the product's actual design language (it matches the design language section of AGENTS.md: navy `#1a2744`, teal `#0d7377`, light content, subtle borders). The fix is to **adopt it as the named palette**, not to force the template `Gray*`/`MidnightBlue` palette onto the app. The duplicate concepts (`#666666` vs `#6b7280`, `#999999` vs `#9ca3af`, `#e0e0e0` vs `#e5e7eb`) are collapsed to the value already used most in `ProjectDetailsPage` (the densest page): `#6b7280`, `#9ca3af`, `#e5e7eb`.

## The palette (new `Colors.xaml`)

| Key | Value | Used for | Replaces |
|---|---|---|---|
| `Navy` | `#1A2744` | flyout, page headers, primary text on light | inline `#1a2744`, TitleBar `Primary` |
| `Accent` | `#0D7377` | primary action (teal) | inline `#0d7377` |
| `AccentBlue` | `#3B82F6` | link/action text, "Rescan"-action, tab highlight | inline `#3b82f6` |
| `TextPrimary` | `#1A2744` | titles, values | inline `#1a2744` text |
| `TextSecondary` | `#6B7280` | labels, meta-info | `#666666` + `#6b7280` |
| `TextMuted` | `#9CA3AF` | hints, empty-states, timestamps | `#999999` + `#9ca3af` |
| `HeaderMuted` | `#B0B8C8` | sub-text on navy | inline `#b0b8c8` |
| `Error` | `#D32F2F` | error messages, delete | inline `#d32f2f` |
| `Border` | `#E5E7EB` | card strokes, dividers | `#e0e0e0` + `#e5e7eb` |
| `Surface` | `#F5F5F5` | page backgrounds (Settings, Folders, About, popup footers) | `#f5f5f5` |
| `SurfaceAlt` | `#F0F4F8` | Overview header/content + ProjectDetails header | `#f0f4f8`, `#f5f8fc` |
| `Success` | `#22C55E` | active indicator | inline `#22c55e` |
| `Warning` | `#FFD45D` | quick-action folder glyph | inline `#FFD45D` |
| `White` / `Black` | (keep) | base styles | — |

Also keep the three SummaryCard accent pairs as named resources so the cards stop using raw hex: `IconBlue` `#3B82F6` + `IconBlueBg` `#E3F2FD`, `IconAmber` `#F5A623` + `IconAmberBg` `#FFF3E0`, and `SuccessGreen` `#10B981` for the overview "active" ellipse (kept distinct from the `#22C55E` dot).

**Delete from `Colors.xaml`:** `Primary`, `PrimaryDark`, `PrimaryDarkText`, `Secondary`, `SecondaryDarkText`, `Tertiary`, `Magenta`, `MidnightBlue`, `OffBlack`, `Gray100`, `Gray200`, `Gray300`, `Gray400`, `Gray500`, `Gray600`, `Gray900`, `Gray950`, and every `*Brush` (`PrimaryBrush`…`Gray950Brush`).

> [!NOTE]
> Surface consolidation: Overview uses `#f0f4f8`, ProjectDetails uses `#f5f8fc`. Both become `SurfaceAlt` (`#F0F4F8`). This is a one-line-per-page background change and visually indistinguishable.

## The Label role styles

Implicit base (replaces current `Label` style): `FontFamily OpenSansRegular`, `FontSize 14`, `TextColor TextPrimary`, `BackgroundColor Transparent`.

| Key | Setters | Call sites it replaces |
|---|---|---|
| `PageHeader` | `FontSize 24`, `Bold`, `White` | Folders/Settings/About page titles |
| `HeaderSubtitle` | `FontSize 14`, `HeaderMuted` | "Configure where to scan…", "Your personal project launcher…" |
| `SectionTitle` | `FontSize 16`, `Bold`, `Navy` | "Projects", "Application", "Description", SectionCard title |
| `DetailValue` | `FontSize 12`, `Navy` | info-grid column 1 (non-bold values) |
| `DetailValueBold` | `FontSize 12`, `Bold`, `Navy` | info-grid column 1 (Type, Git branch) |
| `DetailLabel` | `FontSize 12`, `TextSecondary` | info-grid column 0 ("Type", "Version", …) |
| `MetaLabel` | `FontSize 12`, `TextSecondary` | "Active", folder path, timestamps |
| `HintLabel` | `FontSize 12`, `TextMuted` | "No projects found yet.", "Project not found." |
| `FieldLabel` | `FontSize 12`, `Bold`, `Navy` | "Folder Path", "Folder Type", "Name:"/… |
| `LinkLabel` | `FontSize 12`, `Bold`, `AccentBlue` | action text in FileAction/FolderAction/SolutionAction |
| `BadgeLabel` | `FontSize 10`, `Bold`, `White` | "Primary" badge |
| `ErrorLabel` | `FontSize 13`, `Error` | `ErrorMessage` labels |
| `FlyoutIcon` | `FontAwesomeSolid`, `FontSize 18`, `White`, center | AppShell item glyph |
| `FlyoutItem` | `FontSize 15`, `White`, start | AppShell item title |
| `TabActive` | `FontSize 14`, `Bold`, `AccentBlue` | ProjectDetails "Overview" tab |
| `TabInactive` | `FontSize 14`, `TextSecondary` | ProjectDetails "Source"/"Projects"/… tabs |

## The Button role styles

Rewrite the implicit `Button` base for a light-only, no-forced-size app: `FontFamily OpenSansRegular`, `FontSize 14`, `CornerRadius 6`, `Padding 12,10`, `BackgroundColor Transparent`, `TextColor TextPrimary`, `BorderWidth 0`. **Drop `MinimumHeightRequest`/`MinimumWidthRequest 44`** and the `AppThemeBinding`-based `Disabled` state (replaced by a single `Gray400`-free neutral: `TextColor TextMuted`).

| Key | Setters | Call sites it replaces |
|---|---|---|
| `PrimaryButton` | bg `Accent`, `White`, `13`, `Bold` | "Rescan all", "Add Folder", "…" chooser, "Scan" |
| `SecondaryButton` | bg `White`, `TextSecondary`, `Bold`, `Border` border `1` | "Cancel" in popups |
| `DangerButton` | bg `Error`, `White`, `12-13`, `Bold` | "Delete", ScanPopup "Cancel" |
| `IconButton` | bg `Transparent`, `Padding 0`, `18` | "✕" close, favorite star |
| `TextButton` | bg `Transparent`, `12`, `AccentBlue`, `FontAwesomeSolid` | "Favorites" filter, chevron/star rows |
| `NeutralButton` | bg `#666666`→`TextSecondary`, `White`, `11` | FoldersPage "Open" |
| `SmallButton` | `FontSize 11`, `Bold`, `White`, `Padding 8,6`, `CornerRadius 4` | FoldersPage "Scan"/"Open"/"Delete" row buttons |
| `PlusButton` | bg `AccentBlue`, `White`, `18`, `Padding 6,4`, `CornerRadius 4` | ProjectDetails "+" link-secondary |

> [!WARNING]
> The implicit `Button` base currently forces `BackgroundColor = Primary` (navy) and 44×44 minimums. Until the base is rewritten, every call site that does not override `BackgroundColor` would paint navy. The base rewrite (step 3) and the first button migration must land so that no visible button ends up navy; verify by building and eyeballing the FoldersPage "Open" button and the overview "Rescan all".

## Migration rules

- Replace inline `FontSize`/`FontAttributes`/`TextColor`/`FontFamily`/`BackgroundColor` with `Style="{StaticResource <Role>}"`.
- **Keep inline:** `Grid.*`, `Margin`, `Padding`, `WidthRequest`/`HeightRequest`, `HorizontalOptions`/`VerticalOptions`, `LineBreakMode`, `IsVisible`, `IsRunning`, `Command`, `CommandParameter`, `CanExecute`, `SemanticProperties.*`, `x:Name`, `SemanticProperties.Description`, and every binding-driven property (`TextColor="{Binding …}"`, `Progress`, `Color` on `ActivityIndicator`, `Fill` on `Ellipse`, `Background` on `Border`).
- For a label that is `MetaLabel` plus a `Margin` (e.g. the folder-path label in ProjectDetails header), apply `Style` and keep `Margin` inline.
- `Entry`/`Picker`/`Switch` call sites: give them a matching `Entry`/`Switch` role only if their inline values differ from the (rewritten) implicit base; otherwise leave them to the implicit style. The `Entry` `PlaceholderColor`/`TextColor` inline overrides in popups map to the rewritten implicit `Entry` base (`TextColor Navy`, `PlaceholderColor TextMuted`) and drop inline.
- `ActivityIndicator`/`ProgressBar` `Color`/`ProgressColor = #0d7377` → `Accent`; the implicit `ProgressBar`/`ActivityIndicator` styles take `Accent`.
- `Ellipse Fill`/`BoxView BackgroundColor`/`Border Stroke`/`Border BackgroundColor` hex values → named resources (`Success`, `Warning`, `AccentBlue`, `Border`, `SurfaceAlt`, `White`).
- `SummaryCard`/`SectionCard`/action-control internals → the matching Label roles + named colors.
- `AppShell` item template → `FlyoutIcon`/`FlyoutItem`; the flyout `Grid` `BackgroundColor` and `Expander` footer stay navy via `Navy`.

## Execution checklist

- [x] **Step 1 — `Colors.xaml`:** rewrite to the 15-key app palette (table above), keep `White`/`Black`, add the SummaryCard accent resources; delete all template keys. Build.
- [x] **Step 2 — `AppWindow.xaml`:** `TitleBar BackgroundColor="{StaticResource Primary}"` → `{StaticResource Navy}`. Build.
- [x] **Step 3 — `Styles.xaml`:**
  - [x] Rewrite every implicit control base style (Button, Entry, Label, Switch, Slider, SearchBar, SearchHandler, ProgressBar, RefreshView, Picker, TimePicker, DatePicker, RadioButton, CheckBox) to single light values from the new palette; drop `AppThemeBinding` and the `Disabled`/`PointerOver` visual-state groups that reference deleted colors.
  - [x] Rewrite the `Shell`/`TabbedPage`/`NavigationPage`/`Page` styles: `Magenta`→`Accent`, `OffBlack`→`Black`, `SecondaryDarkText`→`TextMuted`, `Gray200`/`Gray950`→`Surface`/`Black`.
  - [x] Rewrite the `BoxView`/`Shadow`/`Border`/`ActivityIndicator`/`IndicatorView` styles to the new palette.
  - [x] Delete the unused `Headline`/`SubHeadline` styles (no call site).
  - [x] Add all Label role styles and all Button role styles.
  - Build.
- [x] **Step 4 — `AppShell.xaml`:** item template → `FlyoutIcon`/`FlyoutItem`; flyout/footer `#1a2744` → `Navy`. Build.
- [x] **Step 5 — `Controls/`:** `SummaryCard`, `SectionCard`, `FileAction`, `FolderAction`, `GlyphButton`, `LinkAction`, `QuickAction`, `SolutionAction` → Label roles + named colors. Build.
- [x] **Step 6 — `Pages/AboutPage.xaml`:** header → `PageHeader`/`HeaderSubtitle`; section titles → `SectionTitle`; info-grid → `FieldLabel`/`DetailValue`/`DetailValueBold`; description → `TextSecondary`; `#e0e0e0` borders → `Border`; `#f5f5f5` → `Surface`. Build.
- [x] **Step 7 — `Pages/SettingsPage.xaml`:** header → `PageHeader`/`HeaderSubtitle`; "General" → `SectionTitle`; placeholder → `TextSecondary`; border → `Border`; background → `Surface`. Build.
- [x] **Step 8 — `Pages/FoldersPage.xaml`:** header → `PageHeader`/`HeaderSubtitle`; "Add Folder" → `PrimaryButton`; `#d32f2f` error → `ErrorLabel`; `#0d7377` ActivityIndicator → `Accent`; folder card path → `DetailValueBold`; "Primary" badge → `BadgeLabel` + `Accent` background; "Projects"/"Supporting"/"Last Scan" labels → `MetaLabel`/`HintLabel`; counts → `DetailValueBold`; "Scan"/"Open"/"Delete" → `SmallButton` (`Accent`/`TextSecondary`/`Error`); card border → `Border`; status text → `MetaLabel`; `#f5f5f5` → `Surface`. Build.
- [x] **Step 9 — `Pages/OverviewPage.xaml`:** greeting → `SectionTitle`-sized (24, bold, Navy) or a new `Greeting` role; subtitle → `MetaLabel`; search `Entry` → implicit base; "Rescan all" → `PrimaryButton`; `#d32f2f` → `ErrorLabel`; `#0d7377` → `Accent`; "Projects" → `SectionTitle`; "Favorites" filter → `TextButton`; project row name → `DetailValueBold`, path → `MetaLabel`, status → `MetaLabel`, time → `HintLabel`; SummaryCards → accent resources; empty state → `HintLabel`; `#e3f2fd`/`#fff3e0`/`#10b981` → accent resources; `#e0e0e0` → `Border`; `#f0f4f8` → `SurfaceAlt`. Build.
- [x] **Step 10 — `Pages/ProjectDetailsPage.xaml`:** header title → `DetailValueBold` (20) or a `DetailTitle` role; "Active"/path → `MetaLabel`; timestamps → `HintLabel`; tabs → `TabActive`/`TabInactive`; `#3b82f6` underline → `AccentBlue`; info-grid → `DetailLabel`/`DetailValue`/`DetailValueBold`; "Project file" link → `LinkLabel`; empty states → `HintLabel`; "Orphans" → `MetaLabel`; `+` → `PlusButton`; unlink → `DangerButton`-sized small; star/chevron rows → `TextButton`/`HintLabel`; borders → `Border`; `#f5f8fc` → `SurfaceAlt`; `#6b7280`/`#9ca3af` → `TextSecondary`/`TextMuted`. Build.
- [x] **Step 11 — `Popups/`:** `AddFolderPopup`, `LinkSecondaryPopup`, `ScanPopup`, `ViewFilePopup` — headers → `PopupHeader` (new role: 18, bold, White) + `HeaderMuted` close-button text; field labels → `FieldLabel`; entries → implicit base; errors → `ErrorLabel`; footers → `SecondaryButton`/`DangerButton`; `#b0b8c8` → `HeaderMuted`; `#e0e0e0` → `Border`; `#f5f5f5` → `Surface`. Build.
- [x] **Step 12 — sweep:** `grep` the whole `src/NCockpit` for the old hex values (`#1a2744`, `#0d7377`, `#3b82f6`, `#6b7280`, `#9ca3af`, `#d32f2f`, `#e5e7eb`, `#e0e0e0`, `#f5f5f5`, `#f0f4f8`, `#b0b8c8`, `#22c55e`, `#FFD45D`, `#10b981`, `#e3f2fd`, `#fff3e0`) and confirm none remain in XAML outside `Colors.xaml` (the one place they are defined). Confirm no `StaticResource Primary|Gray…|Magenta|MidnightBlue|OffBlack|Secondary|Tertiary` references remain. Build.
- [x] **Step 13 — final `dotnet build`** and report the before/after inline-attribute count.

## Risks

- **`App.xaml` load order** is `Converters` → `Colors` → `Styles`. As long as `Styles.xaml` only references keys defined in `Colors.xaml` (all do, post-step-1), order is satisfied. Do not introduce a `Styles.xaml`-only key referenced by `Colors.xaml`.
- **Button base rewrite** is the riskiest single change: until step 3 lands, the navy `Primary` background is the default for every button. Steps 2–3 must land before any button call site is allowed to drop its `BackgroundColor` override; the step-12 sweep catches any straggler that would render navy.
- **Collapsing `#666666`→`#6b7280` and `#999999`→`#9ca3af`** changes the secondary/muted text shade by a hair. This is the intended unification; verify FoldersPage and OverviewPage read correctly after.
- **`GitStatusColor`** (binding-driven, per-branch color) and all `Converter`-driven text colors stay inline — they are data-driven and correctly not style-able.
- **`Badge.xaml`** is fully binding-driven; it is intentionally untouched.
- The app is **light-only**; introducing a dark theme is explicitly out of scope and the deleted `AppThemeBinding` `Dark=` branches are gone by design.

## Definition of done

- `Colors.xaml` contains only the app palette (15 named colors + `White`/`Black` + SummaryCard accents) and no template keys.
- `Styles.xaml` has no `AppThemeBinding`, no reference to a deleted key, a safe implicit `Button` base (transparent, no forced 44×44), and the named Label/Button role styles.
- The step-12 sweep finds zero old hex literals in XAML outside `Colors.xaml` and zero references to deleted keys.
- `dotnet build` succeeds with no new warnings.
- No visual regression: the app looks the same as before (same colors, same layout), only the XAML is centralized.

# Projects Cockpit

Build a Windows desktop application called **Projects Cockpit** using .NET MAUI.

The purpose of this application is **not project management**. It is a personal cockpit for quickly finding projects, seeing their important information at a glance, and navigating to the files, folders, tools and online services associated with a project.

The application should feel like a polished, modern developer tool: clean, compact, fast and information-dense without feeling cluttered.

## Technology

Use:

```xml
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.7.0" />
<PackageReference Include="Microsoft.Maui.Controls" Version="10.0.80" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.9" />
<PackageReference Include="CommunityToolkit.Maui" Version="14.2.0" />
<PackageReference Include="CommunityToolkit.MVVM" Version="8.4.2" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.9" />
<PackageReference Include="Dapper" Version="2.1.79" />
<PackageReference Include="Microsoft.Extensions.AI" Version="10.7.0" />
```

Use standard .NET MAUI and MVVM Toolkit patterns.

The application is primarily a **Windows desktop application**. Do not introduce WPF, WinUI or other UI frameworks. Keep the UI in MAUI XAML.

Use dependency injection and the normal .NET hosting/application patterns.

Use SQLite + Dapper for persistent application data.

## Design reference

The attached UI mockup is the visual direction for the application.

The mockup shows three related screens:

1. Projects overview / welcome screen
2. Project details
3. Folders

Use the mockup as a **design reference**, not as an exact pixel-perfect specification.

The visual style should have:

* dark navy left navigation
* light main content area
* blue primary accent
* subtle borders and shadows
* rounded cards
* clear typography
* compact information presentation
* generous but efficient spacing
* clear clickable actions
* modern desktop application appearance

Do not turn this into a generic administration dashboard. It should look like a developer's personal project launcher/cockpit.

---

# Navigation

The application has two primary navigation items:

* **Projects**
* **Folders**

Settings/About can exist at the bottom of the navigation, but they are secondary.

Do not use a Shell flyout as the primary navigation mechanism.

---

# Projects screen

This is the most important screen.

Its primary purpose is:

> "What project do I want to work on, and where do I go from here?"

At the top show a compact welcome/summary area.

Example:

```text
Good afternoon, Colin
Here's an overview of your projects.
```

The exact greeting can be generated dynamically based on the time of day.

Below that, show a project list.

Projects should normally be ordered by **recent usage**, with the most recently used projects at the top.

A project can optionally be marked as a favorite.

The project list should make it possible to identify a project immediately without opening its details.

Show at least:

* project name
* primary path
* recent usage
* basic status information
* favorite indicator

Selecting a project opens its Project Details page.

The overview should also provide very fast access to common actions where appropriate.

Possible summary information:

* total projects
* recently used
* favorites
* projects needing attention

Do not overdo dashboard statistics. The project list is the main purpose of this screen.

---

# Project Details

Create a dedicated Project Details page.

This page should be information-dense but easy to scan.

Use sections/cards rather than one enormous form.

## Project identity

Show:

* project name
* project type
* version
* target framework(s)
* primary location
* secondary locations

The primary and secondary locations must be clickable.

Clicking a local location should open it using the appropriate Windows mechanism.

## Source control

Show:

* Git enabled
* current branch
* Git status
* GitHub repository

GitHub should be a clickable external link.

The application does not need to become a Git client. Git information is primarily there for orientation and navigation.

## Solution and projects

Show:

* solution file
* contained projects
* project type
* project version

The solution file must be clickable and should open using the configured/default associated application.

For each project, provide quick access to:

* project location
* Debug output folder
* Release output folder

Debug and Release folders should be clickable.

## Project files

Provide quick access to important files when they exist:

* `AGENTS.md`
* `README.md`

These should open using the normal Windows file association/editor.

Missing files should simply be shown as missing rather than causing errors.

## Related projects

Allow a project to link to other projects.

These links should navigate directly to the other project's details page.

This is intended for relationships such as:

```text
ntokenizers
 +-- NTokenizers.Extensions.Spectre.Console
```

The relationship does not necessarily have to represent a .NET project reference. It can simply be a manually configured relationship between projects.

---

# External services

A project can have links to external services.

These should be presented as a clear list of services rather than as raw URLs.

## Android

Support links for:

* Play Store
* Play Console
* Google Search Console
* Google Ads
* Google Analytics

## Apple

Support links for:

* App Store
* App Store Connect

## AdMob

Support:

* AdMob
* `app-ads.txt`

## Privacy

Support:

* Privacy statement

All external links should be clearly clickable.

Do not hard-code these links globally. They belong to the individual project.

Different projects may have different services.

---

# Folders screen

The Folders screen defines where Projects Cockpit searches for projects.

Example:

```text
C:\Projects
D:\Development
D:\GitHub
\\server\Projects
```

For every configured folder show:

* path
* number of projects found
* last scan
* enabled/disabled state

Provide actions to:

* open folder
* scan
* rescan
* edit/remove folder

The application should be able to scan configured folders recursively for projects.

The exact project discovery strategy should be implemented cleanly and independently from the UI.

---

# Scanning

Projects can be discovered by scanning configured folders.

Scanning should identify relevant project information from the filesystem.

For example:

* solution files
* project files
* Git repositories
* GitHub remotes
* `AGENTS.md`
* `README.md`
* build output folders
* project references
* target frameworks
* versions

Scanning should be safe and resilient.

A missing optional file or inaccessible folder must not make the entire scan fail.

Show useful progress/status information during a scan.

The UI must remain responsive.

---

# Refresh vs Scan

Keep these concepts separate.

A lightweight refresh can update information such as:

* Git status
* current branch
* timestamps

A scan can rediscover project structure and project metadata.

Avoid unnecessarily performing expensive scans when only a quick refresh is needed.

---

# Persistence

Use SQLite for application configuration and project metadata.

Use Dapper for database access.

Persist at least:

* configured folders
* projects
* project locations
* related projects
* external service links
* favorites
* recent usage
* scan information

The filesystem remains the source of truth for discovered project information where appropriate.

Do not duplicate information unnecessarily if it can reliably be rediscovered.

---

# User experience

This application is primarily about **navigation speed**.

Every important item should answer one of two questions:

1. What is this?
2. Where can I go with it?

Prefer direct actions over deep menus.

For example:

```text
Finance Planner

C:\Projects\FinancePlanner

[Open Folder] [Open Solution] [GitHub]

AGENTS.md       Open
README.md       Open

Debug           Open
Release         Open
```

Avoid adding project-management concepts such as:

* tasks
* deadlines
* project plans
* kanban
* time tracking
* complex status workflows

This is a **project cockpit**, not a project-management application.

---

# Architecture

Keep responsibilities separated.

Suggested conceptual areas:

* Models
* ViewModels
* Views
* Services
* Repositories
* Scanning/discovery
* Platform-specific Windows integration

Use CommunityToolkit.MVVM for:

* `ObservableObject`
* `ObservableProperty`
* `RelayCommand`
* `AsyncRelayCommand`

Avoid unnecessary custom infrastructure when the MAUI/MVVM Toolkit already provides the required functionality.

Use dependency injection for services.

Use asynchronous APIs for filesystem and scanning operations where appropriate.

---

# Windows integration

Provide a small abstraction for operations such as:

* opening a folder
* opening a file
* opening a URL
* opening a solution

Keep Windows-specific implementation out of the ViewModels.

The ViewModels should express intent, for example:

```text
OpenFolder
OpenSolution
OpenFile
OpenExternalLink
```

rather than containing Windows process-launching code themselves.

---

# Initial implementation

Start with a functional vertical slice.

The first usable version should contain:

1. Projects navigation
2. Folders navigation
3. SQLite persistence
4. Folder configuration
5. Project discovery
6. Projects overview
7. Project details
8. Opening folders/files/solutions
9. External links
10. Favorites and recent-project ordering

Use realistic sample data during UI development so the screens can be evaluated before the scanner is fully implemented.

Do not build speculative features that are not required by this specification.

The most important success criterion is:

> I can start Projects Cockpit, immediately see my projects, select one, and get to any important file, folder, solution or online service with one click.

Build the UI to match the supplied mockup's overall visual language and information hierarchy while keeping the implementation maintainable and idiomatic .NET MAUI.

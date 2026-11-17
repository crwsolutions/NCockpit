---
title: "Update ProjectFolders.ProjectCount and LastScanUnix on a full scan too"
kind: problem
status: pending
created: 2026-11-17
summary: "A scan from the Overview page never stamps the scanned folder, so the Folders page keeps a stale project count and no last-scan time."
---

## Why
`ScanService.ScanAsync` only calls `UpdateFolderScanStatsAsync` when a single folder id was passed (`if (folderId.HasValue)`, around line 137). `OverviewPageViewModel` (line 231) scans with `folderId = null`, so an Overview scan writes no folder stats at all, while `Pages/FoldersPage.xaml` (line 87) binds the stored `ProjectCount`.

Harness evidence (scratchpad scan-harness, scan mode): on a migrated copy of the live DB, 3 completed scans left `folder E:\repos: ProjectCount=13 LastScanUnix=1794863004` unchanged while 17 project rows exist in that folder; on a brand-new database, 2 completed scans left `ProjectCount=0 LastScanUnix=0` with 17 projects.

## What to do
Stamp every folder scanned in this run, not only an explicitly targeted one: update the stats per folder inside the scan loop (that folder's own discovered count) after the prune step, so a full scan refreshes all primary folders.

## Where
- src/NCockpit/Services/ScanService.cs (ScanAsync, the `if (folderId.HasValue)` block)
- src/NCockpit/ViewModels/OverviewPageViewModel.cs (calls ScanAsync(null, ...))
- src/NCockpit/Pages/FoldersPage.xaml (displays ProjectCount)

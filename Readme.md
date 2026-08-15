# Deedle Formatting Extension for .NET Interactive

`Deedle.DotNet.Interactive.Extension` renders Deedle `Frame` and `Series` values as HTML tables in .NET Interactive notebooks.

Repository: <https://github.com/ingted/Deedle.DotNet.Interactive.Extension>

This package is built with .NET SDK 10.0.400 against the three .NET 10 source projects in the companion [ingted/interactive](https://github.com/ingted/interactive) repository (local checkout: `G:\coldfar_py\interactive`):

- `Microsoft.DotNet.Interactive`
- `Microsoft.DotNet.Interactive.Formatting`
- `Microsoft.DotNet.Interactive.FSharp`

## Project status and support

Microsoft's original [`dotnet/interactive`](https://github.com/dotnet/interactive) repository is archived and is no longer actively supported. This extension and the companion `ingted/interactive` fork provide a practical workaround for F# and Deedle users who want to keep using .NET Interactive notebooks. The maintainer intends to keep this workaround updated over the long term.

Support is deliberately focused on F# and Deedle workflows. Compatibility or continued maintenance is not guaranteed for unrelated .NET Interactive or Polyglot Notebook features.

## Deployment

Use the deployment instructions and maintained artifacts in [`interactive/自主版`](https://github.com/ingted/interactive/tree/20260815_persistent_display_refresh/%E8%87%AA%E4%B8%BB%E7%89%88) (local checkout: `G:\coldfar_py\interactive\自主版`). `printDFFun` requires the patched `polyglot-net10-persistent-display-refresh.vsix`; the stock archived frontend does not listen for display updates after a cell has completed.

## Install

```fsharp
#r "nuget: Deedle.DotNet.Interactive.Extension, 0.1.0-alpha15"

open Deedle
open Deedle.DotNet.Interactive.Extension
```

## Configure frame size

The limits are mutable process-wide settings. Once changed, later renders use the new values until they are changed again.

```fsharp
DeedleFormatterSettings.FrameRowLimit <- Some 100
DeedleFormatterSettings.FrameColumnLimit <- Some 30
```

Use `None` to render every row or column:

```fsharp
DeedleFormatterSettings.FrameRowLimit <- None
DeedleFormatterSettings.FrameColumnLimit <- None
```

## Refresh a frame in place

`printDFFun intervalMilli f` calls `f` immediately to create the initial display. It then calls `f` at the requested interval and replaces the same cell output area with the newly rendered frame.

The cell is executed once. After that, refreshes neither add notebook cells nor resubmit the cell code, and you do not need to press **Run** again.

```fsharp
let dfTimer =
    printDFFun 1000 (fun () -> currentFrame)
```

Keep the returned `System.Threading.Timer` in a binding for as long as updates are needed. Dispose it to stop refreshing:

```fsharp
dfTimer.Dispose()
```

If one refresh is still running when the next interval arrives, the overlapping refresh is skipped. If `f` throws, the error is shown in the same output area and a later successful refresh can replace it.

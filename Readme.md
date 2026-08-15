# Deedle Formatting Extension for .NET Interactive

`Deedle.DotNet.Interactive.Extension` renders Deedle `Frame` and `Series` values as HTML tables in .NET Interactive notebooks.

Repository: <https://github.com/ingted/Deedle.DotNet.Interactive.Extension>

This package is built against the .NET 10 source projects in the companion [ingted/interactive](https://github.com/ingted/interactive) repository (local checkout: `G:\coldfar_py\interactive`).

## Project status and support

Microsoft's original [`dotnet/interactive`](https://github.com/dotnet/interactive) repository is archived and is no longer actively supported. This extension and the companion `ingted/interactive` fork provide a practical workaround for F# and Deedle users who want to keep using .NET Interactive notebooks. The maintainer intends to keep this workaround updated over the long term.

## Install

```fsharp
#r "nuget: Deedle.DotNet.Interactive.Extension, 0.1.0-alpha13"

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

```fsharp
let dfTimer =
    printDFFun 1000 (fun () -> currentFrame)
```

Keep the returned `System.Threading.Timer` in a binding for as long as updates are needed. Dispose it to stop refreshing:

```fsharp
dfTimer.Dispose()
```

If one refresh is still running when the next interval arrives, the overlapping refresh is skipped. If `f` throws, the error is shown in the same output area and a later successful refresh can replace it.

# Deedle Formatting Extension for .NET Interactive

`Deedle.DotNet.Interactive.Extension` renders Deedle `Frame` and `Series` values as HTML tables in .NET Interactive notebooks.

Repository: <https://github.com/ingted/Deedle.DotNet.Interactive.Extension>

This package is built with .NET SDK 10.0.402 against version `1.0.0-dev.26522.2` of three local packages from the companion [ingted/interactive](https://github.com/ingted/interactive) repository (local checkout: `G:\coldfar_py\interactive`):

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
#r "nuget: Deedle.DotNet.Interactive.Extension, 0.1.1"

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

`printDFFun intervalMilli f` creates a stable display area immediately. After one interval it calls `f` for the first frame, then continues at the requested interval and replaces the same cell output area with the newly rendered frame.

The cell is executed once. After that, refreshes neither add notebook cells nor resubmit the cell code, and you do not need to press **Run** again.

```fsharp
let dfTimer =
    printDFFun 1000 (fun () -> currentFrame)
```

Keep the returned `DFPrintTimer` in a binding for as long as updates are needed. It supports the familiar `Change` and `Dispose` members. Dispose it to stop refreshing and cancel an in-progress asynchronous refresh:

```fsharp
dfTimer.Dispose()
```

If one refresh is still running when the next interval arrives, the overlapping refresh is skipped. If `f` throws, the error is shown in the same output area and a later successful refresh can replace it.

For asynchronous work before a refresh, use `printDFFunBase`. Its decorator runs once and receives a persistent `ContextPrint`, the current `Kernel`, a cancellation token, and finally the frame function. It returns the refresh function that receives the current refresh count and the number of completed successful refreshes on every accepted timer tick. Placing the frame function last supports normal F# pipeline and partial-application style. `Task.Delay` pauses without blocking a thread; the extension awaits both the refresh function and its own `Kernel.SendAsync` internally.

```fsharp
open System.Threading
open System.Threading.Tasks

let decorate contextPrint kernel cancellationToken getFrame =
    // This outer function runs once and can initialize closure-local state.
    let mutable decoratorState = 0

    // Only this returned function runs on every accepted timer tick.
    fun count successCount ->
        task {
            decoratorState <- decoratorState + 1

            if count % 10L = 0L then
                do! contextPrint.PrintLineAsync(
                    $"count: {count}, success: {successCount}")
                do! Task.Delay(3000, cancellationToken)

            // kernel is available here when a custom Kernel command is needed.
            return getFrame ()
        }

let printTimer =
    printDFFunBase -1 500 (makeFrame ()) decorate

// Start immediately and refresh every 500 ms.
printTimer.Change(0, 500) |> ignore
```

`ContextPrint.PrintLineAsync` replaces the text in its existing display area. Use
`PrintLineAppendAsync` when each message should be retained as another line. Both
methods call `PrintLineBaseAsync`, whose optional `ifAppend` argument defaults to
`false`:

```fsharp
do! contextPrint.PrintLineAsync("latest only")
do! contextPrint.PrintLineAppendAsync("retain this line")
do! contextPrint.PrintLineBaseAsync("explicit append", ifAppend = true)
```

Switching between replace and append mode clears the previous text history. A
later append therefore starts a new history instead of restoring text that was
visible before the mode change.

`due = -1` creates the display in a stopped state until `Change` is called. `due = 0` schedules the first real frame one full interval later, after the cell has completed. Every real frame, including the first, therefore passes through the same asynchronous callback and has access to the `Kernel`.

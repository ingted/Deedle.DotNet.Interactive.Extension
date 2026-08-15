namespace Deedle.DotNet.Interactive.Extension

[<AutoOpen>]
module DFPrint =
    open System
    open System.Threading
    open Deedle
    open Microsoft.DotNet.Interactive
    open Microsoft.DotNet.Interactive.Commands
    open Microsoft.DotNet.Interactive.Formatting

    let printDFFun
        (intervalMilli: int)
        (f: unit -> Frame<'R, 'C>)
        : Timer =
        if intervalMilli <= 0 then
            invalidArg (nameof intervalMilli) "intervalMilli must be greater than zero."

        if isNull (box f) then
            nullArg (nameof f)

        let currentKernel = Kernel.Current

        if isNull currentKernel then
            invalidOp "printDFFun must be started from a .NET Interactive cell."

        let kernel =
            let rootKernel = Kernel.Root
            if isNull rootKernel then currentKernel else rootKernel

        let initialFrame = f ()
        let displayedValue = Kernel.display (initialFrame, HtmlFormatter.MimeType)
        let callbackGate = new SemaphoreSlim(1, 1)

        let updateDisplay (formattedValue: FormattedValue) =
            let command = UpdateDisplayedValue(formattedValue, displayedValue.DisplayId)
            kernel.SendAsync(command, CancellationToken.None).GetAwaiter().GetResult()
            |> ignore

        let callback _ =
            if callbackGate.Wait(0) then
                try
                    try
                        let nextFrame = f ()

                        FormattedValue.CreateSingleFromObject(nextFrame, HtmlFormatter.MimeType)
                        |> updateDisplay
                    with ex ->
                        FormattedValue(PlainTextFormatter.MimeType, $"printDFFun: %s{ex.Message}")
                        |> updateDisplay
                finally
                    callbackGate.Release() |> ignore

        // Do not retain the completed cell's invocation context in the timer.
        // Each refresh is sent as an independent UpdateDisplayedValue command.
        use _ = ExecutionContext.SuppressFlow()
        new Timer(TimerCallback callback, null, intervalMilli, intervalMilli)

namespace Deedle.DotNet.Interactive.Extension

[<AutoOpen>]
module DFPrint =
    open System
    open System.Threading
    open System.Threading.Tasks
    open Deedle
    open Microsoft.DotNet.Interactive
    open Microsoft.DotNet.Interactive.Commands
    open Microsoft.DotNet.Interactive.Formatting

    [<Sealed>]
    type DFPrintTimer(timer: Timer, cancellation: CancellationTokenSource) =
        let mutable disposed = 0

        member _.Change(dueTime: int, period: int) =
            timer.Change(dueTime, period)

        member _.Change(dueTime: TimeSpan, period: TimeSpan) =
            timer.Change(dueTime, period)

        member _.Dispose() =
            if Interlocked.Exchange(&disposed, 1) = 0 then
                timer.Dispose()
                cancellation.Cancel()

        interface IDisposable with
            member this.Dispose() = this.Dispose()

    let printDFFunBase
        (due: int)
        (intervalMilli: int)
        (f: unit -> Frame<'R, 'C>)
        (decorate:
            ContextPrint ->
            Kernel ->
            CancellationToken ->
            (unit -> Frame<'R, 'C>) ->
            (int64 -> int64 -> Task<Frame<'R, 'C>>))
        : DFPrintTimer =
        if intervalMilli <= 0 then
            invalidArg (nameof intervalMilli) "intervalMilli must be greater than zero."

        if due < Timeout.Infinite then
            invalidArg (nameof due) "due must be Timeout.Infinite (-1), zero, or greater."

        if isNull (box f) then
            nullArg (nameof f)

        if isNull (box decorate) then
            nullArg (nameof decorate)

        let context = KernelInvocationContext.Current

        if isNull context then
            invalidOp "printDFFun must be started from a .NET Interactive cell."

        let currentKernel = Kernel.Current

        let kernel =
            let rootKernel = Kernel.Root
            if isNull rootKernel then currentKernel else rootKernel

        let mutable counter = 0L
        let mutable successCounter = 0L
        let cancellation = new CancellationTokenSource()
        let cancellationToken = cancellation.Token
        let contextPrint = new ContextPrint(context, kernel, cancellationToken)
        let refresh =
            f
            |> decorate contextPrint kernel cancellationToken

        // The placeholder establishes a stable display ID without bypassing the
        // user-supplied asynchronous refresh function for the first real frame.
        let displayedValue =
            context.Display(Unchecked.defaultof<Frame<'R, 'C>>, HtmlFormatter.MimeType)
        let callbackGate = new SemaphoreSlim(1, 1)

        let updateDisplayAsync (formattedValue: FormattedValue) =
            task {
                let command = UpdateDisplayedValue(formattedValue, displayedValue.DisplayId)
                let! _ = kernel.SendAsync(command, cancellationToken)
                return ()
            }

        let reportErrorAsync currentCount completedCount (ex: exn) =
            task {
                try
                    let formattedValue =
                        FormattedValue(
                            PlainTextFormatter.MimeType,
                            $"[printDFFun] count:{currentCount}, count_success:{completedCount}. %s{ex.Message}")

                    do! updateDisplayAsync formattedValue
                with
                | :? OperationCanceledException when cancellationToken.IsCancellationRequested ->
                    ()
                | reportError ->
                    printfn $"[printDFFun] failed to display an error: %s{reportError.Message}"
            }

        let callback _ =
            task {
                // Wait(0) only tests the gate; it never blocks a Timer thread.
                let currentCount = Interlocked.Increment(&counter)
                if callbackGate.Wait(0) then
                    let completedCount = Interlocked.Read(&successCounter)

                    try
                        try
                            let! nextFrame =
                                refresh currentCount completedCount

                            let formattedValue =
                                FormattedValue.CreateSingleFromObject(
                                    nextFrame,
                                    HtmlFormatter.MimeType)

                            do! updateDisplayAsync formattedValue
                            Interlocked.Increment(&successCounter) |> ignore
                        with
                        | :? OperationCanceledException when cancellationToken.IsCancellationRequested ->
                            ()
                        | ex ->
                            do! reportErrorAsync currentCount completedCount ex
                    finally
                        callbackGate.Release() |> ignore
            }
            |> ignore

        // Do not retain the completed cell's invocation context in the timer.
        // Each refresh is sent as an independent UpdateDisplayedValue command.
        use _ = ExecutionContext.SuppressFlow()

        let effectiveDue =
            if due = 0 then intervalMilli
            else due

        let timer = new Timer(TimerCallback callback, null, effectiveDue, intervalMilli)
        new DFPrintTimer(timer, cancellation)

    let printDFFun
        (intervalMilli: int)
        (f: unit -> Frame<'R, 'C>)
        =
        printDFFunBase 0 intervalMilli f (fun _ _ _ getFrame ->
            fun _ _ -> Task.FromResult(getFrame ()))

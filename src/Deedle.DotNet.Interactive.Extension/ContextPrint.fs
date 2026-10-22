namespace Deedle.DotNet.Interactive.Extension

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Microsoft.DotNet.Interactive
open Microsoft.DotNet.Interactive.Commands
open Microsoft.DotNet.Interactive.Formatting

[<Sealed>]
type ContextPrint
    (
        context: KernelInvocationContext,
        kernel: Kernel,
        cancellationToken: CancellationToken
    ) =

    do
        if isNull context then
            nullArg (nameof context)

        if isNull kernel then
            nullArg (nameof kernel)

    let displayedValue =
        KernelInvocationContextExtensions.DisplayAs(
            context,
            String.Empty,
            PlainTextFormatter.MimeType)

    let lines = ResizeArray<string>()
    let updateGate = new SemaphoreSlim(1, 1)
    let mutable currentIfAppend = false

    member _.PrintLineBaseAsync(message: string, ?ifAppend: bool) =
        let ifAppend = defaultArg ifAppend false

        task {
            do! updateGate.WaitAsync(cancellationToken)

            try
                if currentIfAppend <> ifAppend || not ifAppend then
                    lines.Clear()

                currentIfAppend <- ifAppend
                lines.Add(if isNull message then String.Empty else message)

                let formattedValue =
                    FormattedValue(
                        PlainTextFormatter.MimeType,
                        String.Join(Environment.NewLine, lines))

                let command =
                    UpdateDisplayedValue(
                        formattedValue,
                        displayedValue.DisplayId)

                let! _ = kernel.SendAsync(command, cancellationToken)
                return ()
            finally
                updateGate.Release() |> ignore
        }

    member this.PrintLineAsync(message: string) =
        this.PrintLineBaseAsync(message)

    member this.PrintLineAppendAsync(message: string) =
        this.PrintLineBaseAsync(message, ifAppend = true)

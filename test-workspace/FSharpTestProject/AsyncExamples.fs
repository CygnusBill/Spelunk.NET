namespace FSharpTestProject

open System
open System.Threading.Tasks

module AsyncExamples =
    // Simple async workflow
    let asyncComputation = async {
        printfn "Starting async computation..."
        do! Async.Sleep 1000
        printfn "Async computation completed!"
        return 42
    }

    // Async function
    let downloadDataAsync url = async {
        printfn "Downloading from %s..." url
        do! Async.Sleep 500  // Simulate network delay
        return sprintf "Data from %s" url
    }

    // Parallel async operations
    let downloadMultipleAsync urls = async {
        let! results = 
            urls
            |> List.map downloadDataAsync
            |> Async.Parallel
        return results |> Array.toList
    }

    // Async with exception handling
    let safeDownloadAsync url = async {
        try
            let! data = downloadDataAsync url
            return Ok data
        with
        | ex -> return Error ex.Message
    }

    // Convert between Task and Async
    let taskExample() =
        Task.Run(fun () -> 
            System.Threading.Thread.Sleep(100)
            "Task result"
        )

    let asyncFromTask() = async {
        let task = taskExample()
        let! result = Async.AwaitTask task
        return result
    }

    // Async sequences
    let asyncSeq = asyncSeq {
        for i in 1..5 do
            do! Async.Sleep 100
            yield i * i
    }

    // Cancellation support
    let cancellableOperation (cancellationToken: System.Threading.CancellationToken) = async {
        for i in 1..10 do
            cancellationToken.ThrowIfCancellationRequested()
            printfn "Processing %d..." i
            do! Async.Sleep 200
        return "Completed"
    }

    // MailboxProcessor (actor model)
    type Message =
        | Add of int
        | Get of AsyncReplyChannel<int>
        | Reset

    let counterAgent = MailboxProcessor.Start(fun inbox ->
        let rec loop count = async {
            let! msg = inbox.Receive()
            match msg with
            | Add n -> 
                return! loop (count + n)
            | Get reply ->
                reply.Reply count
                return! loop count
            | Reset ->
                return! loop 0
        }
        loop 0
    )

    // Using the agent
    let testAgent() = async {
        counterAgent.Post(Add 10)
        counterAgent.Post(Add 20)
        let! result = counterAgent.PostAndAsyncReply(Get)
        printfn "Counter value: %d" result
        counterAgent.Post(Reset)
    }
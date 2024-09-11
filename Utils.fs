namespace Utils

open System.Collections.Generic
open System.Threading.Tasks

module ArrayExtensions =
    let filterSome (array: 'T option array): 'T array =
        array |> Array.filter (fun o -> o.IsSome) |> Array.map (fun o -> o.Value)
        
module Operators =
    /// <summary>
    /// Returns false if <paramref name="value"/> is None or evaluates <paramref name="expr"/> and returns the result.
    /// </summary>
    /// <param name="value">The option to check.</param>
    /// <param name="expr">The function to execute with the value provided.</param>
    let inline (?) (value: 'a option) (expr: 'a -> bool) : bool =
        if value.IsSome then expr value.Value else false
    /// <summary>
    /// Pipes <paramref name="value"/> into a list of <paramref name="functions"/> and returns a list of the results.
    /// </summary>
    /// <param name="value">The value to pipe into the functions</param>
    /// <param name="functions">The functions to execute with the value provided</param>
    let inline (|>|) value functions =
        functions |> List.map (fun f -> f value)
    /// <summary>
    /// Passes the <paramref name="value"/> to (and returns) <paramref name="func"/> if it is Some. Returns def if None. 
    /// </summary>
    /// <param name="value">The option value to operate on.</param>
    /// <param name="func">The function accepting the value in it's pure form.</param>
    /// <param name="def">The alternative to the function's return value.</param>
    let inline (|?>) (value: 'a option) (func: 'a -> 'b, def: 'b) =
        if value.IsSome then func(value.Value) else def
    /// <summary>
    /// Pipes the option into the function if it is something and also turns the <paramref name="func"/>'s result into an option return.
    /// </summary>
    /// <param name="value">The value to pipe in.</param>
    /// <param name="func">The func to run if the value is not None.</param>
    let inline (|??>) (value: 'a option) func =
        if value.IsSome then func(value.Value) |> Some else None
    /// <summary>
    /// Turns a potentially null object into an option with None if null.
    /// </summary>
    /// <param name="obj">A potentially null object</param>j
    let inline (!?!) (obj: 'A): 'A option =
        if obj <> null then Some obj else None
    /// <summary>
    /// Ignores the result of the preceding async expression.
    /// </summary>
    /// <param name="item">The item to ignore.</param>
    let inline (|>=) (item: Async<'A> ) () = item |> Async.Ignore
    let inline (|>==) (item: Task<'A> ) () = item |> Async.AwaitTask |> Async.Ignore
    /// <summary>
    /// Ignores the result of the following expression.
    /// </summary>
    /// <param name="item">The item to ignore.</param>
    let inline (~-) item = item |> ignore
    /// <summary>
    /// Ignores the result of the preceding expression.
    /// </summary>
    /// <param name="item">The item to ignore.</param>
    let inline (|>-) item () = item |> ignore
    /// <summary>
    /// Feeds the result of <paramref name="task1"/> into <paramref name="task2"/> in an asynchronous environment.
    /// </summary>
    /// <param name="task1">The first task to perform.</param>
    /// <param name="task2">The second task to perform on the result of the first.</param>
    let inline (>>=) (task1: Task<'T1>) (task2: 'T1 -> Task<'T2>) : Task<'T2> =
        task { 
            let! result = task1
            return! task2 result
        }
    
    
    let AsyncForLoop (asyncSeq: IAsyncEnumerable<'T>) (action: 'T -> Task<unit>) =
        async {
            let enumerator = asyncSeq.GetAsyncEnumerator()
            let mutable hasMore = true
            while hasMore do
                let! moveNextResult = enumerator.MoveNextAsync().AsTask() |> Async.AwaitTask
                hasMore <- moveNextResult
                if hasMore then
                    return! action enumerator.Current |> Async.AwaitTask
        }

    type Event<'T, 'U>() =
        member val subscriptions: ('T -> 'U) array = Array.empty with get,set
        member this.execSubscriptions a = this.subscriptions |> Array.map(fun s -> s a)

    type FEvent<'T, 'U>() =
        inherit Event<'T, 'U>()
        
        member this.Trigger(arg: 'T): 'U array = this.execSubscriptions arg
        member this.Subscribe(func: 'T -> 'U) = 
            this.subscriptions <- Array.append this.subscriptions [|func|]

    type AsyncEvent<'T, 'U>() =
        inherit Event<'T, Async<'U>>()

        member this.TriggerParallel(arg: 'T): Async<'U array> = async {
            return! this.execSubscriptions arg |> Async.Parallel
        }
        member this.TriggerSequential(arg: 'T): Async<'U array> = async {
            return! this.execSubscriptions arg |> Async.Sequential
        }
        member this.Subscribe (func: 'T -> Async<'U>) = 
            this.subscriptions <- Array.append this.subscriptions [|func|]
        member this.Subscribe (func: 'T -> Task<'U>) =
            this.subscriptions <- Array.append this.subscriptions [|(fun t -> func t |> Async.AwaitTask)|]
namespace FSharpTestProject

open System

// Record types
type Person = {
    FirstName: string
    LastName: string
    Age: int
    Email: string option
}

module Person =
    let create firstName lastName age email =
        { FirstName = firstName; LastName = lastName; Age = age; Email = email }
    
    let fullName person =
        sprintf "%s %s" person.FirstName person.LastName
    
    let isAdult person =
        person.Age >= 18

// Discriminated unions
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Square of side: float
    | Triangle of base': float * height: float

module Shape =
    let area shape =
        match shape with
        | Circle radius -> Math.PI * radius * radius
        | Rectangle (width, height) -> width * height
        | Square side -> side * side
        | Triangle (base', height) -> 0.5 * base' * height

    let perimeter shape =
        match shape with
        | Circle radius -> 2.0 * Math.PI * radius
        | Rectangle (width, height) -> 2.0 * (width + height)
        | Square side -> 4.0 * side
        | Triangle _ -> failwith "Perimeter calculation for triangle not implemented"

// Option type usage
type BankAccount = {
    AccountNumber: string
    Balance: decimal
    OverdraftLimit: decimal option
}

module BankAccount =
    let create accountNumber initialBalance overdraftLimit =
        { AccountNumber = accountNumber
          Balance = initialBalance
          OverdraftLimit = overdraftLimit }

    let withdraw amount account =
        let maxWithdrawal = 
            match account.OverdraftLimit with
            | Some limit -> account.Balance + limit
            | None -> account.Balance
        
        if amount <= maxWithdrawal then
            Some { account with Balance = account.Balance - amount }
        else
            None

    let deposit amount account =
        { account with Balance = account.Balance + amount }

// Generic types
type Result<'TSuccess, 'TError> =
    | Ok of 'TSuccess
    | Error of 'TError

type Tree<'T> =
    | Leaf of 'T
    | Node of Tree<'T> * Tree<'T>

module Tree =
    let rec count tree =
        match tree with
        | Leaf _ -> 1
        | Node (left, right) -> count left + count right

    let rec map f tree =
        match tree with
        | Leaf value -> Leaf (f value)
        | Node (left, right) -> Node (map f left, map f right)

// Class type
type Counter(initial: int) =
    let mutable value = initial

    member this.Value = value
    member this.Increment() = value <- value + 1
    member this.Decrement() = value <- value - 1
    member this.Reset() = value = initial

// Interface
type ILogger =
    abstract member Log: string -> unit
    abstract member LogError: string -> unit

// Class implementing interface
type ConsoleLogger() =
    interface ILogger with
        member this.Log message =
            printfn "[INFO] %s" message
        
        member this.LogError message =
            eprintfn "[ERROR] %s" message

// Active patterns
let (|Even|Odd|) n =
    if n % 2 = 0 then Even else Odd

let (|Positive|Negative|Zero|) n =
    if n > 0 then Positive
    elif n < 0 then Negative
    else Zero

let describeNumber n =
    match n with
    | Even & Positive -> "Even and positive"
    | Even & Negative -> "Even and negative"
    | Odd & Positive -> "Odd and positive"
    | Odd & Negative -> "Odd and negative"
    | Zero -> "Zero"
    | _ -> "Unknown"
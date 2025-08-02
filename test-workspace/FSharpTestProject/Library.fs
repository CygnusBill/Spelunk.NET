namespace FSharpTestProject

module MathOperations =
    // Simple functions
    let add x y = x + y
    let subtract x y = x - y
    let multiply x y = x * y
    let divide x y = 
        if y = 0.0 then 
            failwith "Division by zero"
        else 
            x / y

    // Recursive function
    let rec factorial n =
        match n with
        | 0 | 1 -> 1
        | _ -> n * factorial (n - 1)

    // Higher-order function
    let applyOperation op x y = op x y

    // Curried function
    let addTen = add 10

    // Function with tupled arguments
    let addTuple (x, y) = x + y

    // Inline function
    let inline square x = x * x

    // Mutable value
    let mutable counter = 0
    let incrementCounter() =
        counter <- counter + 1
        counter

module ListOperations =
    // List processing functions
    let rec length list =
        match list with
        | [] -> 0
        | _ :: tail -> 1 + length tail

    let rec map f list =
        match list with
        | [] -> []
        | head :: tail -> f head :: map f tail

    let rec filter predicate list =
        match list with
        | [] -> []
        | head :: tail ->
            if predicate head then
                head :: filter predicate tail
            else
                filter predicate tail

    // Using built-in functions
    let doubleAll list = List.map (fun x -> x * 2) list
    let sumAll list = List.fold (+) 0 list
    let evenNumbers list = List.filter (fun x -> x % 2 = 0) list

module StringOperations =
    open System

    let toUpperCase (str: string) = str.ToUpper()
    let toLowerCase (str: string) = str.ToLower()
    
    let split (delimiter: char) (str: string) = 
        str.Split([|delimiter|]) |> Array.toList

    let join (delimiter: string) (strings: string list) =
        String.Join(delimiter, strings)

    let contains (substring: string) (str: string) =
        str.Contains(substring)

    let replace (oldValue: string) (newValue: string) (str: string) =
        str.Replace(oldValue, newValue)
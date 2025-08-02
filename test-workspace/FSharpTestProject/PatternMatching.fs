namespace FSharpTestProject

module PatternMatching =
    // Basic pattern matching
    let describeOption opt =
        match opt with
        | Some value -> sprintf "Has value: %A" value
        | None -> "No value"

    // Pattern matching with guards
    let categorizeNumber n =
        match n with
        | n when n < 0 -> "Negative"
        | 0 -> "Zero"
        | n when n < 10 -> "Single digit"
        | n when n < 100 -> "Double digit"
        | _ -> "Large number"

    // List patterns
    let rec sumList list =
        match list with
        | [] -> 0
        | [x] -> x
        | head :: tail -> head + sumList tail

    let getFirstTwo list =
        match list with
        | [] -> None, None
        | [x] -> Some x, None
        | x :: y :: _ -> Some x, Some y

    // Tuple patterns
    let addPair pair =
        match pair with
        | (x, y) -> x + y

    let describe3Tuple tuple =
        match tuple with
        | (0, 0, 0) -> "Origin"
        | (x, 0, 0) -> sprintf "On X-axis at %d" x
        | (0, y, 0) -> sprintf "On Y-axis at %d" y
        | (0, 0, z) -> sprintf "On Z-axis at %d" z
        | (x, y, z) -> sprintf "Point at (%d, %d, %d)" x y z

    // Record patterns
    type Contact = {
        Name: string
        Email: string option
        Phone: string option
    }

    let getContactMethod contact =
        match contact with
        | { Email = Some email; Phone = Some phone } -> 
            sprintf "Can contact via email (%s) or phone (%s)" email phone
        | { Email = Some email } -> 
            sprintf "Can only contact via email (%s)" email
        | { Phone = Some phone } -> 
            sprintf "Can only contact via phone (%s)" phone
        | _ -> "No contact method available"

    // Array patterns
    let describeArray arr =
        match arr with
        | [||] -> "Empty array"
        | [|x|] -> sprintf "Single element: %A" x
        | [|x; y|] -> sprintf "Two elements: %A and %A" x y
        | arr -> sprintf "Array with %d elements" arr.Length

    // Active pattern usage
    let (|EmailAddress|_|) str =
        if System.Text.RegularExpressions.Regex.IsMatch(str, @"^\S+@\S+\.\S+$") then
            Some str
        else
            None

    let (|PhoneNumber|_|) str =
        if System.Text.RegularExpressions.Regex.IsMatch(str, @"^\d{3}-\d{3}-\d{4}$") then
            Some str
        else
            None

    let classifyContact str =
        match str with
        | EmailAddress email -> sprintf "Email: %s" email
        | PhoneNumber phone -> sprintf "Phone: %s" phone
        | _ -> "Unknown contact format"

    // Nested patterns
    type Expr =
        | Const of int
        | Add of Expr * Expr
        | Multiply of Expr * Expr
        | Variable of string

    let rec evaluate vars expr =
        match expr with
        | Const n -> n
        | Variable name -> 
            Map.tryFind name vars |> Option.defaultValue 0
        | Add (left, right) -> 
            evaluate vars left + evaluate vars right
        | Multiply (left, right) -> 
            evaluate vars left * evaluate vars right

    // Pattern matching in let bindings
    let getCoordinates() = (10, 20)
    let x, y = getCoordinates()

    // Pattern matching in function parameters
    let printPair (x, y) = 
        printfn "(%d, %d)" x y

    // Exception patterns
    let safeDivide x y =
        try
            Some (x / y)
        with
        | :? System.DivideByZeroException -> None
        | ex -> failwithf "Unexpected error: %s" ex.Message
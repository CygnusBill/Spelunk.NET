// F# Test File for Detection
module FSharpDetectionTest

// Simple F# function
let add x y = x + y

// F# type definition
type Person = {
    Name: string
    Age: int
}

// F# discriminated union
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Triangle of base: float * height: float

// F# pattern matching
let calculateArea shape =
    match shape with
    | Circle radius -> Math.PI * radius * radius
    | Rectangle (width, height) -> width * height
    | Triangle (base, height) -> 0.5 * base * height

// F# async computation
let fetchDataAsync url = async {
    // Simulated async operation
    do! Async.Sleep 1000
    return sprintf "Data from %s" url
}
Namespace VBTestProject
    Public Class Calculator
        Public Function Add(a As Integer, b As Integer) As Integer
            Return a + b
        End Function
        
        Public Function Subtract(a As Integer, b As Integer) As Integer
            Return a - b
        End Function
        
        Public Function Multiply(a As Integer, b As Integer) As Integer
            Return a * b
        End Function
        
        Public Function Divide(a As Double, b As Double) As Double
            If b = 0 Then
                Throw New DivideByZeroException("Cannot divide by zero")
            End If
            Return a / b
        End Function
    End Class
End Namespace
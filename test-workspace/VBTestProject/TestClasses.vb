Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Namespace VBTestProject.TestClasses
    ' Test abstract class
    Public MustInherit Class Shape
        Public MustOverride Function CalculateArea() As Double
        Public Overridable Function GetDescription() As String
            Return "This is a shape"
        End Function
    End Class

    ' Test inheritance and method overriding
    Public Class Circle
        Inherits Shape

        Private _radius As Double

        Public Property Radius As Double
            Get
                Return _radius
            End Get
            Set(value As Double)
                If value < 0 Then
                    Throw New ArgumentException("Radius cannot be negative")
                End If
                _radius = value
            End Set
        End Property

        Public Sub New(radius As Double)
            Me.Radius = radius
        End Sub

        Public Overrides Function CalculateArea() As Double
            Return Math.PI * _radius * _radius
        End Function

        Public Overrides Function GetDescription() As String
            Return $"Circle with radius {_radius}"
        End Function
    End Class

    ' Test interface implementation
    Public Interface ILogger
        Sub LogInfo(message As String)
        Sub LogError(message As String, exception As Exception)
        Function GetLogCount() As Integer
    End Interface

    Public Class ConsoleLogger
        Implements ILogger

        Private _logCount As Integer = 0

        Public Sub LogInfo(message As String) Implements ILogger.LogInfo
            _logCount += 1
            Console.WriteLine($"[INFO] {DateTime.Now}: {message}")
        End Sub

        Public Sub LogError(message As String, exception As Exception) Implements ILogger.LogError
            _logCount += 1
            Console.WriteLine($"[ERROR] {DateTime.Now}: {message} - {exception.Message}")
        End Sub

        Public Function GetLogCount() As Integer Implements ILogger.GetLogCount
            Return _logCount
        End Function
    End Class

    ' Test async methods
    Public Class AsyncProcessor
        Public Async Function ProcessDataAsync(data As String) As Task(Of String)
            Await Task.Delay(100)
            Return data.ToUpper()
        End Function

        Public Async Function ProcessMultipleAsync(items As List(Of String)) As Task(Of List(Of String))
            Dim tasks = New List(Of Task(Of String))()
            
            For Each item In items
                tasks.Add(ProcessDataAsync(item))
            Next

            Dim results = Await Task.WhenAll(tasks)
            Return results.ToList()
        End Function
    End Class

    ' Test properties with different accessors
    Public Class PropertyExample
        Private _readOnlyValue As String = "ReadOnly"
        Private _writeOnlyValue As String
        Private _readWriteValue As Integer

        ' Read-only property
        Public ReadOnly Property ReadOnlyProp As String
            Get
                Return _readOnlyValue
            End Get
        End Property

        ' Write-only property
        Public WriteOnly Property WriteOnlyProp As String
            Set(value As String)
                _writeOnlyValue = value
            End Set
        End Property

        ' Read-write property
        Public Property ReadWriteProp As Integer
            Get
                Return _readWriteValue
            End Get
            Set(value As Integer)
                _readWriteValue = value
            End Set
        End Property

        ' Auto-implemented property
        Public Property AutoProp As String
    End Class

    ' Test static (Shared) members
    Public Class StaticExample
        Public Shared ReadOnly CreatedInstances As New List(Of StaticExample)()
        Private Shared _counter As Integer = 0

        Public ReadOnly Property Id As Integer

        Public Sub New()
            _counter += 1
            Id = _counter
            CreatedInstances.Add(Me)
        End Sub

        Public Shared Function GetTotalInstances() As Integer
            Return _counter
        End Function

        Public Shared Sub ResetCounter()
            _counter = 0
            CreatedInstances.Clear()
        End Sub
    End Class

    ' Test generic class
    Public Class GenericRepository(Of T As {Class, New})
        Private _items As New List(Of T)()

        Public Sub Add(item As T)
            _items.Add(item)
        End Sub

        Public Function GetAll() As IEnumerable(Of T)
            Return _items
        End Function

        Public Function Find(predicate As Func(Of T, Boolean)) As T
            Return _items.FirstOrDefault(predicate)
        End Function
    End Class

    ' Test structure
    Public Structure Point
        Public X As Integer
        Public Y As Integer

        Public Sub New(x As Integer, y As Integer)
            Me.X = x
            Me.Y = y
        End Sub

        Public Function DistanceFrom(other As Point) As Double
            Dim dx = X - other.X
            Dim dy = Y - other.Y
            Return Math.Sqrt(dx * dx + dy * dy)
        End Function
    End Structure

    ' Test enum
    Public Enum LogLevel
        Debug = 0
        Info = 1
        Warning = 2
        [Error] = 3
        Fatal = 4
    End Enum

    ' Test delegate and event
    Public Delegate Sub StatusChangedEventHandler(sender As Object, newStatus As String)

    Public Class StatusManager
        Public Event StatusChanged As StatusChangedEventHandler

        Private _status As String = "Idle"

        Public Property Status As String
            Get
                Return _status
            End Get
            Set(value As String)
                If _status <> value Then
                    _status = value
                    RaiseEvent StatusChanged(Me, _status)
                End If
            End Set
        End Property
    End Class
End Namespace
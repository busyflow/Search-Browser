param(
    [Parameter(Position=0)][string]$Command = "probe",
    [Parameter(Position=1)][string]$Arg = ""
)
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "search-bench", [System.IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect(2000)
    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($pipe)
    $writer.WriteLine("$Command $Arg".Trim())
    $reply = $reader.ReadLine()
    Write-Output $reply
} finally {
    $pipe.Dispose()
}

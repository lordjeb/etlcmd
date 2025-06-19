$root = $PSScriptRoot

& robocopy.exe "$root\etlcmd\bin\x64\Release" "$root\bld\etlcmd"
& robocopy.exe "$root\PsEtl.Cmdlet\bin\x64\Release\netstandard2.0" "$root\bld\PsEtl.Cmdlet"
Copy-Item -Path "$root\etlcmd\bin\x64\Release\Microsoft.Diagnostics.Tracing.TraceEvent.dll" -Destination "$root\bld\PsEtl.Cmdlet"

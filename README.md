# ETL Tools for Command-line

After building in Visual Studio 2022, you can run `prepareRelease.ps1` to copy required files into release directories under `bin\`

## PSEtl Cmdlet

PsEtl.Cmdlet is a powershell cmdlet that reads an .etl file and creates an object pipeline that can then be used with any of the standard powershell cmdlets.

```pwsh
Import-Module .\bin\PsEtl.Cmdlet\PsEtl.Cmdlet.dll
```

```pwsh
Remove-Module -Name PsEtl.Cmdlet
```

```pwsh
Import-Etl -Path [input_file]
```

```pwsh
Import-Etl -Path "D:\trace.etl" |
  Where-Object {
    $_.ProviderName -eq 'LordJebSoftware.ProviderName' -and
    $_.Id -gt 66327000 -and
    $_.Id -lt 67000000 -and
    $_.EventName -ne 'TheEventIDontWantToSee' } |
  Select-Object -First 20
```

## ETLCmd

EtlCmd is a command-line tool that processes etl files. It was my first attempt to write this kind of tool, and I tried to give it a number of options before I decided that using powershell and taking advantage of all of it's filtering options would be better.

### Usage

`etlcmd.exe [filter|version|help] [arguments]`

**filter** arguments:

| Argument | Description |
| -------- | ----------- |
| -i, --input | Required. ETL file from which to read trace data. |
| -o, --output | ETL file to which to write (modified) trace data. |
| -v, --verbose | Output records to console. |
| -q, --quiet | Quiet output. |
| -p, --include-provider | Include trace data from providers. |
| -e, --include-event | Include trace data matching event names. |
| -l, --level | (Default: 5) Include trace data of level or more important. |
| -r, --range | Include trace data between the provided ids. |
| -m, --match-payload | Include trace data whose payload contains match string. |
| -a, --activityid | Include trace data matching activity id. |

### Examples

Here are some examples of what you can do with etlcmd.exe...
 
1. Dump events from a file to command line:

```
etlcmd.exe filter -i <filename> -v
```

2. Write events to a new file:

```
etlcmd.exe filter -i <filename> -o <filename>
```

3. Filter events by provider name (-p) event name (-e) log level (-l) payload string (-m) 

```
etlcmd.exe filter -i <filename> -v -p LordJebSoftware.ProviderName -l 5 -e EVENT_NAME -m some_string_in_payload
```

4. Filter events by a range of event IDs

```
etlcmd.exe filter -i <filename> -v -r 200:400
```

So, if you have a gigantic file and want to get just the tracing for a set of events, you could do something like the following to find which IDs start and end the range you care about, and then to write those ranges to a new file...
 
```
PS D:\dev> etlcmd.exe filter -i input.etl -v -p LordJebSoftware.ProviderName -m event_identifier

ID           RelativeTime EventName                      Level         Payload
--           ------------ ---------                      -----         -------
77209         911054.5856 EVENT_START                    Informational event_identifier
126961        945135.1107 EVENT_END                      Informational event_identifier
169985 Events Filtered, 169987 Events Processed in 00:00:00.4898776
 
PS D:\dev> etlcmd.exe filter -i input.etl -o output.etl -r 77209:126961
120234 Events Filtered, 169987 Events Processed in 00:00:01.2464617
```

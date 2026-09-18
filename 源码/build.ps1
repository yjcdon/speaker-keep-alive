$ErrorActionPreference = 'Stop'
$speakerSource = $PSScriptRoot
$speakerCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $speakerCompiler)) {
    $speakerCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $speakerCompiler)) { throw '.NET Framework C# compiler not found.' }
if (-not (Test-Path -LiteralPath (Join-Path $speakerSource 'assets\speaker.ico'))) {
    & (Join-Path $speakerSource 'make-icons.ps1')
}
$speakerOutput = Join-Path (Split-Path -Parent $speakerSource) 'SpeakerKeepAlive.exe'
& $speakerCompiler /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /warnaserror+ /utf8output `
    '/reference:System.dll' '/reference:System.Core.dll' '/reference:System.Drawing.dll' `
    '/reference:System.Windows.Forms.dll' `
    "/win32manifest:$speakerSource\app.manifest" "/win32icon:$speakerSource\assets\speaker.ico" `
    "/resource:$speakerSource\assets\speaker.ico,speaker.ico" "/resource:$speakerSource\assets\speaker-paused.ico,speaker-paused.ico" `
    "/resource:$speakerSource\assets\Bootstrap-Icons-LICENSE.txt,Bootstrap-Icons-LICENSE.txt" `
    "/out:$speakerOutput" "$speakerSource\NativeAudio.cs" "$speakerSource\AudioEngine.cs" "$speakerSource\TargetDevice.cs" "$speakerSource\Program.cs"
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Get-Item -LiteralPath $speakerOutput | Select-Object FullName,Length

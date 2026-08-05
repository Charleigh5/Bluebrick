param(
    [string]$Text = "Hello, this is your BlueBrick voice update.",
    [string]$OutputPath = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\voice_reply.wav"
)

Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.SetOutputToWaveFile($OutputPath)
$synth.Speak($Text)
$synth.Dispose()

Write-Host "Voice file generated successfully at $OutputPath"

$regKey = "HKCU:\SOFTWARE\ViraInsight\BlueBrick\Settings"
$mode = (Get-ItemProperty -Path $regKey -Name "AssistantMode" -ErrorAction SilentlyContinue).AssistantMode
$hasKey = !([string]::IsNullOrEmpty((Get-ItemProperty -Path $regKey -Name "AssistantApiKey" -ErrorAction SilentlyContinue).AssistantApiKey))
Write-Output "AssistantMode=$mode"
Write-Output "AssistantApiKey present=$hasKey"

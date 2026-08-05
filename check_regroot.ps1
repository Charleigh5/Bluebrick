$asm = [System.Reflection.Assembly]::LoadFrom("C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Debug\BlueBrick.dll")
$type = $asm.GetType("BlueBrick.AppIdentity")
$field = $type.GetField("RegistryRoot", [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Public)
$val = $field.GetValue($null)
Write-Output $val

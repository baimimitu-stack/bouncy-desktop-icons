$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path "$root\dist" -Force | Out-Null
& $csc /nologo /codepage:65001 /target:exe /main:ChatTests "/out:$root\dist\ChatTests.exe" "/resource:$root\使用说明.txt,BouncyIcons.Manual.txt" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.Security.dll "$root\src\PetBrain.cs" "$root\src\PetSocial.cs" "$root\src\PetApp.cs" "$root\src\LauncherManager.cs" "$root\src\SharedData.cs" "$root\src\SpeechWindow.cs" "$root\src\ReactionLibrary.cs" "$root\src\ReactionWindow.cs" "$root\src\ChatService.cs" "$root\src\ChatWindow.cs" "$root\tests\ChatTests.cs"
if($LASTEXITCODE -ne 0){throw "聊天测试编译失败"}
& "$root\dist\ChatTests.exe" "$root\dist"
if($LASTEXITCODE -ne 0){throw "聊天测试失败"}

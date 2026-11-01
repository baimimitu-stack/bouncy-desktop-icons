param([switch]$Test)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $csc)){throw '需要 Windows 自带的 .NET Framework 4.x 编译器。'}
New-Item -ItemType Directory -Path (Join-Path $root 'dist\assets') -Force | Out-Null
if($Test){
  & $csc /nologo /codepage:65001 /target:exe "/out:$root\dist\CoreTests.exe" "$root\src\PetBrain.cs" "$root\src\PetSocial.cs" "$root\tests\CoreTests.cs"
  if($LASTEXITCODE -ne 0){throw '测试编译失败'}
  & "$root\dist\CoreTests.exe"
  if($LASTEXITCODE -ne 0){throw '算法测试失败'}
}
& {
  & $csc /nologo /codepage:65001 /target:winexe /optimize+ "/win32icon:$root\assets\app.ico" "/out:$root\dist\BouncyIcons.exe" "/resource:$root\使用说明.txt,BouncyIcons.Manual.txt" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.Security.dll "$root\src\PetBrain.cs" "$root\src\PetSocial.cs" "$root\src\PetApp.cs" "$root\src\LauncherManager.cs" "$root\src\SharedData.cs" "$root\src\SpeechWindow.cs" "$root\src\ReactionLibrary.cs" "$root\src\ReactionWindow.cs" "$root\src\ChatService.cs" "$root\src\ChatWindow.cs"
  if($LASTEXITCODE -ne 0){throw "程序编译失败"}
}
Copy-Item -LiteralPath "$root\assets\folder.gif" -Destination "$root\dist\assets\folder.gif"
Write-Output '构建完成：dist 中包含一个程序及 assets 文件夹。'

Copy-Item -LiteralPath "$root\使用说明.txt" -Destination "$root\dist\使用说明.txt"

Copy-Item -LiteralPath "$root\assets\app.ico" -Destination "$root\dist\assets\app.ico"

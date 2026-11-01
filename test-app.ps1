$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& "$root\build.ps1" -Test
& $csc /nologo /target:exe /main:AppSmoke "/out:$root\dist\AppSmoke.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$root\src\PetBrain.cs" "$root\src\PetApp.cs" "$root\src\LauncherManager.cs" "$root\tests\AppSmoke.cs"
if($LASTEXITCODE -ne 0){throw '集成测试编译失败'}
foreach($edition in @('lite','standard','pro')){
  & "$root\dist\AppSmoke.exe" "$root\dist" $edition
  if($LASTEXITCODE -ne 0){throw "$edition 原生界面集成测试失败"}
}

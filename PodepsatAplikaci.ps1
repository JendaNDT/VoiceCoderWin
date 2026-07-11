# Skript pro lokální podepsání CodePlanner.exe k obcházení SmartScreen varování.
# Spusťte tento skript v PowerShellu.

$exePath = Join-Path $PSScriptRoot "CodePlanner.exe"
if (-not (Test-Path $exePath)) {
    $exePath = Join-Path $PSScriptRoot "CodePlanner_v0.6_Windows\CodePlanner.exe"
}
if (-not (Test-Path $exePath)) {
    $exePath = Read-Host "Zadejte cestu k CodePlanner.exe"
}

if (-not (Test-Path $exePath)) {
    Write-Error "Soubor CodePlanner.exe nebyl nalezen!"
    exit 1
}

# 1. Hledáme existující lokální podpisový certifikát
$certFriendlyName = "CodePlanner Local Signing"
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.FriendlyName -eq $certFriendlyName } | Select-Object -First 1

if (-not $cert) {
    Write-Host "Vytvářím nový lokální podpisový certifikát..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Type CodeSigning -Subject "CN=CodePlannerLocal" -FriendlyName $certFriendlyName -CertStoreLocation "Cert:\CurrentUser\My"
} else {
    Write-Host "Nalezen stávající lokální certifikát." -ForegroundColor Green
}

# 2. Podepíšeme binárku
Write-Host "Podepisuji soubor $exePath ..." -ForegroundColor Cyan
$signResult = Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert

if ($signResult.Status -eq "Valid") {
    Write-Host "Aplikace byla úspěšně podepsána!" -ForegroundColor Green
} else {
    Write-Warning "Podepsání proběhlo, ale certifikát ještě není důvěryhodný (Stav: $($signResult.Status))."
    Write-Host ""
    Write-Host "K trvalému odstranění varování SmartScreen musíte certifikát jednorázově naimportovat jako důvěryhodný." -ForegroundColor Yellow
    Write-Host "Spusťte prosím PowerShell jako Administrátor a zadejte tyto dva příkazy:" -ForegroundColor Yellow
    Write-Host ""
    
    # Vygenerujeme instrukce k exportu a importu
    $tempCer = Join-Path $env:TEMP "CodePlannerLocal.cer"
    Write-Host "  Export-Certificate -Cert (Get-ChildItem Cert:\CurrentUser\My | Where-Object { `$_.FriendlyName -eq '$certFriendlyName' }) -FilePath '$tempCer'" -ForegroundColor White
    Write-Host "  Import-Certificate -FilePath '$tempCer' -CertStoreLocation 'Cert:\LocalMachine\Root'" -ForegroundColor White
    Write-Host "  Import-Certificate -FilePath '$tempCer' -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher'" -ForegroundColor White
    Write-Host ""
    Write-Host "Tím dáte svému systému vědět, že binárkám podepsaným vaším lokálním certifikátem může důvěřovat." -ForegroundColor Green
}
Write-Host ""
Write-Host "Stisknutím klávesy Enter zavřete..."
[void][System.Console]::ReadLine()

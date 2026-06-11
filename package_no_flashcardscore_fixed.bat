@echo off
chcp 65001 >nul
setlocal

set "SOURCE=D:\CSharp\TocflQuiz\FlashCards"
set "OUTPUT=D:\CSharp\flashcardshehe.zip"
set "PS1=%TEMP%\package_no_flashcardscore.ps1"

echo ==========================================
echo DONG GOI TOCFLQUIZ - BO QUA FlashCardsCore
echo ==========================================
echo Source: %SOURCE%
echo Output: %OUTPUT%
echo.

if not exist "%SOURCE%" (
    echo LOI: Khong tim thay thu muc SOURCE.
    echo Hay sua lai bien SOURCE trong file .bat
    pause
    exit /b 1
)

> "%PS1%" echo $source = '%SOURCE%'
>> "%PS1%" echo $output = '%OUTPUT%'
>> "%PS1%" echo $excludeNames = @('FlashCardsCore','bin','obj','.vs','.git','node_modules','dist','publish','.idea','.vscode')
>> "%PS1%" echo if (Test-Path $output) { Remove-Item $output -Force }
>> "%PS1%" echo $items = Get-ChildItem -LiteralPath $source -Force ^| Where-Object {
>> "%PS1%" echo     $name = $_.Name
>> "%PS1%" echo     -not ($excludeNames -contains $name)
>> "%PS1%" echo }
>> "%PS1%" echo if ($items.Count -eq 0) { throw 'Khong co file nao de dong goi.' }
>> "%PS1%" echo Compress-Archive -LiteralPath $items.FullName -DestinationPath $output -Force
>> "%PS1%" echo Write-Host ''
>> "%PS1%" echo Write-Host 'DONE:' $output

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"

echo.
echo DONG GOI XONG NEU KHONG CO LOI.
pause

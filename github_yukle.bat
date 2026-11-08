@echo off
chcp 65001 >nul
echo ===================================================
echo   YtLight v1.0.0 — GitHub'a Yukleme Araci
echo ===================================================
echo.
set /p REPO="GitHub Depo Linki [Varsayilan: https://github.com/csefacan/ytlight.git]: "
if "%REPO%"=="" set REPO=https://github.com/csefacan/ytlight.git

echo.
echo Git remote ayarlaniyor: %REPO%
git remote remove origin 2>nul
git remote add origin %REPO%

echo.
echo Kodlar GitHub'a gonderiliyor (main dali)...
git push -u origin main

echo.
echo Versiyon 1.0.0 etiketi gonderiliyor...
git push origin --tags

echo.
echo ===================================================
echo   Islem tamamlandi!
echo ===================================================
pause

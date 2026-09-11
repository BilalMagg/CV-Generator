@echo off

set "ROOT=C:\Users\Stronger\Documents\Container\Project\CV_Generator"
set "MONOLITH=%ROOT%\monolith"
set "FRONTEND=%ROOT%\frontend"

echo.
echo ========================================
echo       Starting CV Generator
echo ========================================
echo.

wt ^
  new-tab --title "CVG-Backend" -d "%MONOLITH%\backend" cmd /k "title CVG-Backend && dotnet run" ^
  ; new-tab --title "CVG-Gateway" -d "%MONOLITH%\gateway" cmd /k "title CVG-Gateway && dotnet run" ^
  ; new-tab --title "CVG-Agents" -d "%MONOLITH%\agents" cmd /k "title CVG-Agents && make run" ^
  ; new-tab --title "CVG-Frontend" -d "%FRONTEND%" cmd /k "title CVG-Frontend && ng serve"

echo.
echo All services started in Windows Terminal tabs!
echo.


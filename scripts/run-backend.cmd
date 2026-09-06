@echo off
setlocal
cd /d "%~dp0.."
start "Job Radar Backend" cmd /k "dotnet run --project artifacts\api-server-dotnet\JobRadar.Api.csproj"
endlocal

@echo off
rem One command. Runs run.ps1 without changing the machine's PowerShell execution policy.
rem   run.cmd          everything (provider, API, React client), opens UI and Swagger
rem   run.cmd server   server only (provider, API), opens Swagger
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*

@echo off
REM Kudu runs this script (from the OLD version's tools\ folder) before overwriting extension files.
REM Force-kill any running TempTrimmer process so HttpPlatformHandler releases the DLL lock.
taskkill /IM TempTrimmer.exe /F >nul 2>&1
REM Give the process a few seconds to exit and release all file handles.
ping -n 6 127.0.0.1 > nul

@echo off
REM Kudu runs this script (from the OLD version's tools\ folder) before overwriting extension files.
REM Place app_offline.htm in the extension root to signal ANCM to stop the out-of-process child,
REM which releases the DLL lock so the new files can be copied in.
FOR %%I IN ("%~dp0..") DO SET "EXT_ROOT=%%~fI"
echo . > "%EXT_ROOT%\app_offline.htm"
REM Give the child process up to 10 seconds to shut down cleanly.
ping -n 11 127.0.0.1 > nul

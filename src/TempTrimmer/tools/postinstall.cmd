@echo off
REM Kudu runs this script (from the NEW version's tools\ folder) after extension files are in place.
REM Remove app_offline.htm so ANCM starts the updated child process.
FOR %%I IN ("%~dp0..") DO SET "EXT_ROOT=%%~fI"
IF EXIST "%EXT_ROOT%\app_offline.htm" DEL /F /Q "%EXT_ROOT%\app_offline.htm"

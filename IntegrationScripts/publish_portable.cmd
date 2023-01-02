@echo off
set current=%date:~6,4%-%date:~3,2%-%date:~0,2%
set filename="C:\tmp\pix2dAv\*"
set filename2="C:\tmp\pix2d-%current%.zip"
echo %filename%
cd "C:\tmp"
if exist %filename% (
    "C:\Program Files\7-Zip\7z.exe" a %filename2% %filename%
    echo zip-finished
)

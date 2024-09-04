@echo off
setlocal enabledelayedexpansion

:: Get the directory where the batch script is located
set script_dir=%~dp0

:: Set the input and output folder to the script's directory
set input_folder=%script_dir%
set output_folder=%script_dir%processed

:: Create the output folder if it doesn't exist
if not exist "%output_folder%" (
    mkdir "%output_folder%"
)

:: Process each MP4 file in the input folder
for %%f in ("%input_folder%\*.mp4") do (
    set "filename=%%~nf"
    ffmpeg -i "%%f" -vf "format=yuv420p,scale=in_color_matrix=bt709:out_color_matrix=bt709" -c:v libx264 -crf 23 -preset medium -c:a copy -color_primaries 1 -color_trc 1 -colorspace 1 "%output_folder%\!filename!.mp4"
)

echo All videos processed.
pause
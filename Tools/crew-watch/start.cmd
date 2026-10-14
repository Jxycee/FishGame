@echo off
rem crew-watch: zero-token live view of the running Claude workflow. Double-click to start; close this window to stop.
start "" http://127.0.0.1:8777
python -I "%~dp0crew_watch.py" --port 8777

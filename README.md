# Xcopy

This project builds a single-file Windows 10 executable that uploads every file from
`C:\Users\Administrator\Desktop\合同台账\` (including subfolders) into a WebDAV folder
(`https://dav.gfwzb.com/`). The upload flattens the directory structure, and duplicate
names are overwritten by the last upload.

## Build

```bash
dotnet publish Xcopy/Xcopy.csproj -c Release -o dist
```

The single-file executable will be in `dist/` (for example, `dist/Xcopy.exe`).

## Run

Copy the generated `.exe` to the target machine and run it. It logs to `xcopy-log.txt`
next to the executable. After completing the copy, a confirmation dialog appears with
an automatic 3-second countdown close.

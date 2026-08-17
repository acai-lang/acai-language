[Setup]
AppId=084AFCBE-FE9B-4AE6-A376-0798203AF227
AppName=Acai
AppVersion=0.1.0
AppPublisher=DeepIgnite
DefaultDirName={autopf}\Acai
DefaultGroupName=Acai 0.1
OutputBaseFilename=Acai-windows-x64
Compression=lzma
SolidCompression=yes
ChangesEnvironment=yes
LicenseFile="LICENSE"

ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=acai.ico
UninstallDisplayIcon={app}\acai.ico
UninstallDisplayName=Acai
UninstallIconFile=acai.ico
AppCopyright=Copyright © 2026 DeepIgnite

[Files]
; 1. Grab the executable directly from your .NET build output folder
Source: "bin\Release\net10.0\win-x64\publish\Acai.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net10.0\win-x64\publish\Acai.pdb"; DestDir: "{app}"; Flags: ignoreversion

; 2. Grab your docs, demos, and libs directly from your root project folders
Source: "README.md"; DestDir: "{app}"; Flags: isreadme
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "acai.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "demo\*"; DestDir: "{app}\demo"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "metadata\*"; DestDir: "{app}\metadata"; Flags: ignoreversion recursesubdirs createallsubdirs
[Registry]
; This block automatically adds your Program Files folder to the system PATH variable
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath

[Code]
function NeedsAddPath(): Boolean;
var
  OldPath: String;
  AppPath: String;
  CleanOldPath: String;
  CleanAppPath: String;
begin
  // Look up the current User PATH from Environment
  if RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OldPath) then
  begin
    AppPath := ExpandConstant('{app}');
    
    // Step 1: Assign the original text values to our "Clean" string variables
    CleanAppPath := AppPath;
    CleanOldPath := OldPath;
    
    // Step 2: Run StringChange directly on the variables (it modifies them in place)
    StringChange(CleanAppPath, '\', '\\');
    StringChange(CleanOldPath, '\', '\\');
    
    // Step 3: Check if our clean path is already inside the system PATH
    if Pos(CleanAppPath, CleanOldPath) = 0 then
    begin
      Result := True;
    end
    else
    begin
      Result := False;
    end;
  end
  else
  begin
    // If the PATH variable doesn't exist at all, we definitely need to create it
    Result := True;
  end;
end;

[Setup]
AppName=Acai
AppVersion=0.1.0
AppPublisher=Acai Open Source
DefaultDirName={autopf}\Acai
DefaultGroupName=Acai 0.1
OutputBaseFilename=Acai_Stable0.1.0_Setup
Compression=lzma
SolidCompression=yes
ChangesEnvironment=yes

SetupIconFile=acai.ico
UninstallDisplayIcon={app}\acai.ico

[Files]
; 1. Grab the executable directly from your .NET build output folder
Source: "bin\Release\net10.0\win-x64\Acai.exe"; DestDir: "{app}"; Flags: ignoreversion

; 2. Grab your docs, demos, and libs directly from your root project folders
Source: "acai.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "demo\*"; DestDir: "{app}\demo"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "lib\*"; DestDir: "{app}\lib"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "src\*"; DestDir: "{app}\src"; Flags: ignoreversion recursesubdirs createallsubdirs
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

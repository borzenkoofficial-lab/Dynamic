#define AppName "Dynamic"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "artifacts"
#endif

[Setup]
AppId={{C7F6B45A-9D3A-4D69-92B8-7A9B3A64D1E8}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Dynamic
DefaultDirName={localappdata}\Programs\Dynamic
DefaultGroupName=Dynamic
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir={#OutputDir}
OutputBaseFilename=Dynamic-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Dynamic.exe
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные параметры:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Dynamic"; Filename: "{app}\Dynamic.exe"
Name: "{autodesktop}\Dynamic"; Filename: "{app}\Dynamic.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Dynamic.exe"; Description: "Запустить Dynamic"; Flags: postinstall nowait skipifsilent

#define MyAppName "Pick Up Online"
#define MyAppVersion "0.2.0"
#define MyAppPublisher "Pick Up Online"
#define PublishDir "..\PickUp.Windows\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{B8A5D7B2-5A45-4E6D-9C77-4F6C8B5E8A21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Pick Up Online
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=PickUpOnline-Setup-{#MyAppVersion}-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\PickUp.Windows.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\PickUp.Windows.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\PickUp.Windows.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Symbole:"; Flags: unchecked

[Run]
Filename: "{app}\PickUp.Windows.exe"; Description: "{#MyAppName} starten"; Flags: postinstall nowait skipifsilent

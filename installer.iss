#define MyAppName "ASI ERP"
#define MyAppVersion "2.0"
#define MyAppPublisher "Afrah Samiya Industries Pvt Ltd"
#define MyAppExeName "AsiErp.exe"

[Setup]
AppId={{5E7957B7-E8B6-4BAE-86D3-D6E4FB0F7F29}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=C:\Users\DELL\source\repos\AsiErp\publish\Installer
OutputBaseFilename=AsiErp_Setup_v2.0
SetupIconFile=C:\Users\DELL\source\repos\AsiErp\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "C:\Users\DELL\source\repos\AsiErp\publish\AsiErp_Standalone\AsiErp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\DELL\source\repos\AsiErp\publish\AsiErp_Standalone\firebase-key.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\DELL\source\repos\AsiErp\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

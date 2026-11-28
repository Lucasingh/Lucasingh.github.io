#define MyAppName "AovoCL 启动器"
#define MyAppVersion "0.2.6"
#define MyAppExeName "AovoCL.Desktop.exe"

[Setup]
AppId={{8F3E6A72-4B91-4D0E-9F5A-2C7B1D8E6A40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=AovoCL
DefaultDirName={localappdata}\Programs\AovoCL
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=AovoCL-Setup-{#MyAppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoDescription={#MyAppName}安装程序
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\dist\publish-v{#MyAppVersion}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent unchecked

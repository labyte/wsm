; WSM 安装包脚本（Inno Setup 6）
; 用法示例：
;   ISCC.exe installer\wsm.iss /DAppVersion=1.0.0 /DPublishDir=src\WSM.App.Modern\bin\Release\net8.0-windows\publish\win-x64

#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif

#ifndef PublishDir
  #define PublishDir "src\WSM.App.Modern\bin\Release\net8.0-windows\publish\win-x64"
#endif

#define AppName "WSM"
#define AppPublisher "WSM Team"
#define AppExeName "WSM.exe"

[Setup]
AppId={{6B0A6CE9-4DA0-4F40-B4D4-9274DBA5F355}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir=installer\Output
OutputBaseFilename=WSM-Setup-v{#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\src\WSM.App.Shared\Assets\wsm-logo.ico

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
; 将 dotnet publish 的输出全部打包到安装目录
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\WSM"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 WSM"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WSM"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 WSM"; Flags: nowait postinstall skipifsilent

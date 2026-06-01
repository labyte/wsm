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
OutputDir=Output
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
Name: "{group}\WSM"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\wsm-logo.ico"
Name: "{group}\卸载 WSM"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WSM"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\wsm-logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 WSM"; Flags: nowait postinstall skipifsilent

[Code]
const
  EXIT_NO_SERVICES = 0;
  EXIT_PARTIAL_FAILURE = 1;
  EXIT_ADMIN_REQUIRED = 2;
  EXIT_FATAL_ERROR = 255;

function GetAppExePath(): string;
begin
  Result := ExpandConstant('{app}\{#AppExeName}');
end;

function QueryManagedServiceCount(): Integer;
var
  ResultCode: Integer;
  ExePath: string;
begin
  Result := -1;
  ExePath := GetAppExePath();
  if not FileExists(ExePath) then
    Exit;

  if Exec(ExePath, '--pre-uninstall-check', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode
  else
    Result := EXIT_FATAL_ERROR;
end;

function UninstallAllManagedServices(): Integer;
var
  ResultCode: Integer;
  ExePath: string;
begin
  Result := EXIT_FATAL_ERROR;
  ExePath := GetAppExePath();
  if not FileExists(ExePath) then
    Exit;

  if Exec(ExePath, '--uninstall-all-services', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode
  else
    Result := EXIT_FATAL_ERROR;
end;

function InitializeUninstall(): Boolean;
var
  ServiceCount: Integer;
  UninstallResult: Integer;
  CountText: string;
  Prompt: string;
begin
  Result := True;
  ServiceCount := QueryManagedServiceCount();

  if ServiceCount <= EXIT_NO_SERVICES then
    Exit;

  if ServiceCount < 0 then
  begin
    MsgBox(
      '无法检测 WSM 托管服务状态，建议先手动打开 WSM 卸载全部服务后再卸载本程序。',
      mbInformation,
      MB_OK);
    Exit;
  end;

  if ServiceCount >= 254 then
    CountText := '254+'
  else
    CountText := IntToStr(ServiceCount);

  Prompt :=
    '检测到 ' + CountText + ' 个由 WSM 托管的 Windows 服务。' + #13#10 +
    '若直接卸载 WSM，可能因服务或日志文件占用导致删除失败。' + #13#10#13#10 +
    '是否现在卸载全部托管服务？' + #13#10 +
    '• 是：先卸载全部服务，再继续卸载 WSM' + #13#10 +
    '• 否：仍继续卸载 WSM（不卸载服务）' + #13#10 +
    '• 取消：中止本次卸载';

  case MsgBox(Prompt, mbConfirmation, MB_YESNOCANCEL or MB_DEFBUTTON1) of
    IDYES:
      begin
        UninstallResult := UninstallAllManagedServices();
        case UninstallResult of
          EXIT_NO_SERVICES:
            Exit;
          EXIT_PARTIAL_FAILURE:
            MsgBox(
              '部分托管服务卸载失败。请打开 WSM 在服务列表中手动卸载剩余服务后，再重新运行卸载程序。',
              mbError,
              MB_OK);
          EXIT_ADMIN_REQUIRED:
            MsgBox(
              '卸载托管服务需要管理员权限。请以管理员身份重新运行卸载程序，或先在 WSM 中手动卸载全部服务。',
              mbError,
              MB_OK);
          EXIT_FATAL_ERROR:
            MsgBox(
              '卸载托管服务时发生错误。请打开 WSM 手动卸载全部服务后再重试。',
              mbError,
              MB_OK);
        end;
      end;
    IDNO:
      MsgBox(
        '将跳过卸载托管服务。若后续删除文件失败，请重新运行卸载并选择“是”，或先在 WSM 中手动卸载全部服务。',
        mbInformation,
        MB_OK);
    IDCANCEL:
      Result := False;
  end;
end;

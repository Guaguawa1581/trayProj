; BackendTray 安裝程式（同一支 Setup.exe 也是更新包）
;
; - AppId 固定不變：再次執行較新版本的 Setup.exe 就會覆蓋更新。
; - 程式裝在 Program Files，設定與 log 在 ProgramData\BackendTray，更新時不會動到。
; - 不附帶 .NET 執行環境，只檢查是否已安裝。
; - 服務的建立與設定由 BackendTray.Service.exe 的 install / uninstall / stop-for-update 指令處理。
;
; 由 build\build.ps1 呼叫：ISCC.exe /DAppVersion=1.2.3 BackendTray.iss
; 靜默更新：BackendTray-Setup-1.2.3.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppName "BackendTray"
#define PublishDir "..\..\artifacts\publish"
#define ServiceExe "{app}\service\BackendTray.Service.exe"
#define ManagerExe "{app}\manager\BackendTray.Manager.exe"

[Setup]
; 這個 GUID 一旦發佈就永遠不能改，否則無法更新已安裝的版本。
AppId={{0119DF24-0EDD-4B8F-AF83-4674B1898D22}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\..\artifacts\installer
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; 由 [Code] 透過 stop-for-update 關閉服務與管理介面，不使用 Restart Manager。
CloseApplications=no
UninstallDisplayIcon={#ManagerExe}
UninstallDisplayName={#AppName}

[InstallDelete]
; Vue 的檔名含 hash，先清掉舊版檔案，避免越積越多。
Type: filesandordirs; Name: "{app}\wwwroot"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName} 管理介面"; Filename: "{#ManagerExe}"

[Run]
Filename: "{#ManagerExe}"; Description: "開啟 {#AppName} 管理介面"; Flags: nowait postinstall

[UninstallRun]
Filename: "{#ServiceExe}"; Parameters: "uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveService"

[Code]
const
  RuntimeDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';

function HasSharedFramework(const Name: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\' + Name + '\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

{ 只檢查 .NET 10 執行環境是否已安裝，不自動下載。 }
function InitializeSetup(): Boolean;
var
  Missing: String;
  ErrorCode: Integer;
begin
  Result := True;
  Missing := '';
  if not HasSharedFramework('Microsoft.AspNetCore.App') then
    Missing := Missing + #13#10 + '  - ASP.NET Core Runtime 10 (x64)';
  if not HasSharedFramework('Microsoft.WindowsDesktop.App') then
    Missing := Missing + #13#10 + '  - .NET Desktop Runtime 10 (x64)';

  if Missing <> '' then
  begin
    Result := False;
    if SuppressibleMsgBox('這台電腦缺少以下執行環境：' + Missing + #13#10#13#10 +
      '請先安裝後再執行本安裝程式。要開啟下載頁面嗎？', mbError, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', RuntimeDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

{ 更新前：用舊版的服務程式停止服務並關閉所有管理介面，檔案才能被取代。 }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{#ServiceExe}')) then
  begin
    if not Exec(ExpandConstant('{#ServiceExe}'), 'stop-for-update', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
      or (ResultCode <> 0) then
      Result := '無法停止目前執行中的 BackendTray 服務（代碼 ' + IntToStr(ResultCode) + '）。' + #13#10 +
        '請先手動停止服務後再試一次。';
  end;
end;

{ 檔案複製完成後：建立或更新 Windows 服務，並視情況啟動。 }
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := '正在設定 Windows 服務…';
    if not Exec(ExpandConstant('{#ServiceExe}'), 'install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
      or (ResultCode <> 0) then
      SuppressibleMsgBox('設定 Windows 服務失敗（代碼 ' + IntToStr(ResultCode) + '）。' + #13#10 +
        '詳細資訊請看 ' + ExpandConstant('{commonappdata}\BackendTray\logs') + ' 裡的 setup 記錄檔。',
        mbError, MB_OK, IDOK);
  end;
end;

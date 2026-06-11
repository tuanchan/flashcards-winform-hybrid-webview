[Setup]
#define MyAppVersion "1.1.1"
#define MyPublishDir "bin\Publish"
AppName=FlashCards
AppVersion={#MyAppVersion}
AppPublisher=TRAN ANH TUAN
AppId={{B7DD7714-8B1B-4A4C-9C52-4C05038D9D3A}
DefaultDirName={autopf}\FlashCards
DefaultGroupName=FlashCards
OutputDir=InstallerOutput
OutputBaseFilename=FlashCards_Setup_v{#MyAppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\FlashCards.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UsePreviousAppDir=yes
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Dataset\*"

[Icons]
Name: "{group}\FlashCards"; Filename: "{app}\FlashCards.exe"
Name: "{commondesktop}\FlashCards"; Filename: "{app}\FlashCards.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FlashCards.exe"; Description: "{cm:LaunchProgram,FlashCards}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  DownloadPage: TDownloadWizardPage;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorCode: Integer;
  IsWebView2Installed: Boolean;
begin
  Result := True;

  if CurPageID = wpReady then
  begin
    IsWebView2Installed :=
      RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
      RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
      RegKeyExists(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');

    if not IsWebView2Installed then
    begin
      DownloadPage.Clear;
      DownloadPage.Add('https://go.microsoft.com/fwlink/p/?LinkId=2124703', 'WebView2Setup.exe', '');
      DownloadPage.Show;
      try
        try
          DownloadPage.Download;
          Exec(ExpandConstant('{tmp}\WebView2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
        except
          MsgBox('Cannot download or install WebView2 Runtime automatically. Please check your internet connection.', mbError, MB_OK);
        end;
      finally
        DownloadPage.Hide;
      end;
    end;
  end;
end;

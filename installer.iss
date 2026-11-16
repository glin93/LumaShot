#ifndef AppSource
  #define AppSource SourcePath
#endif
[Setup]
AppId={{E5808125-1FD8-4B6C-8568-03B55559D677}
AppName=LumaShot
AppVersion=0.1.2
AppPublisher=glin93
AppPublisherURL=https://github.com/glin93/LumaShot
AppSupportURL=https://github.com/glin93/LumaShot/issues
AppUpdatesURL=https://github.com/glin93/LumaShot/releases
DefaultDirName={localappdata}\Programs\LumaShot
DefaultGroupName=LumaShot
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\release
OutputBaseFilename=LumaShot-0.1.2-Setup-x64
SetupIconFile={#AppSource}\LumaShot.ico
UninstallDisplayIcon={app}\LumaShot.exe
LicenseFile={#AppSource}\LICENSE
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\LumaShot-Running
CloseApplications=no
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "default"; Description: "标准安装"
Name: "custom"; Description: "自定义安装"; Flags: iscustom

[Components]
Name: "main"; Description: "LumaShot（截图、录屏、历史、同步、OpenAI / Gemini Key）"; Types: default custom; Flags: fixed
Name: "chatgpt"; Description: "ChatGPT 登录实验组件（非商业许可，可选）"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Files]
Source: "{#AppSource}\backend\siwc\LICENSE"; DestName: "chatgpt-license.txt"; Flags: dontcopy
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main; Excludes: "\LumaShot.*.exe,\backend\siwc\*,\engine\ShareX\*,\tools\*,\.git\*,\.build-cache\*,\Data\*,\test-output\*,*.dpapi,*.tmp"
Source: "{#AppSource}\backend\siwc\*"; DestDir: "{app}\backend\siwc"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: chatgpt

[Icons]
Name: "{autoprograms}\LumaShot"; Filename: "{app}\LumaShot.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\LumaShot"; Filename: "{app}\LumaShot.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LumaShot.exe"; Description: "启动 LumaShot"; Flags: nowait postinstall skipifsilent

[Code]
var
  ChatGPTPage: TWizardPage;
  LicenseMemo: TNewMemo;
  AgreeChatGPT: TNewCheckBox;
  LicenseText: AnsiString;

procedure InitializeWizard;
begin
  ChatGPTPage := CreateCustomPage(wpSelectComponents, 'ChatGPT 登录组件许可',
    '此组件仅供个人学习、实验和无预期商业用途的开发；商业使用需要另获 OpenAI 协议。');
  LicenseMemo := TNewMemo.Create(ChatGPTPage);
  LicenseMemo.Parent := ChatGPTPage.Surface;
  LicenseMemo.SetBounds(0, 0, ChatGPTPage.SurfaceWidth, ChatGPTPage.SurfaceHeight - ScaleY(48));
  LicenseMemo.ReadOnly := True;
  LicenseMemo.ScrollBars := ssVertical;
  ExtractTemporaryFile('chatgpt-license.txt');
  if LoadStringFromFile(ExpandConstant('{tmp}\chatgpt-license.txt'), LicenseText) then
    LicenseMemo.Text := UTF8Decode(LicenseText);
  AgreeChatGPT := TNewCheckBox.Create(ChatGPTPage);
  AgreeChatGPT.Parent := ChatGPTPage.Surface;
  AgreeChatGPT.SetBounds(0, ChatGPTPage.SurfaceHeight - ScaleY(36), ChatGPTPage.SurfaceWidth, ScaleY(32));
  AgreeChatGPT.Caption := '我已阅读并接受该非商业许可证，选择安装此组件';
  AgreeChatGPT.Checked := False;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = ChatGPTPage.ID) and not WizardIsComponentSelected('chatgpt');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if not WizardSilent and (CurPageID = ChatGPTPage.ID) and not AgreeChatGPT.Checked then begin
    MsgBox('请接受组件许可，或返回上一步取消勾选 ChatGPT 登录组件。', mbInformation, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if WizardIsComponentSelected('chatgpt') then begin
    if WizardSilent then begin
      if ExpandConstant('{param:ACCEPTCHATGPTLICENSE|0}') <> '1' then
        Result := '静默安装此可选组件需要明确传入 /ACCEPTCHATGPTLICENSE=1 以接受其非商业许可证。';
    end else if not AgreeChatGPT.Checked then
      Result := '尚未接受 ChatGPT 登录组件的非商业许可证。';
  end;
end;

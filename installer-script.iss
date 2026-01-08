; EditFarmer Installer Script
; Created with Inno Setup
; High-quality icon (no compression) - EditFarmer branding

#define MyAppName "EditFarmer"
#define MyAppVersion "1.3"
#define MyAppPublisher "Const and Props LLC"
#define MyAppURL "https://edit-farmer-c57dcf858c81.herokuapp.com"
#define MyAppExeName "CarrotDownload.Maui.exe"
#define DotNetVersion "8.0"

[Setup]
; App Information
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
CloseApplications=no
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output
OutputDir=C:\Users\user\Desktop
OutputBaseFilename=EditFarmer Setup
SetupIconFile=InstallerAssets\carrot_square.ico
WizardImageFile=InstallerAssets\wizard_side.bmp
WizardSmallImageFile=InstallerAssets\wizard_small.bmp
WizardImageStretch=no
Compression=lzma2/max
SolidCompression=yes

; Windows Version
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

; Mutex to prevent ReadProcessMemory errors during uninstall
AppMutex=EditFarmer_Mutex_A1B2C3D4

; UI
WizardStyle=modern
DisableWelcomePage=no
LicenseFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Application Files
Source: "CarrotDownload.Maui\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; EditFarmer Icon - High quality, no compression
Source: "InstallerAssets\carrot_square.ico"; DestDir: "{app}"; DestName: "carrot-icon.ico"; Flags: ignoreversion
; Bundle .NET Runtime Installer
Source: "InstallerAssets\dotnet-runtime.exe"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\carrot-icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\carrot-icon.ico"

[Run]
; Run the app after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNetInstalled: Boolean;
var
  ResultCode: Integer;
begin
  // Check if .NET 8 Desktop Runtime is installed
  Result := Exec('cmd.exe', '/c dotnet --list-runtimes 2>nul | findstr "Microsoft.WindowsDesktop.App 8."', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  DotNetInstaller: String;
begin
  Result := '';
  
  if not IsDotNetInstalled then
  begin
      // Extract and install .NET 8 Desktop Runtime SILENTLY and MANDATORY
      ExtractTemporaryFile('dotnet-runtime.exe');
      DotNetInstaller := ExpandConstant('{tmp}\dotnet-runtime.exe');
      
      // Run installer with /install /quiet /norestart parameters using ShellExec for better UAC handling
      if not ShellExec('', DotNetInstaller, '/install /quiet /norestart', ExpandConstant('{tmp}'), SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        MsgBox('Failed to automatically install .NET 8 Desktop Runtime.' + #13#10 +
               'Please install it manually to use this application.', mbError, MB_OK);
      end;
  end;
end;

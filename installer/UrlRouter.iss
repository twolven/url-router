#define AppName "URL Router"
#define AppVersion "1.0.0"
#define AppPublisher "twolven"
#define AppExeName "UrlRouter.exe"

[Setup]
AppId={{E8269C05-E71C-47E5-BC30-B7DE51D85184}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\UrlRouter
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=UrlRouter-Setup-x64
SetupIconFile=..\assets\url-router.ico
UninstallDisplayIcon={app}\url-router.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Local URL browser router
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}

[Files]
Source: "..\publish\UrlRouter.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\url-router.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\config\rules.example.json"; DestDir: "{app}"; DestName: "rules.json"; Flags: onlyifdoesntexist
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Edit URL Router rules"; Filename: "notepad.exe"; Parameters: """{app}\rules.json"""; IconFilename: "{app}\url-router.ico"
Name: "{group}\URL Router documentation"; Filename: "{app}\README.md"; IconFilename: "{app}\url-router.ico"

[Registry]
Root: HKCU; Subkey: "Software\UrlRouter"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol"; ValueType: string; ValueName: ""; ValueData: "URL Router"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol"; ValueType: dword; ValueName: "EditFlags"; ValueData: "2"
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol"; ValueType: string; ValueName: "FriendlyTypeName"; ValueData: "Web URL"
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\url-router.ico,0"
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKCU; Subkey: "Software\Classes\UrlRouter.Protocol\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

Root: HKCU; Subkey: "Software\UrlRouter\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Routes selected URLs to configurable browsers and profiles."
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\url-router.ico,0"
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\url-router.ico,0"
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities\StartMenu"; ValueType: string; ValueName: "StartMenuInternet"; ValueData: "UrlRouter"
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities\URLAssociations"; ValueType: string; ValueName: "http"; ValueData: "UrlRouter.Protocol"
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities\URLAssociations"; ValueType: string; ValueName: "https"; ValueData: "UrlRouter.Protocol"
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKCU; Subkey: "Software\UrlRouter\Capabilities\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "UrlRouter"; ValueData: "Software\UrlRouter\Capabilities"; Flags: uninsdeletevalue

[Run]
Filename: "notepad.exe"; Parameters: """{app}\rules.json"""; Description: "Edit browser and routing rules"; Flags: postinstall nowait skipifsilent
Filename: "ms-settings:defaultapps?registeredAppUser=UrlRouter"; Description: "Choose URL Router for HTTP and HTTPS defaults"; Flags: shellexec postinstall nowait skipifsilent

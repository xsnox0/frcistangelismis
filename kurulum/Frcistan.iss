; Frcistan kurulum betiği (Inno Setup 6)
; Yayımlama profili (src/Frcistan/Properties/PublishProfiles/Kurulum.pubxml) bu dosyayı otomatik derler.
; Elle derlemek için: önce yayımlayın, sonra bu dosyayı Inno Setup'ta açıp Compile'a basın.

#ifndef Surum
  #define Surum "1.0.0"
#endif
#define KisaSurum Copy(Surum, 1, RPos(".", Surum) - 1)

[Setup]
AppId={{99892FE0-F2BF-4DE5-9A17-081618C794A7}
AppName=Frcistan
AppVersion={#Surum}
AppVerName=Frcistan {#KisaSurum}
AppPublisher=İlyas Enes Sepetci
AppCopyright=© 2026 İlyas Enes Sepetci
VersionInfoVersion={#Surum}
VersionInfoDescription=Frcistan - Robot Parçası Stok Yönetimi Kurulumu
DefaultDirName={autopf}\Frcistan
DefaultGroupName=Frcistan
DisableProgramGroupPage=yes
; Yönetici izni gerekmez; kullanıcı isterse tüm kullanıcılar için kurabilir
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\kurulum-cikti
OutputBaseFilename=Frcistan-{#KisaSurum}-Kurulum
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Frcistan\Assets\frcistan.ico
WizardImageFile=sihirbaz-100.bmp,sihirbaz-125.bmp,sihirbaz-150.bmp,sihirbaz-175.bmp,sihirbaz-200.bmp,sihirbaz-250.bmp
WizardSmallImageFile=sihirbaz-kucuk-100.bmp,sihirbaz-kucuk-125.bmp,sihirbaz-kucuk-150.bmp,sihirbaz-kucuk-175.bmp,sihirbaz-kucuk-200.bmp,sihirbaz-kucuk-250.bmp
DisableWelcomePage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\Frcistan.exe
UninstallDisplayName=Frcistan {#KisaSurum}
CloseApplications=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Messages]
turkish.WelcomeLabel2=Bu sihirbaz bilgisayarınıza [name/ver] programını kuracak.%n%nFrcistan, FRC ve FTC robot parçası bayileri için hazırlanmış stok yönetim programıdır. Ek bir program (XAMPP, .NET vb.) kurmanıza gerek yoktur.%n%nDevam etmeden önce diğer uygulamaları kapatmanız önerilir.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\yayin\Frcistan.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; DestName: "BENIOKU.md"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Frcistan"; Filename: "{app}\Frcistan.exe"; Comment: "Robot parçası stok yönetimi"
Name: "{autodesktop}\Frcistan"; Filename: "{app}\Frcistan.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Frcistan.exe"; Description: "{cm:LaunchProgram,Frcistan}"; Flags: nowait postinstall skipifsilent

; Not: Veriler (%LOCALAPPDATA%\FrcistanStok) programı kaldırınca silinmez; böylece yeniden kurulumda kaybolmaz.

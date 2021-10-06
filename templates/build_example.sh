# Runtime Identifier: win-x64, linux-x64, osx-x64 (see https://docs.microsoft.com/en-us/dotnet/core/rid-catalog for details)
RID=<REQUIRED_RID> 
DOTNET_CLI_TELEMETRY_OPTOUT=1

git clone https://github.com/hyperion-cs/dhaf.git
cd dhaf

dotnet restore src/Dhaf.sln

# build dhaf core, node, cli

dotnet publish src/Dhaf.Core/Dhaf.Core.csproj  --configuration Release --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID/core" /p:DebugType=None /p:DebugSymbols=false

dotnet publish src/Dhaf.Node/Dhaf.Node.csproj --configuration Release --no-dependencies --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID" -p:PublishSingleFile=true --self-contained false /p:DebugType=None /p:DebugSymbols=false

dotnet publish src/Dhaf.CLI/Dhaf.CLI.csproj --configuration Release --no-dependencies --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID" -p:PublishSingleFile=true --self-contained false /p:DebugType=None /p:DebugSymbols=false

# build core extensions

dotnet publish src/Dhaf.HealthCheckers.Web/Dhaf.HealthCheckers.Web.csproj --configuration Release --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID/ext/hc/web" /p:DebugType=None /p:DebugSymbols=false
dotnet publish src/Dhaf.HealthCheckers.Exec/Dhaf.HealthCheckers.Exec.csproj --configuration Release --no-restore -nowarn:CS1998 -r $RID -o "bin/$RID/ext/hc/exec" /p:DebugType=None /p:DebugSymbols=false

dotnet publish src/Dhaf.Switchers.Cloudflare/Dhaf.Switchers.Cloudflare.csproj --configuration Release --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID/ext/sw/cloudflare" /p:DebugType=None /p:DebugSymbols=false
dotnet publish src/Dhaf.Switchers.Exec/Dhaf.Switchers.Exec.csproj --configuration Release --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID/ext/sw/exec" /p:DebugType=None /p:DebugSymbols=false

dotnet publish src/Dhaf.Notifiers.Email/Dhaf.Notifiers.Email.csproj --configuration Release --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID/ext/ntf/email" /p:DebugType=None /p:DebugSymbols=false
dotnet publish src/Dhaf.Notifiers.Telegram/Dhaf.Notifiers.Telegram.csproj --configuration Release --no-restore -nowarn:CS1998 -r "$RID" -o "bin/$RID/ext/ntf/tg" /p:DebugType=None /p:DebugSymbols=false

mkdir -p $RID-artifacts
mkdir -p $RID-artifacts/libs
mkdir -p $RID-artifacts/extensions/health-checkers/web
mkdir -p $RID-artifacts/extensions/health-checkers/exec
mkdir -p $RID-artifacts/extensions/switchers/cloudflare
mkdir -p $RID-artifacts/extensions/switchers/exec
mkdir -p $RID-artifacts/extensions/notifiers/email
mkdir -p $RID-artifacts/extensions/notifiers/tg
mv ./bin/$RID/Dhaf.Node $RID-artifacts/dhaf.node
mv ./bin/$RID/Dhaf.CLI $RID-artifacts/dhaf.cli
mv ./bin/$RID/appsettings.json ./bin/$RID/nlog.config $RID-artifacts
mv ./bin/$RID/core/* $RID-artifacts/libs
mv ./bin/$RID/ext/hc/web/* $RID-artifacts/extensions/health-checkers/web
mv ./bin/$RID/ext/hc/exec/* $RID-artifacts/extensions/health-checkers/exec
mv ./bin/$RID/ext/sw/cloudflare/* $RID-artifacts/extensions/switchers/cloudflare
mv ./bin/$RID/ext/sw/exec/* $RID-artifacts/extensions/switchers/exec
mv ./bin/$RID/ext/ntf/email/* $RID-artifacts/extensions/notifiers/email
mv ./bin/$RID/ext/ntf/tg/* $RID-artifacts/extensions/notifiers/tg
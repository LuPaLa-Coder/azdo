dotnet publish src/DevOpsCli -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o publish/Mac/ -p:UseAppHost=true

dotnet publish src/DevOpsCli -c Release -r linux-x64  --self-contained true -p:PublishSingleFile=true -o publish/Linux/ -p:UseAppHost=true
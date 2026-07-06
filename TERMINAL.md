# Comandos de Terminal — UltraNote

Lista de comandos de terminal, úteis para o desenvolvimento do app

## Publicar container docker
```
docker run --rm --network edge --dns 8.8.8.8 \
  -v /share/Container/ultranote-app:/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish UltraNote.Web/UltraNote.Web.csproj -c Release -o /src/_out_web
```

## Iniciar servidor API
```
cd C:\Users\mvsiq\Downloads\apps\UltraNote2\UltraNote.Api
dotnet run --urls http://127.0.0.1:5099
```

## Iniciar servidor WEB
```
cd C:\Users\mvsiq\Downloads\apps\UltraNote2\UltraNote.Web
dotnet run --urls http://127.0.0.1:5200
```

FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-windowsservercore-ltsc2022 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 9696
RUN mkdir C:\temp && pwsh -Command "Invoke-WebRequest -Method 'GET' -uri 'https://dl.smallstep.com/gh-release/cli/docs-cli-install/v0.24.4/step_windows_0.24.4_amd64.zip' -Outfile 'C:\temp\step_windows_0.24.4_amd64.zip'" && tar -oxzf C:\temp\step_windows_0.24.4_amd64.zip -C "C:\Program Files" && rmdir /s /q C:\temp
USER ContainerAdministrator
RUN setx /M PATH "%PATH%;C:\Program Files\step_0.24.4\bin"
USER ContainerUser

# define build and copy required source files
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-windowsservercore-ltsc2022 AS build
WORKDIR /src
COPY ./certify/src ./certify/src
COPY ./certify-plugins/src ./certify-plugins/src
COPY ./certify-internal/src/Certify.Plugins ./certify-internal/src/Certify.Plugins
COPY ./libs/anvil ./libs/anvil

# build and publish (as Debug mode) to /app/publish
FROM build AS publish
RUN dotnet publish ./certify/src/Certify.Tests/Certify.Core.Tests.Unit/Certify.Core.Tests.Unit.csproj -f net9.0 -c Debug -o /app/publish
RUN dotnet publish ./certify-internal/src/Certify.Plugins/Plugins.All/Plugins.All.csproj -f net9.0 -c Debug -o /app/publish/plugins
COPY ./libs/Posh-ACME/Posh-ACME /app/publish/Scripts/DNS/PoshACME

# copy build from /app/publish in sdk image to final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# run the service, alternatively we could runs tests etc
ENTRYPOINT ["dotnet", "test", "Certify.Core.Tests.Unit.dll", "-f", "net9.0"]

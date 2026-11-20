FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Amanet.API/Amanet.API.csproj --locked-mode
RUN dotnet publish Amanet.API/Amanet.API.csproj --configuration Release --no-restore --output /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
RUN mkdir /app/private-uploads && chown "$APP_UID" /app/private-uploads
USER $APP_UID
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "Amanet.API.dll"]

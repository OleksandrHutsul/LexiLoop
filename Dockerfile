FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["LexiLoop.csproj", "./"]
RUN dotnet restore "LexiLoop.csproj"
COPY . .
RUN dotnet publish "LexiLoop.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LexiLoop.dll"]

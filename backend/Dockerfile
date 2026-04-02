FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
# Azure App Service Linux uses WEBSITES_PORT (default 8080)
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["LibraMS.Api/LibraMS.Api.csproj", "LibraMS.Api/"]
RUN dotnet restore "LibraMS.Api/LibraMS.Api.csproj"
COPY . .
WORKDIR "/src/LibraMS.Api"
RUN dotnet build  "LibraMS.Api.csproj" -c Release -o /app/build
RUN dotnet publish "LibraMS.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Must listen on 0.0.0.0 so Azure App Service routing can reach it
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "LibraMS.Api.dll"]

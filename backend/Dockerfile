# Stage 1: base runtime image — lightweight Alpine with only the ASP.NET runtime (no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
# Azure App Service Linux uses WEBSITES_PORT (default 8080)
EXPOSE 8080

# Stage 2: build — uses the full .NET SDK to restore, build, and publish the app
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
# Copy only the project file first to leverage Docker layer caching for restore
COPY ["LibraMS.Api/LibraMS.Api.csproj", "LibraMS.Api/"]
RUN dotnet restore "LibraMS.Api/LibraMS.Api.csproj"
# Copy the rest of the source code
COPY . .
WORKDIR "/src/LibraMS.Api"
RUN dotnet build "LibraMS.Api.csproj" -c Release -o /app/build
# Publish without a native host wrapper (UseAppHost=false) since we use the dotnet entrypoint
RUN dotnet publish "LibraMS.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: final — copy published output into the slim runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Must listen on 0.0.0.0 so Azure App Service routing can reach it
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "LibraMS.Api.dll"]

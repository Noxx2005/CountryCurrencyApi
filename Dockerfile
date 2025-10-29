# Step 1: Base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Step 2: Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "./CountryCurrencyApi.csproj"
RUN dotnet publish "./CountryCurrencyApi.csproj" -c Release -o /app/publish

# Step 3: Final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CountryCurrencyApi.dll"]

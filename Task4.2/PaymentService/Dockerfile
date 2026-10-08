FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["PaymentService.csproj", "."]
RUN dotnet restore "PaymentService.csproj"

COPY . .
RUN dotnet publish "PaymentService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# curl is needed for the docker-compose healthcheck (image has no curl/wget).
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:3000
EXPOSE 3000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.dll"]
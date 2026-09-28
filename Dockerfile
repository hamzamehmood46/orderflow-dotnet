FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY OrderFlow.sln ./
COPY src/OrderFlow.Core/OrderFlow.Core.csproj src/OrderFlow.Core/
COPY src/OrderFlow.Infrastructure/OrderFlow.Infrastructure.csproj src/OrderFlow.Infrastructure/
COPY src/OrderFlow.Api/OrderFlow.Api.csproj src/OrderFlow.Api/
COPY tests/OrderFlow.Tests/OrderFlow.Tests.csproj tests/OrderFlow.Tests/
RUN dotnet restore
COPY . .
RUN dotnet publish src/OrderFlow.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]

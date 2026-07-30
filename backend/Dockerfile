FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BookSpace.sln ./
COPY src/BookSpace.Domain/BookSpace.Domain.csproj src/BookSpace.Domain/
COPY src/BookSpace.Application/BookSpace.Application.csproj src/BookSpace.Application/
COPY src/BookSpace.Infrastructure/BookSpace.Infrastructure.csproj src/BookSpace.Infrastructure/
COPY src/BookSpace.Api/BookSpace.Api.csproj src/BookSpace.Api/
COPY tests/BookSpace.UnitTests/BookSpace.UnitTests.csproj tests/BookSpace.UnitTests/
COPY tests/BookSpace.IntegrationTests/BookSpace.IntegrationTests.csproj tests/BookSpace.IntegrationTests/
RUN dotnet restore BookSpace.sln

COPY . .
RUN dotnet publish src/BookSpace.Api/BookSpace.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data && chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://0.0.0.0:5080
ENV BOOKSPACE_ConnectionStrings__DefaultConnection="Data Source=/app/data/bookspace.db"
EXPOSE 5080

ENTRYPOINT ["dotnet", "BookSpace.Api.dll"]

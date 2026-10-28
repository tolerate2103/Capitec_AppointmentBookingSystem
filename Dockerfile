# Build the Angular SPA.
FROM node:22-alpine AS client-build
WORKDIR /src/client
COPY capitec_appointmentbookingsystem.client/package*.json ./
RUN npm ci
COPY capitec_appointmentbookingsystem.client/ ./
RUN npm run build

# Restore and publish the .NET 9 API.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS server-build
WORKDIR /src
COPY Data_Tier/Data_Tier.csproj Data_Tier/
COPY Application_Tier/Application_Tier.csproj Application_Tier/
COPY AppointmentAPI/AppointmentAPI.csproj AppointmentAPI/
RUN dotnet restore AppointmentAPI/AppointmentAPI.csproj
COPY Data_Tier/ Data_Tier/
COPY Application_Tier/ Application_Tier/
COPY AppointmentAPI/ AppointmentAPI/
RUN dotnet publish AppointmentAPI/AppointmentAPI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false
COPY --from=client-build /src/client/dist/capitec_appointmentbookingsystem.client/browser/ /app/publish/wwwroot/

# Small runtime image; the database runs as a separate service.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=server-build /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "AppointmentAPI.dll"]

# Build no contexto da RAIZ do repositório (precisa enxergar motor-regras/ e
# nucleo/ como irmãos) — ver docs/publicacao-easypanel.md.
#
# Nucleo.Api é o único serviço do backend .NET que roda fora do servidor do
# cliente: regras puras (precificação/comissão/crédito/fiscal), sem
# System.Data.OleDb nem nenhuma dependência de Windows — confirmado antes de
# containerizar (26/08/2026). O Integrador (console) NUNCA vai para cá: ele
# precisa do VFPOLEDB e da pasta de DBFs, que só existem no servidor do
# cliente — ver docs/instalacao-console-cliente.md.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY motor-regras/MotorRegras/ ./motor-regras/MotorRegras/
COPY nucleo/Nucleo.Api/ ./nucleo/Nucleo.Api/
WORKDIR /src/nucleo/Nucleo.Api
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5060
EXPOSE 5060
ENTRYPOINT ["dotnet", "Nucleo.Api.dll"]

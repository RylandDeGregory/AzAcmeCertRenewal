FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6 AS build

ARG TARGETARCH
ARG VERSION=0.0.0
WORKDIR /src

COPY --link src/AzAcmeCertRenewal/Directory.Packages.props src/AzAcmeCertRenewal/
COPY --link src/AzAcmeCertRenewal/AzAcmeCertRenewal.csproj src/AzAcmeCertRenewal/
COPY --link src/Acmebot.Acme/Acmebot.Acme.csproj src/Acmebot.Acme/

RUN dotnet restore src/AzAcmeCertRenewal/AzAcmeCertRenewal.csproj -a "${TARGETARCH}" -p:SelfContained=true

COPY --link src/ src/

RUN dotnet publish src/AzAcmeCertRenewal/AzAcmeCertRenewal.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -a "${TARGETARCH}" \
        --self-contained true \
        -p:Version="${VERSION}" \
        -p:AssemblyVersion="${VERSION}" \
        -p:FileVersion="${VERSION}" \
        -p:InformationalVersion="${VERSION}" \
        -p:PublishSingleFile=true \
        -p:InvariantGlobalization=true \
        -p:DebugType=None \
        -p:DebugSymbols=false


FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:9cae0b131d058693c8d7496fed4a188380f05f27f34c98dd9a516b4d0331b0c4 AS final

ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

WORKDIR /app

COPY --link --from=build /app/publish/ ./
COPY --link LICENSE /licenses/LICENSE
COPY --link THIRD_PARTY_NOTICES.md /licenses/THIRD_PARTY_NOTICES.md
COPY --link src/Acmebot.Acme/LICENSE /licenses/acmebot/LICENSE

USER $APP_UID

ENTRYPOINT ["/app/AzAcmeCertRenewal"]

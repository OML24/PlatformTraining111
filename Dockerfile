# ---------------------------------------------------------------------------
# منصة التدريب والتطوير المهني — جامعة الزرقاء
# صورة واحدة تبني خدمة الـAPI وبوابة الويب، ويُشغّل كل منهما بأمر مستقل.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ZU.Training.sln ./
COPY src/ZU.Training.Domain/ZU.Training.Domain.csproj src/ZU.Training.Domain/
COPY src/ZU.Training.Application/ZU.Training.Application.csproj src/ZU.Training.Application/
COPY src/ZU.Training.Infrastructure/ZU.Training.Infrastructure.csproj src/ZU.Training.Infrastructure/
COPY src/ZU.Training.WebApi/ZU.Training.WebApi.csproj src/ZU.Training.WebApi/
COPY src/ZU.Training.WebPortal/ZU.Training.WebPortal.csproj src/ZU.Training.WebPortal/
COPY tests/ZU.Training.Domain.Tests/ZU.Training.Domain.Tests.csproj tests/ZU.Training.Domain.Tests/
RUN dotnet restore src/ZU.Training.WebApi/ZU.Training.WebApi.csproj \
 && dotnet restore src/ZU.Training.WebPortal/ZU.Training.WebPortal.csproj

COPY src/ src/
RUN dotnet publish src/ZU.Training.WebApi/ZU.Training.WebApi.csproj -c Release -o /app/api --no-restore \
 && dotnet publish src/ZU.Training.WebPortal/ZU.Training.WebPortal.csproj -c Release -o /app/portal --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# خطوط عربية لمولّد الشهادات (QuestPDF) وأدوات فحص الصحة
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl fontconfig fonts-dejavu-core fonts-noto-core \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Asia/Amman

COPY --from=build /app/api /app/api
COPY --from=build /app/portal /app/portal
COPY deploy/docker-entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh && mkdir -p /app/storage && chown -R app:app /app

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]

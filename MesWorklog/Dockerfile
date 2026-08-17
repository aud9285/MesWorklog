# ── 1. 프론트엔드 빌드 (Node) ─────────────────────────────
# Vite 8은 Node 20.19+ 를 요구하므로 22를 쓴다
FROM node:22-alpine AS frontend
WORKDIR /src/frontend

# package.json/lock만 먼저 복사해 npm ci 레이어를 캐싱한다.
# 소스만 고쳤을 때 의존성 재설치를 건너뛰어 빌드가 빨라진다
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build          # → /src/frontend/dist

# ── 2. 백엔드 빌드 (.NET SDK) ─────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["MesWorklog/MesWorklog.csproj", "MesWorklog/"]
RUN dotnet restore "./MesWorklog/MesWorklog.csproj"

COPY . .
WORKDIR "/src/MesWorklog"
RUN dotnet publish "./MesWorklog.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ── 3. 실행 이미지 ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
# 프론트 빌드 결과를 정적 파일 루트로 — 한 컨테이너에서 같이 서빙된다
COPY --from=frontend /src/frontend/dist ./wwwroot

USER $APP_UID
ENTRYPOINT ["dotnet", "MesWorklog.dll"]
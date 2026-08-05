# MES Worklog

스마트팩토리 구축 회사에서의 실무 경험을 바탕으로, 고객사가 중요하게 여겼던 기능을 재구현하는 프로젝트입니다.
MES 기능중에서 작업자별로 업무 시작/일시정지/재개/종료 기능을 웹에서 구현해서 해당 시작/일시정지/재개/종료 시간을 토대로 가동율을 그래프로 시각화를 구현하는 프로젝트입니다.


## 기술 스택

Language : C#  
Framework : .NET 10, ASP.NET Core Web API  
ORM : Entity Framework Core + Pomelo.EntityFrameworkCore.MySql  
DB : MySQL  

## 실행 화면

-- 개발중

## 실행 방법

-- 개발중

## 실행 방법

### 사전 요구사항

- .NET 10 SDK
- MySQL 서버 (또는 Docker로 MySQL 컨테이너 실행)

### 1. 리포지토리 클론

```bash
git clone <repo-url>
cd MesWorklog
```

### 2. MySQL 준비 (Docker 사용 시 예시)

```bash
docker run --name mes-mysql -e MYSQL_ROOT_PASSWORD=1234 -e MYSQL_DATABASE=mes -p 3306:3306 -d mysql:8
```

### 3. DB 연결 문자열 설정

`MesWorklog` 프로젝트 폴더에서 `dotnet user-secrets`로 로컬 전용 연결 문자열을 등록합니다 (비밀번호가 git에 올라가지 않도록).

```bash
cd MesWorklog
dotnet user-secrets set "ConnectionStrings:MesDb" "Server=localhost;Port=3306;Database=mes;User=root;Password=1234;"
```

### 4. 패키지 복원 및 마이그레이션 적용

```bash
dotnet restore
dotnet ef database update
```

### 5. 실행

```bash
dotnet run
```

또는 Visual Studio에서 시작 프로젝트를 `MesWorklog`로 두고 F5(디버그 실행)로도 실행할 수 있습니다.

실행 후 아래 주소로 접속할 수 있습니다.

- API: `https://localhost:7268` (또는 `http://localhost:5201`)
- Swagger UI: `https://localhost:7268/swagger`

## 도메인 모델 (ERD)

핵심 테이블 4개로 구성됩니다.

- *Process(공정)**: id, name, lineName
- **Worker(작업자)**: id, name, processId(FK)
- **WorkOrder(작업지시)**: id, processId(FK), orderDate, plannedStart, plannedEnd, targetQty
- **WorkLog(작업이력, 트랜잭션 테이블)**: id, workOrderId(FK), workerId(FK), status(WAITING/IN_PROGRESS/PAUSED/COMPLETED), startTime, endTime, pausedAt, totalPausedSeconds, actualQty

```
Process 1:N Worker
Process 1:N WorkOrder
Worker   1:N WorkLog
WorkOrder 1:N WorkLog
```

`WorkLog`가 `Worker`-`WorkOrder` 간 N:M 관계를 풀어주는 중간 테이블 역할을 합니다.

## 핵심 비즈니스 로직

### 시간 입력 방식

작업자가 시작/일시정지/재개/종료 각 시점의 시각을 **직접 선택**해서 서버에 전달하는 방식입니다. ("버튼 클릭 시 서버가 자동으로 현재 시각을 기록"하는 방식은 "작업자가 제때 못 누르면 시간이 부정확해진다"는 이유로 폐기했습니다.)

- 날짜: 캘린더 UI
- 시간: 시(0-23) + 분(10분 단위: 00/10/20/30/40/50) 선택

### 순수 작업시간 계산

```
순수 작업시간 = 전체 경과시간 - 누적 일시정지시간
```


### 서버 검증 규칙

- 모든 시각은 미래일 수 없음
- `pausedAt > startTime`
- `resumedAt > pausedAt`
- `endTime > 마지막 기록 시각` (일시정지 중이면 `pausedAt`, 아니면 `startTime`)
- 위반 시 409 Conflict + 에러 메시지 응답

### 알려진 한계

같은 작업지시를 동시에 여러 명이 시작하려는 경우의 동시성(race condition) 문제는 아직 처리하지 않았습니다. 개선 방향으로는 낙관적 락(버전 컬럼) 또는 DB 유니크 제약을 고려하고 있습니다.

## API 엔드포인트

| Method | Path | 설명 |
|---|---|---|
| GET | `/api/processes` | 공정 목록 |
| GET | `/api/workers?processId=` | 작업자 목록 |
| GET | `/api/work-orders?date=&processId=` | 작업지시 목록 |
| POST | `/api/work-logs/start` | `{ workOrderId, workerId, startTime }` |
| POST | `/api/work-logs/{id}/pause` | `{ pausedAt }` |
| POST | `/api/work-logs/{id}/resume` | `{ resumedAt }` |
| POST | `/api/work-logs/{id}/complete` | `{ endTime, actualQty }` |
| GET | `/api/work-logs/{id}` | 상세 조회 |
| GET | `/api/work-logs/timeline?date=` | 대시보드 타임라인(작업자별 세그먼트) |
| GET | `/api/work-logs/utilization?date=` | 작업자별 가동률 |

## 화면 구성

1. **현장 작업 화면**: 공정/작업자 선택 → 오늘의 작업지시 카드 목록 → 상태별 버튼(시작/일시정지/재개/종료) → 클릭 시 시간선택 Dialog → 완료는 시간 확정 후 실적수량 입력 2단계
2. **대시보드**: 요약 카드 3개(작업 인원/완료 건수/평균 가동률) + 작업자별 타임라인(간트 스타일 바 차트, 진행중=파랑/일시정지=주황/완료=초록)
3. **상세 조회**: 작업이력 ID로 조회, 계획 대비 실제 시간, 누적 정지시간, 순수 작업시간 표시

## 진행 상태

- 백엔드: 프로젝트 생성 및 초기 설계 단계
- 프론트엔드: 화면 골격 구성 중

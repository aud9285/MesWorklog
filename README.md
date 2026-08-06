# MES Worklog

스마트팩토리 구축 회사에서의 실무 경험을 바탕으로, 고객사가 중요하게 여겼던 기능을 재구현하는 프로젝트입니다.
MES 기능중에서 작업자별로 업무 시작/일시정지/재개/종료 기능을 웹에서 구현해서 해당 시각들을 토대로 OEE 시간가동율을 그래프로 시각화를 구현하는 프로젝트입니다.


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

마스터데이터 5개 + 조인 2개 + 트랜잭션 3개로 구성됩니다.

```
Line(라인)    ──N:M──▶ Process(공정)     ※ 조인: LineProcess
Process(공정) ──N:M──▶ Worker(작업자)    ※ 조인: WorkerProcess
Equipment(설비) — 어느 공정에도 종속되지 않는 독립 테이블

WorkOrder(작업지시) ──N:1──▶ Process (필수)
                   ──N:1──▶ Line (필수)
                   ──N:1──▶ Equipment (nullable, 수작업 가능)
                   ──1:N──▶ WorkLog(작업이력) ──N:1──▶ Worker
                                              ──1:N──▶ WorkLogPause ──N:1──▶ PauseReason

```


`WorkLog`가 `Worker`-`WorkOrder` 간 N:M 관계를 풀어주는 중간 테이블 역할을 합니다.

## 핵심 비즈니스 로직

### 시간 입력 방식

작업자가 시작/일시정지/재개/종료 각 시점의 시각을 **직접 선택**해서 서버에 전달하는 방식입니다. ("버튼 클릭 시 서버가 자동으로 현재 시각을 기록"하는 방식은 "작업자가 제때 못 누르면 시간이 부정확해진다"는 이유로 폐기했습니다.)

- 날짜: 캘린더 UI
- 시간: 시(0-23) + 분(10분 단위: 00/10/20/30/40/50) 선택

### 가동률(OEE Availability) 계산

```
조업시간   = EndTime - StartTime
계획정지   = Σ(WorkLogPause 중 category=PLANNED)     // 식사, 정기점검 등
비가동     = Σ(WorkLogPause 중 category=UNPLANNED)   // 고장, 자재대기 등
가동시간   = 조업시간 - 계획정지
실가동시간 = 가동시간 - 비가동

가동률(%) = Σ실가동시간 / Σ조업시간 × 100
```


### 서버 검증 규칙

- 모든 시각은 미래일 수 없음
- `pausedAt > 직전 시작/재개 시각` (정지 구간 겹침 방지)
- `resumedAt > pausedAt`
- `endTime > 마지막 기록 시각` (일시정지 중이면 `pausedAt`, 아니면 `startTime`)
- 위반 시 409 Conflict + 에러 메시지 응답
- **작업 상태 변경시 방지**: `Start`는 `WAITING`, `Pause`는 `IN_PROGRESS`, `Resume`은 `PAUSED`, `Complete`는 `IN_PROGRESS`일 때만 허용
- **작업자 중복 시작 방지**: 이미 활성(`IN_PROGRESS`/`PAUSED`) 건이 있는 작업자는 새로 시작 불가

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

1. **현장 작업 화면**: 이어하기 체크박스(끄면 신규 입력폼 — 라인→공정→작업자 캐스케이딩 셀렉트, 켜면 미완료 작업지시 목록에서 선택) → 시작/일시정지(사유 선택)/재개/완료(실적수량 입력 2단계). 오입력 데이터는 삭제 가능
2. **대시보드**: 기간(일/주/월/연) × 그룹(작업자/공정/라인/설비) 가동률 그래프. 공정·라인은 세로 막대, 작업자·설비는 가로 막대 + 스크롤
3. **상세 조회**: 실제 시작/종료, 정지 이력(사유별), 실가동시간, 가동률 표시. 라인/공정/설비 수정 가능
4. **마스터데이터 관리**: `[라인|공정|작업자|설비]` 탭, DataTable + Dialog CRUD

## 진행 상태

- 백엔드: 프로젝트 생성 및 초기 설계 단계
- 프론트엔드: 화면 골격 구성 중

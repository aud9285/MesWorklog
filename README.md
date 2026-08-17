# MES Worklog

스마트팩토리 구축 회사에서의 실무 경험을 바탕으로, 고객사가 중요하게 여겼던 기능을 재구현하는 프로젝트입니다.
MES 기능중에서 작업자별로 업무 시작/일시정지/재개/종료 기능을 웹에서 구현해서 해당 시각들을 토대로 OEE 시간가동율을 그래프로 시각화를 구현하는 프로젝트입니다.


## 기술 스택

Language : C#  
Framework : .NET 10, ASP.NET Core Web API  
ORM : Entity Framework Core + Pomelo.EntityFrameworkCore.MySql
      Dapper + EFCore.NamingConventions
DB : MySQL

## 데모

**http://43.201.89.32** — AWS EC2에 Docker Compose로 배포(앱+MySQL 컨테이너). 3개월치 시드 데이터가 들어있어 대시보드·상세조회에서 실제 데이터로 확인할 수 있습니다.

## 실행 화면

-- 개발중

## 실행 방법

### Docker로 실행

컨테이너 하나(웹)+하나(MySQL)로 전체가 뜹니다. 실제 데모 배포에도 이 구성을 그대로 씁니다.

```bash
git clone https://github.com/aud9285/MesWorklog.git
cd MesWorklog
echo "MYSQL_ROOT_PASSWORD=원하는_비밀번호" > .env
docker compose up --build
```

`http://localhost` 에서 확인할 수 있습니다(80번 포트가 이미 쓰이고 있다면 `.env`에 `APP_PORT=8080` 같은 값을 추가해서 바꿀 수 있습니다). 최초 실행 시 스키마 마이그레이션이 자동으로 적용되지만, 마스터데이터·작업이력은 비어있는 상태로 시작합니다.

### 직접 실행

#### 사전 요구사항

- .NET 10 SDK
- Node.js 20.19+ (프론트엔드 실행 시)
- MySQL 서버 (또는 위 Docker 방식으로 MySQL만 따로 띄워도 됨)

#### 1. 리포지토리 클론

```bash
git clone https://github.com/aud9285/MesWorklog.git
cd MesWorklog
```

#### 2. MySQL 준비 (Docker 사용 시 예시)

```bash
docker run --name mes-mysql -e MYSQL_ROOT_PASSWORD=1234 -e MYSQL_DATABASE=mes -p 3306:3306 -d mysql:8
```

#### 3. DB 연결 문자열 설정

`MesWorklog` 프로젝트 폴더에서 `dotnet user-secrets`로 로컬 전용 연결 문자열을 등록합니다 (비밀번호가 git에 올라가지 않도록).

```bash
cd MesWorklog
dotnet user-secrets set "ConnectionStrings:MesDb" "Server=localhost;Port=3306;Database=mes;User=root;Password=1234;"
```

#### 4. 패키지 복원 및 마이그레이션 적용

```bash
dotnet restore
dotnet ef database update
```

#### 5. 실행

```bash
dotnet run
```

또는 Visual Studio에서 시작 프로젝트를 `MesWorklog`로 두고 F5(디버그 실행)로도 실행할 수 있습니다.

실행 후 아래 주소로 접속할 수 있습니다.

- API: `https://localhost:7268` (또는 `http://localhost:5201`)
- Swagger UI: `https://localhost:7268/swagger`

#### 6. 프론트엔드 실행 (선택)

`frontend/` 폴더에 Vue 3 + PrimeVue 프로젝트가 있습니다. 화면 4개 모두 API 연동이 끝나 있어, 위 5번까지 진행해 백엔드를 띄운 상태에서 실행하면 실제 데이터로 동작합니다.

```bash
cd frontend
npm install
npm run dev
```

`http://localhost:5173` 에서 확인할 수 있습니다. Vite dev 서버가 `/api` 요청을 백엔드(`http://localhost:5201`)로 프록시합니다.

## 테스트

```bash
dotnet test
```

`WorkLog`의 상태 전이 예외처리와 OEE 시간 분해를 검증하는 단위테스트 13건이 있습니다.
DB나 웹 서버 없이 실행되며, 아래 항목들을 고정합니다.

- 정지 중에는 완료 불가 
- 연속 정지 불가 — 정지가 여러 개 생겨 시간 계산이 커지던 문제 방지
- 모든 시각 입력의 미래 시각 거부
- 계획정지/비가동이 각각 차감되는지 (조업 180분 → 부하 150분 → 가동 130분)
- **정지·종료 시각이 직전 재개 시각과 "같은" 경우 허용** 

DB 없이 시간 경계값을 검증할 수 있는 건 모든 상태 전이 메서드가 기준시각(`now`)을 파라미터로 주입받도록 설계했기 때문입니다.

## 도메인 모델 (ERD)

마스터데이터 5개 + 조인 2개 + 트랜잭션 3개로 구성됩니다.

<img width="1178" height="727" alt="Image" src="/MesWorklog/docs/mes.png" />

```
Line(라인)    ──N:M──▶ Process(공정)     ※ 조인: LineProcess
Process(공정) ──N:M──▶ Worker(작업자)    ※ 조인: WorkerProcess
Equipment(설비) — 어느 공정에도 종속되지 않는 독립 테이블

WorkOrder(작업지시) ──N:1──▶ Process (필수)
                   ──N:1──▶ Line (필수)                   
                   ──1:N──▶ WorkLog(작업이력) ──N:1──▶ Worker
                                              ──N:1──▶ Equipment (nullable, 수작업 가능)
                                              ──1:N──▶ WorkLogPause ──N:1──▶ PauseReason

```


`WorkLog`가 `Worker`-`WorkOrder` 간 N:M 관계를 풀어주는 중간 테이블 역할을 합니다.

LineProcess, WorkerProcess는 복합키로 관리

테이블·컬럼명은 스네이크 케이스(`work_logs`, `elapsed_minutes`)이며, 각 테이블과 컬럼에 한글 논리명이 COMMENT로 기록되어 있습니다.

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
부하시간   = 조업시간 - 계획정지
가동시간   = 부하시간 - 비가동

가동률(%) = Σ가동시간 / Σ부하시간 × 100
```

계획정지(식사·정기점검)를 분모에서 빼는 이유는, 애초에 가동할 계획이 없던 시간까지 분모에 넣으면 정지 사유를 성실히 기록할수록 수치가 나빠지기 때문입니다.


### 서버 검증 규칙

- 모든 시각은 미래일 수 없음
- `resumedAt > pausedAt` — 정지 구간 자신의 길이가 0/음수가 되는 것을 막음
- `pausedAt >= 직전 시작/재개 시각`, `endTime >= 마지막 기록 시각` — **"이전"만 거부하고 "같음"은 허용**. 처음엔 같음도 막았으나, "식사 끝나자마자 곧바로 설비고장 정지", "고장 수리 후 재개하자마자 종료" 같은 정상 시나리오가 전부 거부되는 문제가 있어 완화했습니다. 두 정지 사이 가동시간이 0분이어도 계산식은 깨지지 않습니다(정지 구간을 빼는 구조라 0만큼 덜 빠질 뿐)
- 위반 시 **400 Bad Request**(보낸 값 자체가 잘못됨) / **409 Conflict**(값은 정상이나 현재 상태와 충돌)로 구분해 응답. RFC 7807 `ProblemDetails` 형식
- **작업 상태 전이 가드**: `Pause`는 `IN_PROGRESS`, `Resume`은 `PAUSED`, `Complete`는 `IN_PROGRESS`일 때만 허용. `Start`는 정적 팩토리(`WorkLog.Start(...)`)라 "시작 전 상태" 자체가 존재하지 않아 별도 가드가 필요 없음
- **작업자 중복 시작 방지**: 이미 활성(`IN_PROGRESS`/`PAUSED`) 건이 있는 작업자는 새로 시작 불가


### 설계 과정에서 발견해 고친 문제

초안을 스스로 검토하며 찾은 것들입니다. 

- **가동률이 부풀려지던 버그**: 정지 상태에서 바로 완료하면 정지가 끝나지 않는 상태가되고 해당 구간이 시간 합산에서는 빠지면서 조업시간에는 포함돼, 가동률이 100%로 계산됐습니다. 그래서 완료는 무조건 진행중 상태에서만 가능하게 변경하였습니다.
- **연속 정지로 시간 계산이 무한정 커지던 문제**: 상태 체크 구간이 없어서 정지를 연달아 호출하면 재개시간이(재개 처리되지 않은) 없는 정지가 여러 개 생성됐습니다.
- **방치된 진행중 작업이 분모를 계속 키우던 문제**: 퇴근 시 종료를 잊은 건을 취소할 방법이 없었습니다. 작업자가 작업종료 되지않는 이력이 있을경우 Exception 발생기능 추가. 오입력으로 기록된 이력을 위해 `DELETE /api/work-logs/{id}` 추가
- **설비를 특정 공정에 종속시킨 모델링 오류**: 같은 설비를 여러 공정이 공유하면 데이터가 모순되어 독립 엔티티로 변경했습니다.
- **마스터데이터 삭제가 작업이력을 통째로 지울 뻔한 문제**: EF Core는 필수 FK의 삭제 전파가 기본 활성화라(cascade), 설정 없이 배포했다면 작업자 삭제 시 그 사람의 작업이력이 전부 사라질 뻔했습니다. 이력이 있으면 `is_active=false`로 비활성화·없으면 실제 삭제하는 정책으로 정리했습니다.
- **설비를 작업지시에(WorkOrder) 고정 시킨것을 작업이력(WorkLog)으로 이관**: 설비가 고장이슈로 설비를 교체할 시 과거 이력이 왜곡되는 이슈발생 WorkOrder -> WorkLog로 설비 이관, 이제 설비 고장이슈로 설비 교체시 진행 flow는 설비고장으로 인한 중지 -> 재개 -> 완료 수량입력 -> 이어하기로 변경

### 알려진 한계

같은 작업지시를 동시에 여러 명이 시작하려는 경우의 동시성(race condition) 문제는 아직 처리하지 않았습니다. 개선 방향으로는 낙관적 락(버전 컬럼) 또는 DB 유니크 제약을 고려하고 있습니다.

## API 엔드포인트

전부 구현 완료이며, 프론트엔드 4개 화면에 모두 연동돼 있습니다.

| Method | Path | 설명 |
|---|---|---|
| GET/POST/PUT/DELETE | `/api/lines?includeInactive=` | 라인 CRUD |
| GET/POST/PUT/DELETE | `/api/processes?includeInactive=` | 공정 CRUD (소속 라인 N:M, `lineIds` 배열로 응답) |
| GET/POST/PUT/DELETE | `/api/workers?includeInactive=` | 작업자 CRUD (소속 공정 N:M, `processIds` 배열로 응답) |
| GET/POST/PUT/DELETE | `/api/equipments?includeInactive=` | 설비 CRUD |
| GET | `/api/pause-reasons` | 정지사유 목록 |
| GET | `/api/work-orders/open?workerId=` | 이어하기용 미완료 작업지시 |
| POST | `/api/work-logs/start` | `{workerId, startTime, equipmentId, workOrderId}`(이어하기) 또는 `{..., lineId, processId, targetQty}`(신규 생성) |
| GET | `/api/work-logs/active?workerId=` | 그 작업자의 진행중/정지중 건. 없으면 204 |
| POST | `/api/work-logs/{id}/pause` | `{pausedAt, pauseReasonId}` |
| POST | `/api/work-logs/{id}/resume` | `{resumedAt}` |
| POST | `/api/work-logs/{id}/complete` | `{endTime, actualQty}` |
| DELETE | `/api/work-logs/{id}` | 오입력·방치 이력 삭제 |
| GET | `/api/work-logs?startDate=&endDate=` | 기간별 작업이력 목록 (상세조회 화면) |
| GET | `/api/work-logs/by-order/{workOrderId}` | 한 작업지시에 딸린 세션 전체 |
| GET | `/api/work-logs/effectiveness?period=&date=&groupBy=` | 대시보드 가동률 집계 (Dapper) |

> `PUT /api/work-orders/{id}`(라인·공정·목표수량 정정)는 설계·구현까지 했으나 **끝내 어느 화면에서도 호출하지 않아 제거**했습니다. 라인→공정 캐스케이딩 셀렉트가 오선택을 대부분 막아줘서 정정 경로가 실제로 필요하지 않았습니다.

## 화면 구성

4개 화면 모두 UI/UX 구현 + API 연동 완료입니다(목 데이터는 전부 제거). 소스는 `frontend/src/components/`.

1. **현장 작업 화면** (`WorkerDashboard.vue`): **작업자 선택이 먼저** — 그 작업자의 활성 건이 있으면 진행중 카드(정지/재개/완료/삭제), 없으면 시작 폼. 이어하기 체크박스(끄면 라인→공정 캐스케이딩 셀렉트로 신규 생성, 켜면 미완료 작업지시 카드에서 선택). 라인·공정 후보는 **그 작업자가 배정된 조합만** 노출. 시각은 10분 단위 선택 + 미래 시각 차단. 완료는 시각 확정 → 실적수량 입력 2단계. 목표/누적/잔여 수량 표시
2. **대시보드** (`Dashboard.vue`): 기간(일/월/연) × 그룹(작업자/공정/라인/설비) 가동률 그래프. 공정·라인은 세로 막대, 작업자·설비는 가로 막대 + 스크롤. CSS 기반 막대라 별도 차트 라이브러리 없음. 완료 건뿐 아니라 **진행중 건도 조회 시점 기준으로 실시간 합산**
3. **상세 조회** (`DetailView.vue`): 기간(범위) 선택 → 작업지시 단위 대표 목록(표, 페이지네이션·정렬·검색) → 행 선택 시 하단에 작업지시 헤더 + 그 작업지시의 세션 카드 전부. 완료 건만 시간 분해(조업/부하/가동 누적 막대 + 계산식) 표시, 진행중 건은 경과 시간만. 세션 단위 삭제 가능
4. **마스터데이터 관리** (`MasterData.vue`): `[라인|공정|작업자|설비]` 탭, DataTable + Dialog CRUD. 공정/작업자 탭은 N:M 관계를 MultiSelect로 편집. 비활성 항목은 기본적으로 숨기고 토글로 복구 가능

## 프로젝트 구조

```
MesWorklog/
├── MesWorklog/          ASP.NET Core Web API
├── MesWorklog.Tests/    xUnit 단위테스트
├── frontend/            Vue 3 + PrimeVue (별도 npm 프로젝트)
└── docs/DESIGN_DECISIONS.md   설계 결정 전체 이력
```

## 진행 상태

- 설계: 완료
- 백엔드
  - 도메인 모델 / 데이터 계층(EF Core, 마이그레이션): 완료
  - 전역 예외 처리(`IExceptionHandler` → ProblemDetails): 완료
  - 마스터데이터 CRUD 4종(라인 / 공정 / 작업자 / 설비): 완료
  - 작업이력(시작·정지·재개·완료·삭제·조회) API: 완료
  - Dapper 기반 대시보드 집계(`EffectivenessService`): 완료
- 테스트: `WorkLog` 상태 전이·OEE 시간 분해 단위테스트 13건 (`dotnet test`)
- 프론트엔드: 화면 4개 전부 UI/UX + API 연동 완료
- 검증: 3개월치 시드 데이터로 대시보드 4개 그룹 기준 집계 확인, 시작→정지→재개→완료 전체 흐름 브라우저 검증 완료


## AI 도구 활용

이 프로젝트는 개발 과정에서 Claude(Anthropic)를 도구로 활용했습니다.
- Java(MyBatis) 경험을 C#/EF Core로 옮기는 과정에서 개념 설명·비교 학습 용도로 활용
- **프론트엔드 UI/UX(컴포넌트 마크업·스타일)**: Claude가 작성
- 디버깅 시 원인 분석 보조로 활용 (실제 수정은 직접 진행)

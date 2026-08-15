using MesWorklog.Common;
using MesWorklog.Data;
using MesWorklog.Dtos;
using MesWorklog.Exceptions;
using MesWorklog.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;
using Dapper;

namespace MesWorklog.Services
{
    // C#에서 Service는 ASP.NET Core는 Program.cs에 직접 등록해야 함

    // sql문을 param을 넣어서 실행
    // QueryAsync<T>(sql, param) — 목록조회 
    // QueryFirstAsync<T>(sql, param) - 단건조회(여러건중 하나만 필요할때 0건일시 예외처리)
    // QueryFirstOrDefaultAsync<T>(sql, param) - 단건조회(여러건중 하나만 필요할때 0건일시 null)
    // QuerySingleAsync<T>(sql, param) - 단건조회(한건만 나와야할떄(pk값으로 조회할떄) 여러건이나, 0건일시 예외처리)
    // QuerySingleOrDefaultAsync<T>(sql, param) - 단건조회(한건만 나와야할떄(pk값으로 조회할떄) 여러건일시 예외처리, 0건일시 null)
    // ExecuteAsync(sql, param) - INSERT/UPDATE/DELETE시 사용 리턴값을 int로(1개 바뀌면 1, 변한게없으면 0)
    // ExecuteScalarAsync<T>(sql, param) - 1행1열값만 조회할때 사용
    public class EffectivenessService



    {
        // log
        private readonly ILogger<EffectivenessService> _logger;   // <T> 자리에 자기 클래스 이름

        // EF Core의 AppDbContext 대신 순수 IDbConnection 보관 — Dapper 전용이라 ChangeTracker 불필요
        private readonly IDbConnection _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public EffectivenessService(IDbConnection db, ILogger<EffectivenessService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // 날짜별(연/월/일), 그룹별(라인/공정/작업자/설비) 가동율
        public async Task<List<EffectivenessResponse>> GetEffectivenessAsync(string period, DateTime date, string groupBy)
        {
            // 튜플 문법 ResolvePeriod에서 계산한 날짜를받음
            var (start, end) = ResolvePeriod(period, date);

            // 튜플 문법 GroupByClause에서 그룹기준의 key, name, sql join문을 받아옴
            var (groupKey, groupName, join) = GroupByClause(groupBy);

            // koreatime.now 한국시간기준 지금시간가져옴
            var now = KoreaTime.Now;

            // dapper가 프로퍼티를 매칭해서 바인딩(Start = @Start...)
            var queryParams = new { Start = start, End = end, Now = now };

            // 완성건 시간 계산
            // 선택한 날짜,그룹별 가동률
            var completedSql = $@"
                SELECT {groupKey} AS GroupKey, {groupName} AS GroupName,
                        CAST(SUM(w.operating_minutes) AS SIGNED) AS OperatingMinutes,
                        CAST(SUM(w.net_operating_minutes) AS SIGNED) AS NetOperatingMinutes
                FROM work_logs w
                {join}
                WHERE w.status = 'Completed' AND w.start_time >= @Start AND w.start_time < @End
                GROUP BY {groupKey}, {groupName}";

            // 진행중건(정지건) 시간 계산
            // 서브쿼리로 한 이유 work_logs 테이블과 work_log_pauses 테이블은 1:N 관계로 서브쿼리로 안할시 work_logs가 중복됨
            var inProgressSql = $@"
                SELECT {groupKey} AS GroupKey, {groupName} AS GroupName,
                       CAST(SUM(TIMESTAMPDIFF(MINUTE, w.start_time, @Now) - COALESCE(op.planned_minutes, 0)) AS SIGNED) AS OperatingMinutes,
                       CAST(SUM(TIMESTAMPDIFF(MINUTE, w.start_time, @Now) - COALESCE(op.planned_minutes, 0) - COALESCE(op.unplanned_minutes, 0)) AS SIGNED) AS NetOperatingMinutes
                FROM work_logs w
                {join}
                LEFT OUTER JOIN (
                    SELECT wlp.work_log_id,
                           -- CASE WHEN으로 정지사유 분류(계획/비가동)별 합계를 한 번의 GROUP BY 안에서 같이 계산(조건부 집계)
                           CAST(SUM(CASE WHEN pr.category = 'Planned'   THEN TIMESTAMPDIFF(MINUTE, wlp.paused_at, COALESCE(wlp.resumed_at, @Now)) ELSE 0 END) AS SIGNED) AS planned_minutes,
                           CAST(SUM(CASE WHEN pr.category = 'Unplanned' THEN TIMESTAMPDIFF(MINUTE, wlp.paused_at, COALESCE(wlp.resumed_at, @Now)) ELSE 0 END) AS SIGNED) AS unplanned_minutes
                    FROM work_log_pauses wlp
                    INNER JOIN pause_reasons pr ON pr.id = wlp.pause_reason_id
                    INNER JOIN work_logs wl ON wl.id = wlp.work_log_id
                        AND wl.status IN ('InProgress', 'Paused')
                        AND wl.start_time >= @Start AND wl.start_time < @End
                    GROUP BY wlp.work_log_id
                ) op ON op.work_log_id = w.id
                WHERE w.status IN ('InProgress', 'Paused') AND w.start_time >= @Start AND w.start_time < @End
                GROUP BY {groupKey}, {groupName}";

            // QueryAsync<T> — SELECT 실행하고 결과를 T(EffectivenessResponse) 리스트로 매핑. 완료건/진행중건 각각 별도 왕복
            var completedRows = (await _db.QueryAsync<EffectivenessResponse>(completedSql, queryParams)).ToList();
            var inProgressRows = (await _db.QueryAsync<EffectivenessResponse>(inProgressSql, queryParams)).ToList();

            // ToDictionary — 배열,리스트등을 key, value 구조로 변환
            // 완료건 리스트를 (GroupKey, GroupName) 튜플을 key로 변환 value = r
            var merged = completedRows.ToDictionary(r => (r.GroupKey, r.GroupName));

            _logger.LogInformation("가동율 완료건 확인{key}, {value}", merged.Keys, merged.Values);


            // 진행중건을 반복문으로 완료건에 합산
            foreach (var row in inProgressRows)
            {
                // 동일한 값의 튜플을 key로 변환
                var key = (row.GroupKey, row.GroupName);

                _logger.LogInformation("가동율 진행중건 확인{groupkey}, {groupname}, {nMin}, {oMin}", row.GroupKey, row.GroupName, row.NetOperatingMinutes, row.OperatingMinutes);


                // 삼항연산자
                // 조건이 true면 ? false면 :
                // TryGetValue — 키가 있으면 existing에 담고 true, 없으면 false(예외 처리 x)
                merged[key] = merged.TryGetValue(key, out var existing)
                    // 완료건에 같은 그룹이 있으면 — with 식으로 existing을 복사하면서 두 필드만 덮어씀
                    ? existing with
                    {
                        OperatingMinutes = existing.OperatingMinutes + row.OperatingMinutes,   // "완료건.시간 + 진행중건.시간"
                        NetOperatingMinutes = existing.NetOperatingMinutes + row.NetOperatingMinutes
                    }
                    // 완료건에 없던 그룹(진행중인 건만 있는 경우)이면 그대로 추가
                    : row;
            }

            // .Values — value 값들만 꺼내서, 가동률 리스트 계산  오름차순 리스트
            return merged.Values.OrderByDescending(r => r.RatePercent).ToList();
        }

        // private static — 이 클래스 안에서만(인스턴스 안씀)
        // period = 웹에서 받은 기간단위
        // 받은기간 단위로 날짜 가공 "day" = day ~ day + 1(day), "month" = month ~ month + 1(month), "year" = year ~ year + 1(year)
        private static (DateTime Start, DateTime End) ResolvePeriod(string period, DateTime date)
        {
            // .Date = DateTime에서 시:분:초를 00:00:00으로 버림(날짜만 남김)
            var day = date.Date; 

            // switch 식(expression) — switch문(statement)이랑 다르게 값을 바로 리턴하는 표현식 형태
            // period가 day일 시 
            return period switch
            {
                // case: "day" 라면 return(day, day.addDay(1))
                "day" => (day, day.AddDays(1)),

                // case: "month" 라면 return(new DateTime(day.Year, day.Month, 1), new DateTime(day.Year, day.Month, 1).AddMonths(1))
                "month" => (new DateTime(day.Year, day.Month, 1), new DateTime(day.Year, day.Month, 1).AddMonths(1)),

                // case: "year" 라면 return(new DateTime(day.Year, 1, 1), new DateTime(day.Year, 1, 1).AddYears(1))
                "year" => (new DateTime(day.Year, 1, 1), new DateTime(day.Year, 1, 1).AddYears(1)),

                // _  = 위 3개 다 아닐 때(default)
                _ => throw new BusinessRuleException($"알 수 없는 기간 단위입니다: {period}") 
            };
        }

        // private static — 이 클래스 안에서만(인스턴스 안씀)
        // groupBy = 웹에서 받은 그룹기준
        // 받은 그룹기준에 따라 그룹키, 그룹이름, sql join문 return
        // "worker" 작업자 = workers.id, workers.name, inner join workers ~
        // "process" 공정 = processes.id, processes.name, inner join processes ~
        // "line" 라인 = lines.id, lines.name, inner join lines ~
        // "equipment" 설비 = equipments.id, equipments.name, left outer join equipments
        private static (string GroupKey, string GroupName, string Join) GroupByClause(string groupBy) => groupBy switch
        {
            // work_logs가 worker_id를 직접 갖고 있어서 work_orders 안 거치고 바로 workers만 조인
            "worker" => ("w.worker_id", "wk.name",
                "INNER JOIN workers wk ON wk.id = w.worker_id"),

            // process_id는 work_orders 소유라 work_orders를 한 단계 거쳐야 함
            "process" => ("o.process_id", "p.name",
                "INNER JOIN work_orders o ON o.id = w.work_order_id INNER JOIN processes p ON p.id = o.process_id"),

            "line" => ("o.line_id", "l.name",
                "INNER JOIN work_orders o ON o.id = w.work_order_id INNER JOIN `lines` l ON l.id = o.line_id"),

            // equipment_id는 nullable(수작업이면 NULL)이라 INNER JOIN 쓰면 수작업 건이 통째로 빠짐 → LEFT OUTER JOIN
            // COALESCE(e.name, '수작업') — 매칭 안 돼서 e.name이 NULL이면 화면에 보여줄 라벨을 대신 채움
            "equipment" => ("w.equipment_id", "COALESCE(e.name, '수작업')",
                "LEFT JOIN equipments e ON e.id = w.equipment_id"),

            _ => throw new BusinessRuleException($"알 수 없는 그룹 기준입니다: {groupBy}")
        };
    }
}
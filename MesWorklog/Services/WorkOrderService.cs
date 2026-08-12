using MesWorklog.Common;
using MesWorklog.Data;
using MesWorklog.Dtos;
using MesWorklog.Exceptions;
using MesWorklog.Models;
using Microsoft.EntityFrameworkCore;

namespace MesWorklog.Services
{
    // C#에서 Service는 ASP.NET Core는 Program.cs에 직접 등록해야 함
    public class WorkOrderService
    {
        // 보관할 필드 생성자에서만 대입가능, 이 후 변경불가
        private readonly AppDbContext _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public WorkOrderService(AppDbContext db)
        {
            _db = db;
        }

        // 이어하기 목록
        // 작업지시서(WokrOrder)기준 목표수량에 해당 작업지시(WorkOrder)의 작업이력(WorkLog)의 완료수량 합이 도달하지 않은 목록
        // + 작업자 기준 배정된 공정의 작업지시만
        public async Task<List<OpenWorkOrderResponse>> GetOpenByWorkerAsync(int workerId)
        {
            // 이 작업자가 배정된 공정 id들 (WorkerProcess 조인)
            var myProcessIds = await _db.WorkerProcesses
                .Where(wp => wp.WorkerId == workerId)
                .Select(wp => wp.ProcessId)
                .ToListAsync();

            // 배정된 공정이 없으면 이어할 대상도 없다 — DB에 더 안 가고 바로 반환
            if (myProcessIds.Count == 0)
                return new List<OpenWorkOrderResponse>();

            var orders = await _db.WorkOrders
                .AsNoTracking()
                .Include(o => o.Line)
                .Include(o => o.Process)
                .Include(o => o.Equipment)
                .Include(o => o.WorkLogs)          // 누적 실적/인원 수를 세는 데 필요
                // CompletedAt이 null = 목표 미달 = 아직 이어할 수 있음
                .Where(o => o.CompletedAt == null && myProcessIds.Contains(o.ProcessId))
                .ToListAsync();

            // 집계는 메모리에서 — WorkLogs를 이미 Include로 가져왔으므로 추가 쿼리가 없다
            return orders.Select(o => new OpenWorkOrderResponse(
                o.Id,
                o.LineId, o.Line.Name,
                o.ProcessId, o.Process.Name,
                o.EquipmentId, o.Equipment?.Name,
                o.TargetQty,
                // 누적실적 = 완료된 이력들의 수량 합
                o.WorkLogs.Where(w => w.Status == WorkLogStatus.Completed).Sum(w => w.ActualQty),    
                // 진행중 인원 체크
                o.WorkLogs.Count(w => w.Status == WorkLogStatus.InProgress
                                   || w.Status == WorkLogStatus.Paused)))
                .ToList();
        }

    }

}
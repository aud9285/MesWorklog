using MesWorklog.Common;
using MesWorklog.Data;
using MesWorklog.Dtos;
using MesWorklog.Exceptions;
using MesWorklog.Models;
using Microsoft.EntityFrameworkCore;

namespace MesWorklog.Services
{
    // C#에서 Service는 ASP.NET Core는 Program.cs에 직접 등록해야 함
    
    // ToListAsync() — 여러 건 조회, 리스트로 (SELECT ... WHERE ...)
    // FirstOrDefaultAsync() — 단건 조회, 없으면 null (SELECT ... LIMIT 1)
    // AnyAsync(조건) — 존재 여부만 (true/false) (SELECT EXISTS...)
    // CountAsync(조건) — 건수 (SELECT COUNT(*)  ...
    // FindAsync(PK) - 캐시먼저 확인, PK 조회 (SELECT ... WHERE PK = ?)
    // SumAsync(선택자) - 합계 계산(SELECT SUM()... WHERE ...)
    // SaveChangesAsync() -  한 트랜잭션으로 INSERT/UPDATE/DELETE 실행. 중간에 실패시 Rollback (INSERT, UPDATE, DELETE...)
    public class PauseReasonService
    {
        // 보관할 필드 생성자에서만 대입가능, 이 후 변경불가
        private readonly AppDbContext _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public PauseReasonService(AppDbContext db)
        {
            _db = db;
        }


        // 정지사유 목록. 등록/수정 화면이 없어 조회만 제공한다(등록/수정은 DBeaver로 직접 관리)
        // 등록사유가 추가되는 경우는 거의 없음으로 등록/수정은 제외
        public async Task<List<PauseReasonResponse>> GetAllAsync()
        {
            // 계획정지끼리, 비가동끼리 묶여 보이도록 분류 → 이름 순으로 정렬
            var reasons = await _db.PauseReasons
                .AsNoTracking()
                .OrderBy(r => r.Category).ThenBy(r => r.Name)
                .ToListAsync();

            // enum → 문자열 변환은 SQL로 번역이 안 될 수 있어 메모리에서 처리한다
            return reasons
                .Select(r => new PauseReasonResponse(r.Id, r.Name, r.Category.ToString()))
                .ToList();
        }



    }

}
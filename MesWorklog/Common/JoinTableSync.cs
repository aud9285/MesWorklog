namespace MesWorklog.Common
{
    public static class JoinTableSync
    {
        // existingIds: 지금 DB에 있는 조인 대상 id들
        // requestedIds: 프론트가 체크박스로 보낸 새 id 전체
        // 어떤걸 등록할지(ToInsert) 어떤걸 지울지(ToDelete)만 계산 — DB 접근 없음
        public static (List<int> ToInsert, List<int> ToDelete) Diff(
            IEnumerable<int> existingIds, IEnumerable<int> requestedIds)
        {
            var existing = existingIds.ToHashSet();
            var requested = requestedIds.ToHashSet();

            return (
                ToInsert: requested.Except(existing).ToList(),
                ToDelete: existing.Except(requested).ToList()
            );
        }
    }
}

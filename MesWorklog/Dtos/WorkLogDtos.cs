using System.ComponentModel.DataAnnotations;

namespace MesWorklog.Dtos
{
    // 신규 생성, 이어하기 두가지 형태로요청받음
    // 신규 생성 : lineId + ProcessId + targetQty
    // 이어하기 : workOrderId
    public class CreateWorkLogRequest
    {
        [Required(ErrorMessage = "작업자는 필수입니다.")]
        public int WorkerId { get; set; }

        [Required(ErrorMessage = "시작 시각은 필수입니다.")]
        public DateTime StartTime { get; set; }

        // 이어하기 — 값이 있으면 이 작업지시를 그대로 재사용
        public int? WorkOrderId { get; set; }

        // 신규 생성 — WorkOrderId가 없을 때만 사용
        public int? LineId { get; set; }
        public int? ProcessId { get; set; }
        public int? EquipmentId { get; set; }   // 수작업이면 null (설비 없이 진행)
        public int? TargetQty { get; set; }
    }
    // 작업 정지 
    public class PauseWorkLogRequest
    {
        [Required]
        public DateTime PausedAt { get; set; }      // 정지시간

        [Required]
        public int PauseReasonId { get; set; }      // 정지사유코드
    }

    // 작업 재개
    public class ResumeWorkLogRequest
    {
        [Required(ErrorMessage = "재개 시각은 필수입니다.")]
        public DateTime ResumedAt { get; set; }
    }

    // 작업 완료
    public class CompleteWorkLogRequest
    {
        [Required(ErrorMessage = "종료 시각은 필수입니다.")]
        public DateTime EndTime { get; set; }

        // 음수 방지 — WorkLog.Complete() 자체엔 이 검증이 없어서 여기서 반드시 막아야 함
        [Range(1, int.MaxValue, ErrorMessage = "실적 수량은 1 이상이어야 합니다.")]
        public int ActualQty { get; set; }
    }

    public record WorkLogResponse(
        int Id,
        int WorkOrderId,
        int WorkerId,
        string Status,       // enum을 문자열로 노출 — 프론트가 숫자 대신 "InProgress" 그대로 씀
        DateTime StartTime,
        DateTime? EndTime,
        int? ActualQty,
        int? ElapsedMinutes,
        int? OperatingMinutes,
        int? NetOperatingMinutes
    );

    // 현장 작업 화면 - 작업자 선택 직후 호출하는 응답.
    // 화면이 라인명·공정명·정지사유명까지 다 필요로 해서, 추가 요청 없이 한 번에 주도록 펼쳐서 담는다
    public record ActiveWorkLogResponse(
        int Id,
        int WorkOrderId,
        int WorkerId,
        string WorkerName,
        string LineName,
        string ProcessName,
        string? EquipmentName,     // 수작업이면 설비가 없어서 null
        int TargetQty,             // 작업지시의 목표 수량
        int CompletedQty,          // 이 작업지시에 쌓인 누적 실적(완료된 이력들의 합)
        string Status,
        DateTime StartTime,
        List<WorkLogPauseResponse> Pauses);

    // 정지 이력 한 건. 화면은 ResumedAt이 null인 걸 정지중인걸로 보고 사유를 표시한다
    public record WorkLogPauseResponse(
        int Id,
        string ReasonName,
        string Category,           // "Planned" / "Unplanned"
        DateTime PausedAt,
        DateTime? ResumedAt);

    // 상세조회 목록 카드 한줄
    public record WorkLogListItemResponse(
        int Id,
        int WorkOrderId,
        int WorkerId,
        string WorkerName,
        string LineName,
        string ProcessName,
        string? EquipmentName,
        string Status,
        DateTime StartTime,
        DateTime? EndTime,
        int? ActualQty,
        int? ElapsedMinutes,
        int? OperatingMinutes,
        int? NetOperatingMinutes);

    // 상세 화면에서 세션카드 하나
    public record WorkLogDetailResponse(
        int Id,
        int WorkOrderId,
        int LineId,
        string LineName,
        int ProcessId,
        string ProcessName,
        string? EquipmentName,
        int WorkerId,
        string WorkerName,
        string Status,
        DateTime StartTime,
        DateTime? EndTime,
        int? ActualQty,
        int? ElapsedMinutes,
        int? OperatingMinutes,
        int? NetOperatingMinutes,
        int TargetQty,
        List<WorkLogPauseResponse> Pauses);
}


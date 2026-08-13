using System.ComponentModel.DataAnnotations;

namespace MesWorklog.Dtos
{

    // 이어하기 카드가 보여줄 정보. 화면이 "합류할지 이어받을지" 바로 판단할 수 있게
    // 현재 붙어 있는 작업자 수(ActiveWorkerCount)까지 담는다
    // equipment WorkLog로 이관
    public record OpenWorkOrderResponse(
        int Id,
        int LineId,
        string LineName,
        int ProcessId,
        string ProcessName,
        int TargetQty,
        int CompletedQty,          // 완료된 이력들의 실적 합
        int ActiveWorkerCount);    // 지금 진행중/정지중인 인원 수 (0이어도 이어받기 가능)


    // 작업지시 수정 — 라인/공정 오선택 정정, 목표수량 조정용
    public class UpdateWorkOrderRequest
    {
        public int LineId { get; set; }
        public int ProcessId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "목표 수량은 1 이상이어야 합니다.")]
        public int TargetQty { get; set; }
    }

    // 수정(PUT) 응답. 화면이 라인/공정 이름을 다시 조회하지 않고 바로 갱신할 수 있게 이름까지 포함
    public record WorkOrderResponse(
        int Id,
        int LineId,
        string LineName,
        int ProcessId,
        string ProcessName,
        int TargetQty,
        int CompletedQty,      // 완료된 이력 실적 합 — 자동완료 판정에 쓴 값을 그대로 노출
        DateTime? CompletedAt);
}
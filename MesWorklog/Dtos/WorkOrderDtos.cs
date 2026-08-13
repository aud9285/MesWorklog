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
}
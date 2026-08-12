using System.ComponentModel.DataAnnotations;

namespace MesWorklog.Dtos
{

    // Category는 화면에서 계획정지/비가동 배지를 그리는 데 쓴다
    public record PauseReasonResponse(int Id, string Name, string Category);

}

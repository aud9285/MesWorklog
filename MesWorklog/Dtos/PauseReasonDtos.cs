using System.ComponentModel.DataAnnotations;

namespace MesWorklog.Dtos
{
    // record는 값을 담기만 하는 타입을 짧게 쓰는 문법
    // record는 Id/Name/Category 프로퍼티 + 생성자 + Equals + ToString 을 전부 만들어준다.
    // Category는 화면에서 계획정지/비가동 배지를 그리는 데 쓴다
    public record PauseReasonResponse(int Id, string Name, string Category);

}

using System.ComponentModel.DataAnnotations;

namespace MesWorklog.Dtos
{
    // 요청 DTO는 class로 — 검증 어트리뷰트를('[]') 붙이기 위함
    // [ApiController]가 붙은 컨트롤러는 이 검증을 자동으로 수행하고, 실패하면 400을 반환한다
    public class CreateWorkerRequest
    {
        // null, 빈문자열 방지
        [Required(ErrorMessage = "작업자명은 필수입니다.")]
        [MaxLength(100, ErrorMessage = "작업자명은 100자를 넘을 수 없습니다.")]
        public string Name { get; set; } = default!;

        // 이 작업자가 속할 공정 id 목록, 웹에서 멀티셀릭트 형식
        // null 대신 빈 리스트 기본값 서비스에서 null 체크 안해도됨
        public List<int> ProcessIds { get; set; } = new();
    }

    public class UpdateWorkerRequest
    {
        [Required(ErrorMessage = "작업자명은 필수입니다.")]
        [MaxLength(100, ErrorMessage = "작업자명은 100자를 넘을 수 없습니다.")]
        public string Name { get; set; } = default!;

        // 비활성화된 작업자를 다시 살릴 때 사용
        public bool IsActive { get; set; } = true;

        // 수정 후 이 작업자가 속해야 할 공정 id 목록
        public List<int> ProcessIds { get; set; } = new();
    }

    // record는 값을 담기만 하는 타입을 짧게 쓰는 문법
    // record는 Id/Name/IsActive 프로퍼티 + 생성자 + Equals + ToString 을 전부 만들어준다.
    public record WorkerResponse(int Id, string Name, bool IsActive, List<int> ProcessIds);

}

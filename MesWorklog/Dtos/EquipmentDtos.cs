using System.ComponentModel.DataAnnotations;

namespace MesWorklog.Dtos
{
    // 요청 DTO는 class로 — 검증 어트리뷰트를('[]') 붙이기 위함
    // [ApiController]가 붙은 컨트롤러는 이 검증을 자동으로 수행하고, 실패하면 400을 반환한다
    public class CreateEquipmentRequest
    {
        // null, 빈문자열 방지
        [Required(ErrorMessage = "설비명은 필수입니다.")]
        [MaxLength(100, ErrorMessage = "설비명은 100자를 넘을 수 없습니다.")]
        public string Name { get; set; } = default!;
    }

    public class UpdateEquipmentRequest
    {
        [Required(ErrorMessage = "설비명은 필수입니다.")]
        [MaxLength(100, ErrorMessage = "설비명은 100자를 넘을 수 없습니다.")]
        public string Name { get; set; } = default!;

        // 비활성화된 설비를 다시 살릴 때 사용
        public bool IsActive { get; set; } = true;
    }

    // record는 값을 담기만 하는 타입을 짧게 쓰는 문법
    // record는 Id/Name/IsActive 프로퍼티 + 생성자 + Equals + ToString 을 전부 만들어준다.
    public record EquipmentResponse(int Id, string Name, bool IsActive);

    
}

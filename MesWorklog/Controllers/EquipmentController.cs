using MesWorklog.Dtos;
using MesWorklog.Services;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Controllers
{
    // [ApiController]: Web API 전용 규약을 켜는 어트리뷰트
    // 자동 400에러 검증, [Frombody], [FromRoute], [FromQuery] 자동 적용
    [ApiController]

    // 이 컨트롤러의 공통 URL 접두사. 아래 액션들의 경로는 전부 여기에 이어붙는다
    [Route("api/equipments")]
    public class EquipmentController : ControllerBase
    {
        // 주입받은 서비스를 담아두는 필드. 생성자에서만 대입 가능
        private readonly EquipmentService _equipmentService;

        // 생성자 주입. DI 컨테이너가 요청마다 EquipmentService를 만들어 넣어준다
        public EquipmentController(EquipmentService equipmentService)
        {
            _equipmentService = equipmentService;
        }

        // HTTP GET 프로토콜로 요청시 
        // GET /api/equipments?includeInactive=false
        [HttpGet]
        public async Task<ActionResult<List<EquipmentResponse>>> GetAll(
            // includeInactive: 비활성 설비까지 포함할지 여부
            // 기본값 false — 호출하는 쪽은 활성 설비만 받게
            // true - 비활성화 설비도 호출
            // 현재는 작업이력이 있어 비활성화 되었을 경우 다시 복구하는 웹 flow가 없음 
            bool includeInactive = false)
        {
            var equipments = await _equipmentService.GetAllAsync(includeInactive);

            return Ok(equipments);
        }

        // GET /api/equipments/?
        // URL 경로 매개변수 id로 정의 int형만 매칭제한
        // string 으로 오면 이 액션에 아예 매칭되지 않고 404가 나간다
        [HttpGet("{id:int}")]
        public async Task<ActionResult<EquipmentResponse>> GetById(
            // id: 조회할 설비의 PK. URL 경로에서 값을 꺼낸다
            int id)
        {
            // 못찾으면 미들웨어에서 404 ProblemDetails로 변환한다 → 여기서 null 체크 불필요
            return Ok(await _equipmentService.GetByIdAsync(id));
        }

        // POST /api/equipments
        [HttpPost]
        public async Task<ActionResult<EquipmentResponse>> Create(
            // request: 생성할 설비 정보, 복합타입 이라 [FromBody]
            CreateEquipmentRequest request)
        {
            var created = await _equipmentService.CreateAsync(request);

            // CreatedAtAction: 201 Created + Location 헤더(= 방금 만든 리소스의 GET URL) + 본문
            // CreatedAtAction은 라우팅 테이블에서 역산하므로 자동으로 맞춰진다.
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // PUT /api/equipments/?
        [HttpPut("{id:int}")]
        public async Task<ActionResult<EquipmentResponse>> Update(
            // id: 수정 대상 설비의 PK (URL 경로)
            int id,
            // request: 새 이름(Name)과 활성 여부(IsActive)
            UpdateEquipmentRequest request)
        {
            return Ok(await _equipmentService.UpdateAsync(id, request));
        }

        // DELETE /api/equipments/?
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<DeleteResult>> Delete(
            // id: 삭제 대상 설비의 PK
            int id)
        {
            var result = await _equipmentService.DeleteAsync(id);

            // 204 No Content가 아니라 200 + 본문인 이유
            // 실제 삭제와 비활성화 중 어느 쪽이 일어났는지 화면이 알아야
            // "삭제되었습니다" / "작업이력 120건이 있어 비활성 처리했습니다" 토스트를 구분해 띄울 수 있음
            return Ok(result);
        }
    }
}

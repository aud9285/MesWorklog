/* 화면 확인용 임시 데이터.
 *
 * ⚠ 연동 시 이 파일은 통째로 지우고, 각 컴포넌트 상단의 `TODO(연동)` 블록에 적힌
 *   api.* 호출로 교체하면 된다. 필드 이름은 백엔드 DTO 기준으로 맞춰두었다.
 *
 * 상태/분류 문자열은 EF Core가 enum을 이름 그대로 varchar로 저장하므로
 * "InProgress" / "Paused" / "Completed", "Planned" / "Unplanned" 가 실제 값이다.
 */

export const lines = [
  { id: 1, name: '1라인', isActive: true },
  { id: 2, name: '2라인', isActive: true },
  { id: 3, name: '3라인(구)', isActive: false },
];

export const processes = [
  { id: 1, name: '조립', isActive: true, lineIds: [1, 2] },
  { id: 2, name: '검사', isActive: true, lineIds: [1] },
  { id: 3, name: '포장', isActive: true, lineIds: [2] },
  { id: 4, name: '도장', isActive: false, lineIds: [3] },
];

export const workers = [
  { id: 1, name: '김철수', isActive: true, processIds: [1, 2] },
  { id: 2, name: '이영희', isActive: true, processIds: [1, 2] },
  { id: 3, name: '박민수', isActive: true, processIds: [2, 3] },
  { id: 4, name: '정다은', isActive: true, processIds: [3] },
  { id: 5, name: '최former', isActive: false, processIds: [1] },
];

export const equipments = [
  { id: 1, name: '조립기 A', isActive: true },
  { id: 2, name: '검사기 B', isActive: true },
  { id: 3, name: '포장기 C', isActive: true },
  { id: 4, name: '폐기설비 D', isActive: false },
];

export const pauseReasons = [
  { id: 1, name: '식사', category: 'Planned' },
  { id: 2, name: '정기점검', category: 'Planned' },
  { id: 3, name: '교대', category: 'Planned' },
  { id: 4, name: '설비 고장', category: 'Unplanned' },
  { id: 5, name: '자재 대기', category: 'Unplanned' },
  { id: 6, name: '품질 이상', category: 'Unplanned' },
];

/* 이어하기용 미완료 작업지시 (CompletedAt == null) */
export const openWorkOrders = [
  {
    id: 101, lineId: 1, lineName: '1라인', processId: 1, processName: '조립',
    equipmentId: 1, equipmentName: '조립기 A',
    targetQty: 500, completedQty: 320, activeWorkerCount: 2,
  },
  {
    id: 102, lineId: 1, lineName: '1라인', processId: 2, processName: '검사',
    equipmentId: 2, equipmentName: '검사기 B',
    // WorkLog 가 0건인 WorkOrder 는 존재할 수 없다 — 시작 시점에 함께 생성되고(§4-4),
    // 마지막 WorkLog 를 지우면 WorkOrder 도 함께 삭제되기 때문(§5)
    targetQty: 300, completedQty: 60, activeWorkerCount: 1,
  },
  {
    id: 103, lineId: 2, lineName: '2라인', processId: 3, processName: '포장',
    equipmentId: null, equipmentName: null,
    // 앞사람이 끝냈지만 목표 미달 — 진행중인 사람이 0명이어도 이어받을 수 있다
    targetQty: 200, completedQty: 140, activeWorkerCount: 0,
  },
];

/* 작업자별 현재 진행중 작업.
 * 한 작업자에게 활성(InProgress/Paused) 건은 최대 1개다 — 서버가 중복 시작을 거부하므로(§8-5).
 * 여기 없는 작업자(3,4,5)는 활성 건이 없어 "작업 시작" 폼이 뜬다. */
export const activeLogsByWorker = {
  1: {
    id: 9001, workOrderId: 101, workerId: 1, workerName: '김철수',
    lineName: '1라인', processName: '조립', equipmentName: '조립기 A',
    targetQty: 500, completedQty: 320, status: 'Paused', startTime: '2026-08-09T09:00:00',
    pauses: [
      { id: 1, reasonName: '식사', category: 'Planned', pausedAt: '2026-08-09T12:00:00', resumedAt: '2026-08-09T13:00:00' },
      { id: 2, reasonName: '설비 고장', category: 'Unplanned', pausedAt: '2026-08-09T15:20:00', resumedAt: null },
    ],
  },
  2: {
    id: 9002, workOrderId: 102, workerId: 2, workerName: '이영희',
    lineName: '1라인', processName: '검사', equipmentName: '검사기 B',
    targetQty: 300, completedQty: 60, status: 'InProgress', startTime: '2026-08-09T10:00:00',
    pauses: [
      { id: 3, reasonName: '자재 대기', category: 'Unplanned', pausedAt: '2026-08-09T13:00:00', resumedAt: '2026-08-09T13:25:00' },
    ],
  },
};

/* 상세조회 목록 — 특정 날짜의 작업이력.
 * 완료 건은 캐시 3개가 채워져 있고, 진행중/정지중 건은 null 이다(§3-5).
 * 진행중 건도 목록에 보여야 종료를 잊고 방치된 이력을 찾아 지울 수 있다(§8-3). */
export const workLogsByDate = [
  {
    id: 9000, workOrderId: 100, workerId: 1, workerName: '김철수',
    lineName: '1라인', processName: '조립', equipmentName: '조립기 A',
    status: 'Completed', startTime: '2026-08-09T09:00:00', endTime: '2026-08-09T18:00:00',
    actualQty: 480, elapsedMinutes: 540, operatingMinutes: 480, netOperatingMinutes: 425,
  },
  {
    id: 9003, workOrderId: 103, workerId: 3, workerName: '박민수',
    lineName: '1라인', processName: '검사', equipmentName: '검사기 B',
    status: 'Completed', startTime: '2026-08-09T09:00:00', endTime: '2026-08-09T17:30:00',
    actualQty: 290, elapsedMinutes: 510, operatingMinutes: 450, netOperatingMinutes: 396,
  },
  {
    id: 9004, workOrderId: 104, workerId: 4, workerName: '정다은',
    lineName: '2라인', processName: '포장', equipmentName: null,
    status: 'Completed', startTime: '2026-08-09T08:30:00', endTime: '2026-08-09T16:00:00',
    actualQty: 195, elapsedMinutes: 450, operatingMinutes: 390, netOperatingMinutes: 250,
  },
  {
    id: 9002, workOrderId: 102, workerId: 2, workerName: '이영희',
    lineName: '1라인', processName: '검사', equipmentName: '검사기 B',
    status: 'InProgress', startTime: '2026-08-09T10:00:00', endTime: null,
    actualQty: 0, elapsedMinutes: null, operatingMinutes: null, netOperatingMinutes: null,
  },
  {
    id: 9001, workOrderId: 101, workerId: 1, workerName: '김철수',
    lineName: '1라인', processName: '조립', equipmentName: '조립기 A',
    status: 'Paused', startTime: '2026-08-09T09:00:00', endTime: null,
    actualQty: 0, elapsedMinutes: null, operatingMinutes: null, netOperatingMinutes: null,
  },
  /* 이어하기로 같은 작업지시(#101)를 나눠 수행한 건.
   * 위 9001(김철수, 진행중)과 workOrderId 가 같아서 서로 "형제 이력"이 된다.
   * 이 상태에서 한쪽 카드의 작업지시를 수정하면 다른 쪽 표시도 함께 바뀐다(§4-4) */
  {
    id: 9005, workOrderId: 101, workerId: 2, workerName: '이영희',
    lineName: '1라인', processName: '조립', equipmentName: '조립기 A',
    status: 'Completed', startTime: '2026-08-09T07:00:00', endTime: '2026-08-09T12:00:00',
    actualQty: 320, elapsedMinutes: 300, operatingMinutes: 270, netOperatingMinutes: 246,
  },
];

/* 목록에서 고른 건의 상세 (정지 이력 포함).
 * TODO(연동) 실제로는 GET /api/work-logs/{id} 로 건별 조회한다.
 *   여기서는 id 를 키로 하는 맵으로 흉내낸다. */
export const workLogDetails = {
  9000: {
    workOrderId: 100, lineId: 1, processId: 1, targetQty: 500,
    pauses: [
      { id: 11, reasonName: '식사', category: 'Planned', pausedAt: '2026-08-09T12:00:00', resumedAt: '2026-08-09T13:00:00' },
      { id: 12, reasonName: '설비 고장', category: 'Unplanned', pausedAt: '2026-08-09T14:10:00', resumedAt: '2026-08-09T14:45:00' },
      { id: 13, reasonName: '자재 대기', category: 'Unplanned', pausedAt: '2026-08-09T16:00:00', resumedAt: '2026-08-09T16:20:00' },
    ],
  },
  9003: {
    workOrderId: 103, lineId: 1, processId: 2, targetQty: 300,
    pauses: [
      { id: 21, reasonName: '식사', category: 'Planned', pausedAt: '2026-08-09T12:00:00', resumedAt: '2026-08-09T13:00:00' },
      { id: 22, reasonName: '품질 이상', category: 'Unplanned', pausedAt: '2026-08-09T15:00:00', resumedAt: '2026-08-09T15:54:00' },
    ],
  },
  9004: {
    workOrderId: 104, lineId: 2, processId: 3, targetQty: 200,
    pauses: [
      { id: 31, reasonName: '식사', category: 'Planned', pausedAt: '2026-08-09T12:00:00', resumedAt: '2026-08-09T13:00:00' },
      { id: 32, reasonName: '설비 고장', category: 'Unplanned', pausedAt: '2026-08-09T13:30:00', resumedAt: '2026-08-09T15:50:00' },
    ],
  },
  9002: {
    workOrderId: 102, lineId: 1, processId: 2, targetQty: 400,
    pauses: [
      { id: 41, reasonName: '자재 대기', category: 'Unplanned', pausedAt: '2026-08-09T13:00:00', resumedAt: '2026-08-09T13:25:00' },
    ],
  },
  9001: {
    workOrderId: 101, lineId: 1, processId: 1, targetQty: 600,
    pauses: [
      { id: 51, reasonName: '식사', category: 'Planned', pausedAt: '2026-08-09T12:00:00', resumedAt: '2026-08-09T13:00:00' },
      { id: 52, reasonName: '설비 고장', category: 'Unplanned', pausedAt: '2026-08-09T15:20:00', resumedAt: null },
    ],
  },
  // 9001 과 같은 작업지시(#101) — 이어하기로 나눠 수행한 형제 이력. targetQty 는 작업지시
  // 소유값이라 형제끼리 항상 같다(§4-4) — 이 mock 도 그 불변식을 그대로 반영한다
  9005: {
    workOrderId: 101, lineId: 1, processId: 1, targetQty: 600,
    pauses: [
      { id: 61, reasonName: '자재 대기', category: 'Unplanned', pausedAt: '2026-08-09T09:30:00', resumedAt: '2026-08-09T09:54:00' },
      { id: 62, reasonName: '정기점검', category: 'Planned', pausedAt: '2026-08-09T10:30:00', resumedAt: '2026-08-09T11:00:00' },
    ],
  },
};

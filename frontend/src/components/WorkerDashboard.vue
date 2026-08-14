<script setup>
/* ════════════════════════════════════════════════════════════════
 * 현장 작업 화면 (설계 §6-1)
 *
 * 작업자가 실제로 쓰는 화면. 시작 → 일시정지 → 재개 → 완료 흐름을 담당한다.
 *
 * 【화면 흐름】
 *   ① 작업자 선택 (화면 주체)
 *   ② 그 작업자의 활성(진행중/정지중) 건을 조회
 *        있으면 → 진행중 카드 (정지 / 재개 / 완료 / 삭제)
 *        없으면 → 작업 시작 카드
 *   ③ 시작 카드는 "이어하기" 토글로 두 모드
 *        OFF(신규)  : 라인 → 공정 → 설비(선택) → 목표수량   ⇒ 작업지시를 새로 만들며 시작
 *        ON(이어하기): 미완료 작업지시 목록에서 선택          ⇒ 기존 작업지시에 합류
 *
 * 【작업자를 맨 앞에 두는 이유】
 *   "누구의 진행중 작업인가"를 알아야 ②를 판단할 수 있는데,
 *   작업자를 시작 폼 안에서 고르면 그 판단을 할 수 없다(닭-달걀).
 *   현장에서도 작업자가 자기 이름을 고르고 쓰는 화면이라 이 순서가 자연스럽다.
 *   라인 → 공정 캐스케이딩은 그대로 유지되므로 §4-8의 조합 차단 효과는 그대로다.
 *
 * 【한 작업자당 활성 건은 최대 1개】 (§8-5)
 *   활성 건이 있으면 시작 카드를 아예 감춰서 중복 시작을 화면에서 1차 차단한다.
 *   서버도 같은 검사를 하므로 Swagger 직접 호출로는 뚫리지 않는다.
 *
 * 【서버가 최종 방어선이라는 전제】
 *   아래 UI 차단은 전부 사용자 경험용이다. 데이터 정합성은 서버가 보장한다.
 *   - 정지중 완료 버튼 비활성화 → 서버도 InProgress 에서만 완료 허용 (§4-9)
 *   - 라인→공정 캐스케이딩     → 서버도 LineProcess 조합 검증 (§4-8)
 *
 * ────────────────────────────────────────────────────────────────


/* ── 사용하는 PrimeVue 위젯 ──────────────────────────────────
 * Card        : 제목/본문/푸터 슬롯을 가진 카드 컨테이너 (#title #content #footer)
 * Checkbox    : 체크박스. binary 를 주면 값이 배열이 아니라 true/false 로 오간다
 * Select      : 드롭다운(셀렉트 박스). 4.x 에서 Dropdown → Select 로 이름이 바뀌었다
 *               options=목록, optionLabel=화면에 보일 필드, optionValue=v-model 에 담을 필드
 *               disabled 로 잠그면 캐스케이딩(상위 미선택 시 하위 비활성)이 된다
 *               showClear 는 선택 해제(X) 버튼 → 설비처럼 nullable 인 항목에 사용
 *               #option 슬롯으로 항목 생김새를 직접 그린다 → 정지사유에 분류 배지 표시
 * InputText   : 한 줄 텍스트 입력. readonly 를 주면 편집은 막고 값 표시 용도로만 쓴다
 *               (시각 선택 팝업에서 시작 시각을 참고용으로 보여줄 때 사용)
 * InputNumber : 숫자 전용 입력. showButtons 로 +/- 스피너, min 으로 하한
 * Button      : 버튼. severity=색(warn/success/danger), text=배경 없음, icon=아이콘
 * Tag         : 상태 배지. severity 에 따라 색이 바뀐다 (진행중=info, 정지중=warn, 완료=success)
 * Dialog      : 모달 팝업. v-model:visible 로 열고 닫는다
 *               modal=배경을 덮고 뒤쪽 조작 차단, draggable=false 로 창 끌기 비활성
 *               #footer 슬롯에 확인/취소 버튼을 넣는다
 * DatePicker  : 달력 + 시각 선택기. 4.x 에서 Calendar → DatePicker 로 이름이 바뀌었다
 *               showTime=시:분 추가, hourFormat="24"=0~23시,
 *               stepMinute=10 → 분이 10분 단위로만 증감 (§3-1 의 입력 규칙)
 *               showIcon + iconDisplay="input" 은 입력칸 안쪽에 달력 아이콘을 붙인다
 *               fluid 는 부모 폭에 맞춰 늘린다
 * useToast    : 우상단 알림. App.vue 의 <Toast /> + main.js 의 ToastService 등록이 전제
 * ─────────────────────────────────────────────────────────── */
import Card from 'primevue/card';
import Checkbox from 'primevue/checkbox';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import InputNumber from 'primevue/inputnumber';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Dialog from 'primevue/dialog';
import DatePicker from 'primevue/datepicker';

import ConfirmDeleteDialog from './common/ConfirmDeleteDialog.vue';
import { ref, watch, computed, onMounted } from 'vue';

import {
  hhmm, mmddhhmm, duration, minutesBetween, toLocalIso, floorTo10Minutes,
  STATUS_LABEL, STATUS_SEVERITY, CATEGORY_LABEL,
} from '../utils/format.js';
import { useToast } from 'primevue/usetoast';

import {api} from '../client.js';

const toast = useToast();

/* ── 마스터 데이터 ───────────────────────────────────────────
 * onMounted 에서 api 호출 결과로 채운다 */
const workers = ref([]);
const lines = ref([]);
const allProcesses = ref([]);
const equipments = ref([]);
const pauseReasons = ref([]);
// 이어하기 목록은 workerId 기준이라 작업자를 고를때마다 불러옴
const openOrders = ref([]);

const loading = ref(false);

/* ══ ① 작업자 선택 ═══════════════════════════════════════ */
const workerId = ref(null);

const selectedWorker = computed(
  () => workers.value.find((w) => w.id === workerId.value) ?? null,
);

/* ══ ② 그 작업자의 활성 건 ═══════════════════════════════
 * null 이면 "작업 시작" 카드가, 값이 있으면 "진행중 작업" 카드가 보인다.
 *   watch(workerId) 에서 GET /api/work-logs/active?workerId= 결과를 대입한다 */
const activeLog = ref(null);

/* 작업자 선택 시 + 시작/정지/재개/완료/삭제 직후에 호출해 화면을 최신 상태로 맞춘다 */
async function loadActiveLog() {
  if (!workerId.value) {
    activeLog.value = null;
    return;
  }
  try {
    activeLog.value = await api.getActiveWorkLog(workerId.value);
  } catch (err) {
    activeLog.value = null;
    toast.add({
      severity: 'error', summary: '작업 상태를 불러오지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

/* 이어하기 목록도 작업자 기준이라 같이 갱신한다 */
async function loadOpenOrders() {
  if (!workerId.value) {
    openOrders.value = [];
    return;
  }
  try {
    openOrders.value = await api.getOpenWorkOrders(workerId.value);
  } catch (err) {
    openOrders.value = [];
    toast.add({
      severity: 'error', summary: '이어할 작업을 불러오지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

const isPaused = computed(() => activeLog.value?.status === 'Paused');
const isRunning = computed(() => activeLog.value?.status === 'InProgress');

/* 아직 재개되지 않은 "열린 정지" — 정지중일 때 사유를 보여주는 데 쓴다 */
const openPause = computed(
  () => activeLog.value?.pauses.find((p) => p.resumedAt === null) ?? null,
);

/* 완료 전이라 서버 캐시값이 없으므로 현재 시각 기준으로 경과를 계산한다 */
const elapsedMinutes = computed(() =>
  activeLog.value ? minutesBetween(activeLog.value.startTime, null) : 0,
);

/* 목록 API는 기본이 활성 항목만 반환하므로(includeInactive=false)
 * 예전 mock에서 하던 .filter(isActive)가 더 이상 필요 없다 */
async function loadMasters() {
  try {
    // 서로 의존하지 않는 요청이라 병렬로 보낸다 (순차로 하면 왕복 시간이 4배)
    const [w, l, p, e, r] = await Promise.all([
      api.getWorkers(),
      api.getLines(),
      api.getProcesses(),
      api.getEquipments(),
      api.getPauseReasons(),
    ]);
    workers.value = w;
    lines.value = l;
    allProcesses.value = p;
    equipments.value = e;
    pauseReasons.value = r;
  } catch (err) {
    toast.add({
      severity: 'error', summary: '기본 정보를 불러오지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

onMounted(loadMasters);

/* ══ ③ 시작 폼 ═══════════════════════════════════════════ */
const continueMode = ref(false);   // 이어하기 토글
const lineId = ref(null);
const processId = ref(null);
const equipmentId = ref(null);
const targetQty = ref(100);
const selectedOrderId = ref(null);

/* 작업자가 바뀌면 이전 선택은 의미가 없으므로 폼을 비우고, 그 작업자 기준 데이터를 다시 불러온다 */
watch(workerId, async () => {
  lineId.value = null;
  processId.value = null;
  equipmentId.value = null;
  selectedOrderId.value = null;

  loading.value = true;
  // 서로 독립적이라 병렬로
  await Promise.all([loadActiveLog(), loadOpenOrders()]);
  loading.value = false;
});

/* 공정 후보 = 선택한 라인에 속하면서(§4-8) + 이 작업자가 배정된 공정(§4-7)
 * 두 조건을 모두 만족해야 한다. 배정되지 않은 공정으로 시작하면
 * "누가 어느 공정에서 일하는가"라는 마스터데이터와 실적이 어긋난다. */
const processOptions = computed(() => {
  if (!lineId.value || !selectedWorker.value) return [];
  return allProcesses.value.filter(
    (p) => p.isActive
      && p.lineIds.includes(lineId.value)
      && selectedWorker.value.processIds.includes(p.id),
  );
});

/* 이어하기 목록도 이 작업자가 배정된 공정의 미완료 작업지시만 보여준다 */
const myOpenOrders = computed(() => openOrders.value);

/* 라인이 바뀌면 하위 선택을 비운다 — 남아 있으면 유효하지 않은 조합이 만들어진다 */
function onLineChange() {
  processId.value = null;
}

const canStart = computed(() => {
  if (!workerId.value) return false;
  return continueMode.value
    ? !!selectedOrderId.value
    : !!(lineId.value && processId.value && targetQty.value > 0);
});

/* ══ 시각 선택 다이얼로그 ════════════════════════════════
 * start / pause / resume / complete 네 동작이 같은 팝업을 공유한다 */
const timeDialog = ref(false);
const timeMode = ref('start');
const pickedTime = ref(floorTo10Minutes());
const pickedReasonId = ref(null);   // pause 일 때만 사용

/* 달력의 상한. 서버가 미래 시각을 거부하므로(§3-3) 화면에서 먼저 막는다.
 * DatePicker 는 maxDate 가 같은 날짜면 시·분 스피너까지 제한하므로(validateTime),
 * 이 한 줄로 "미래 날짜"와 "미래 시각"이 동시에 차단된다.
 * 팝업을 열 때마다 갱신해야 시간이 흐른 만큼 상한도 따라 올라간다 */
const maxTime = ref(new Date());

const timeDialogHeader = computed(() => ({
  start: '작업 시작 시각',
  pause: '일시정지 시각 · 사유',
  resume: '재개 시각',
  complete: '작업 종료 시각',
}[timeMode.value]));

function openTimeDialog(mode) {
  /* 정지중 완료 시도 차단 — 열린 정지가 남은 채 완료되면 가동률이 부풀려진다 (§4-9).
   * 버튼을 비활성화해뒀지만, 안내 없이 막기만 하면 사용자가 이유를 모른다 */
  if (mode === 'complete' && isPaused.value) {
    toast.add({
      severity: 'warn', summary: '정지 중입니다',
      detail: '재개 후 완료해주세요.', life: 3000,
    });
    return;
  }
  timeMode.value = mode;
  /* 기본값을 10분 단위로 내려서 넣는다 (§3-1).
   * stepMinute=10 은 증감 단위일 뿐이라, new Date() 를 그대로 넣으면 14:37 로 열리고
   * 사용자가 분을 안 건드리면 10분 단위가 아닌 값이 그대로 전송된다.
   * 올림이 아니라 내림인 이유는 올리면 현재보다 미래가 되어 서버에서 거부되기 때문 */
  pickedTime.value = floorTo10Minutes();
  maxTime.value = new Date();
  pickedReasonId.value = null;
  timeDialog.value = true;
}

/* pause 모드에서는 사유를 골라야 확인 버튼이 열린다 */
const canConfirmTime = computed(
  () => timeMode.value !== 'pause' || !!pickedReasonId.value,
);

async function confirmTime() {
  const iso = toLocalIso(pickedTime.value);

  if (timeMode.value === 'complete') {
    // 완료는 2단계 — 종료 시각을 확정한 뒤 실적 수량 입력으로 넘어간다
    timeDialog.value = false;
    qtyDialog.value = true;
    return;
  }

  try {
    if (timeMode.value === 'start') {
      // 이어하기 여부에 따라 payload 모양이 다르다
      // 이어하기 : 기존 작업지시에 합류 → workOrderId + 설비(이번 세션 것)
      // 신규 : 작업지시를 새로 만들며 시작 → 라인·공정·설비·목표수량
      // 설비는 두 경우 다 필요하다 — WorkOrder가 아니라 WorkLog(세션) 소속이라
      // 이어하기로 합류해도 "이번에 내가 쓸 설비"는 매번 새로 골라야 한다
      const payload = continueMode.value
        ? {
            workerId: workerId.value,
            startTime: iso,
            workOrderId: selectedOrderId.value,
            equipmentId: equipmentId.value,
          }
        : {
            workerId: workerId.value,
            startTime: iso,
            lineId: lineId.value,
            processId: processId.value,
            equipmentId: equipmentId.value,   // 수작업이면 null 그대로 보냄
            targetQty: targetQty.value,
          };

      await api.startWorkLog(payload);
    }
    else if (timeMode.value === 'pause') {
      // 정지 대상은 지금 화면에 떠 있는 활성 건.
      // 사유는 다이얼로그에서 고른 값(pickedReasonId) — canConfirmTime이 미선택을 막아준다
      await api.pauseWorkLog(activeLog.value.id, iso, pickedReasonId.value);
    }
    else if (timeMode.value === 'resume') {
      // 재개는 시각만 보내면 된다. 어느 정지를 닫을지는 서버가 판단한다
      // (WorkLog.Resume이 ResumedAt == null인 열린 정지를 찾아서 닫음)
      await api.resumeWorkLog(activeLog.value.id, iso);
    }

    // 성공했으면 상태가 바뀌었으므로 다시 조회 → 카드 버튼 구성이 바뀐다
    await loadActiveLog();

    const done = {
      start: '작업을 시작했습니다.',
      pause: '작업을 일시정지했습니다.',
      resume: '작업을 다시 시작했습니다.',
    }[timeMode.value];

    toast.add({
      severity: 'success',
      summary: done,
      detail: `${hhmm(iso)} 기준으로 기록되었습니다.`,
      life: 2500,
    });
    timeDialog.value = false;
  } catch (err) {
    // 409(중복 시작, 조합 오류) / 400(미래 시각) 문구가 err.message에 담겨 온다
    toast.add({
      severity: 'error', summary: '처리하지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

/* ══ 완료 2단계: 실적 수량 ═══════════════════════════════ */
const qtyDialog = ref(false);
const actualQty = ref(0);

/* 이번에 입력한 수량까지 더했을 때 목표를 얼마나 넘기는지.
 * 초과 자체는 정상일 수 있어(불량 포함 계수 등) 막지는 않고 경고만 한다.
 * 다만 목표 500 · 누적 480 인데 300 을 치는 건 대개 오타라서 알려줄 필요가 있다.
 * 서버의 Complete() 에는 actualQty 검증이 아예 없으므로 DTO 에 [Range(0, ...)] 를 걸어야 한다 */
const qtyOver = computed(() => {
  if (!activeLog.value) return 0;
  const total = (activeLog.value.completedQty ?? 0) + (actualQty.value ?? 0);
  return Math.max(0, total - activeLog.value.targetQty);
});

async function confirmComplete() {
  try {
    // 종료 시각은 앞 다이얼로그에서 고른 pickedTime, 실적 수량은 이 다이얼로그의 actualQty
    await api.completeWorkLog(
      activeLog.value.id,
      toLocalIso(pickedTime.value),
      actualQty.value,
    );

    toast.add({
      severity: 'success',
      summary: '작업을 완료했습니다.',
      detail: `${hhmm(toLocalIso(pickedTime.value))} 종료 · 실적 ${actualQty.value}개`,
      life: 3000,
    });

    qtyDialog.value = false;
    actualQty.value = 0;

    // 완료되면 활성 건이 사라지므로 null이 되고, 화면이 "작업 시작" 카드로 돌아간다.
    // 이어하기 목록도 갱신 — 이 작업으로 목표를 채웠다면 그 작업지시가 목록에서 빠져야 한다
    await Promise.all([loadActiveLog(), loadOpenOrders()]);
  } catch (err) {
    // 정지중 완료 시도(409), 시간 역전(400) 등
    toast.add({
      severity: 'error', summary: '완료하지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

/* ══ 삭제 ════════════════════════════════════════════════
 * 오입력이나 종료를 잊고 방치된 이력을 지우는 경로 (§8-3) */
const deleteDialog = ref(false);

async function confirmDelete() {
  try {
    await api.deleteWorkLog(activeLog.value.id);

    toast.add({
      severity: 'success',
      summary: '작업 기록을 삭제했습니다.',
      life: 2500,
    });

    deleteDialog.value = false;

    // 삭제됐으니 활성 건이 사라진다.
    // 형제 이력이 없었다면 작업지시도 함께 지워졌으므로 이어하기 목록도 갱신
    await Promise.all([loadActiveLog(), loadOpenOrders()]);
  } catch (err) {
    toast.add({
      severity: 'error', summary: '삭제하지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}
</script>

<template>
  <div class="col g-4">

    <!-- ══ ① 작업자 선택 — 이 화면의 주체 ═══════════════ -->
    <Card>
      <template #content>
        <div class="row wrap g-3 between">
          <div class="field" style="min-width: 200px">
            <label>작업자</label>
            <Select v-model="workerId" :options="workers" optionLabel="name" optionValue="id"
                    placeholder="작업자를 선택하세요" filter
                    emptyMessage="등록된 작업자가 없습니다."
                    emptyFilterMessage="일치하는 작업자가 없습니다." />
          </div>

          <!-- 선택한 작업자의 현재 상태를 한눈에 -->
          <div v-if="workerId" class="row g-2" style="align-self: flex-end; padding-bottom: 6px">
            <Tag v-if="activeLog" :value="STATUS_LABEL[activeLog.status]"
                 :severity="STATUS_SEVERITY[activeLog.status]" />
            <span v-else class="hint">진행중인 작업이 없습니다. 새로 시작할 수 있습니다.</span>
          </div>
        </div>
      </template>
    </Card>

    <!-- 작업자를 고르기 전에는 아무것도 판단할 수 없다 -->
    <div v-if="!workerId" class="empty">
      위에서 본인 이름을 선택해 주세요.
    </div>

    <!-- ══ ③ 작업 시작 (활성 건이 없을 때만) ════════════ -->
    <Card v-else-if="!activeLog">
      <template #title>
        <div class="row between wrap g-2">
          <span>작업 시작</span>
          <!-- 이어하기: 이미 만들어진 작업지시에 합류.
               끄면 작업지시를 새로 만들면서 시작한다 (§4-4) -->
          <label class="row g-2 continue-toggle">
            <Checkbox v-model="continueMode" binary inputId="continueMode" />
            <span>이어하기</span>
          </label>
        </div>
      </template>

      <template #content>
        <!-- 설비는 WorkOrder가 아니라 WorkLog(세션) 소속이라(§4-16 — 고장/교체 대응),
             신규 생성이든 이어하기든 "이번 세션에 쓸 설비"는 항상 골라야 한다.
             그래서 이어하기 on/off와 무관하게 항상 노출한다. 수작업 공정도 있어 nullable이다 -->
        <div class="field" style="max-width: 240px; margin-bottom: 14px">
          <label>설비 <span class="opt">(선택)</span></label>
          <Select v-model="equipmentId" :options="equipments" optionLabel="name" optionValue="id"
                  placeholder="수작업" showClear
                  emptyMessage="등록된 설비가 없습니다." />
        </div>

        <!-- ── 이어하기 OFF: 신규 작업지시 생성 ── -->
        <div v-if="!continueMode" class="row wrap g-3">
          <div class="field">
            <label>라인</label>
            <Select v-model="lineId" :options="lines" optionLabel="name" optionValue="id"
                    placeholder="라인 선택" @change="onLineChange"
                    emptyMessage="등록된 라인이 없습니다." />
          </div>

          <!-- 라인을 먼저 고르지 않으면 공정을 못 고른다 → 잘못된 조합 자체가 선택 불가 (§4-8)
               목록에는 이 작업자가 배정된 공정만 나온다 (§4-7) -->
          <div class="field">
            <label>공정</label>
            <Select v-model="processId" :options="processOptions" optionLabel="name" optionValue="id"
                    placeholder="공정 선택" :disabled="!lineId"
                    emptyMessage="선택 가능한 공정이 없습니다." />
          </div>

          <div class="field" style="min-width: 130px">
            <label>목표 수량</label>
            <InputNumber v-model="targetQty" :min="1" showButtons />
          </div>
        </div>

        <!-- 라인은 골랐는데 배정된 공정이 없는 경우 — 정상 상태이므로 에러가 아닌 안내 -->
        <p v-if="!continueMode && lineId && !processOptions.length" class="hint mt-2">
          {{ selectedWorker?.name }} 님이 이 라인에서 맡은 공정이 없습니다.
          관리자에게 공정 배정을 요청해 주세요.
        </p>

        <!-- ── 이어하기 ON: 미완료 작업지시 목록 ── -->
        <div v-if="continueMode" class="col g-3">
          <p class="hint" style="margin: 0">
            아직 목표 수량을 채우지 못한 작업입니다. 여러 명이 함께 진행할 수 있습니다.
          </p>

          <div v-if="!myOpenOrders.length" class="empty">
            이어서 진행할 수 있는 작업이 없습니다. 이어하기를 끄고 새 작업을 시작해 주세요.
          </div>

          <div v-else class="order-list">
            <button
              v-for="order in myOpenOrders" :key="order.id"
              class="order-card" :class="{ picked: selectedOrderId === order.id }"
              @click="selectedOrderId = order.id"
            >
              <div class="row between">
                <strong>{{ order.lineName }} · {{ order.processName }}</strong>
                <span class="order-id num">#{{ order.id }}</span>
              </div>
              <!-- "진행중 N명"은 지금 붙어 있는 사람 수다. 0명이면 앞사람이 끝낸 걸 이어받는 것이고,
                   1명 이상이면 병렬로 합류하는 것 — 둘 다 정상 시나리오다(§4-6).
                   설비는 세션(WorkLog)마다 다를 수 있어 여기엔 표시하지 않는다(위에서 이번 세션 설비를 따로 고름) -->
              <div class="order-meta">
                <span v-if="order.activeWorkerCount">진행중 {{ order.activeWorkerCount }}명</span>
                <span v-else>진행중인 작업자 없음</span>
              </div>
              <!-- 가장 최근 세션의 시작~종료 + 누가 했는지. 진행중이면 종료가 없어 "~"로 열어둔다.
                   여러 명이 거쳐간 작업일 수 있어 "이 작업지시의 시간"이 아니라
                   "가장 최근에 무슨 일이 있었는지"를 보여주는 참고 정보다 -->
              <div class="order-time num">
                {{ mmddhhmm(order.lastStartTime) }} ~
                <span v-if="order.lastEndTime">{{ hhmm(order.lastEndTime) }}</span>
                <span v-else class="order-time-open">진행 중</span>
                <span class="order-time-worker">· {{ order.lastWorkerName }}</span>
              </div>
              <!-- 누적 실적 / 목표 — 얼마나 남았는지가 선택 기준이 된다 -->
              <div class="progress">
                <div class="progress-fill"
                     :style="{ width: Math.min(100, (order.completedQty / order.targetQty) * 100) + '%' }" />
              </div>
              <div class="order-qty num">{{ order.completedQty }} / {{ order.targetQty }}개</div>
            </button>
          </div>
        </div>
      </template>

      <template #footer>
        <Button label="작업 시작" icon="pi pi-play" :disabled="!canStart"
                @click="openTimeDialog('start')" />
      </template>
    </Card>

    <!-- ══ ② 진행중 작업 ═══════════════════════════════ -->
    <Card v-else>
      <template #title>
        <div class="row between wrap g-2">
          <span>{{ activeLog.lineName }} · {{ activeLog.processName }}</span>
          <Tag :value="STATUS_LABEL[activeLog.status]" :severity="STATUS_SEVERITY[activeLog.status]" />
        </div>
      </template>

      <template #content>
        <div class="col g-4">
          <div class="row wrap g-3">
            <div class="metric">
              <div class="k">시작</div>
              <div class="v num">{{ mmddhhmm(activeLog.startTime) }}</div>
            </div>
            <div class="metric">
              <div class="k">시작 후 경과</div>
              <div class="v num">{{ duration(elapsedMinutes) }}</div>
            </div>
            <div class="metric">
              <div class="k">설비</div>
              <div class="v sm">{{ activeLog.equipmentName ?? '수작업' }}</div>
            </div>

            <!-- 목표 대비 진척 — 완료 시 몇 개를 입력해야 하는지 판단 근거가 된다.
                 completedQty 는 이 작업지시에 붙은 모든 작업자의 누적 실적이다(§4-4) -->
            <div class="metric progress-metric">
              <div class="k">실적 (누적 / 목표)</div>
              <div class="v num">
                {{ activeLog.completedQty }}<small>/ {{ activeLog.targetQty }}개</small>
              </div>
              <div class="progress">
                <div class="progress-fill"
                     :style="{ width: Math.min(100, (activeLog.completedQty / activeLog.targetQty) * 100) + '%' }" />
              </div>
              <div class="remain num">잔여 {{ Math.max(0, activeLog.targetQty - activeLog.completedQty) }}개</div>
            </div>
          </div>

          <!-- 정지중일 때만 노출: 왜 멈춰 있는지 -->
          <div v-if="isPaused && openPause" class="paused-strip">
            <i class="pi pi-pause-circle" />
            <span>
              <strong>{{ openPause.reasonName }}</strong>
              ({{ CATEGORY_LABEL[openPause.category] }}) 사유로
              {{ hhmm(openPause.pausedAt) }}부터 정지 중입니다.
            </span>
          </div>

          <!-- 정지 이력 — 완료 후 가동률 계산 근거가 되므로 진행 중에도 보여준다 -->
          <div v-if="activeLog.pauses.length" class="col g-2">
            <h4 style="font-size: 13px">정지 이력</h4>
            <div v-for="p in activeLog.pauses" :key="p.id" class="pause-row">
              <span class="dot" :class="p.category === 'Planned' ? 'planned' : 'unplanned'" />
              <span class="pause-name">{{ p.reasonName }}</span>
              <span class="pause-cat">{{ CATEGORY_LABEL[p.category] }}</span>
              <span class="pause-time num">
                {{ hhmm(p.pausedAt) }} ~ {{ p.resumedAt ? hhmm(p.resumedAt) : '진행 중' }}
              </span>
              <span class="pause-dur num">
                {{ p.resumedAt ? duration(minutesBetween(p.pausedAt, p.resumedAt)) : '-' }}
              </span>
            </div>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="row between wrap g-2">
          <div class="row g-2">
            <Button v-if="isRunning" label="일시정지" icon="pi pi-pause" severity="warn"
                    @click="openTimeDialog('pause')" />
            <Button v-if="isPaused" label="재개" icon="pi pi-play"
                    @click="openTimeDialog('resume')" />

            <!-- 정지중에는 완료 불가 (§4-9). 눌러도 안내 토스트만 뜬다 -->
            <Button label="완료" icon="pi pi-check" severity="success"
                    :disabled="isPaused" @click="openTimeDialog('complete')" />
            <span v-if="isPaused" class="hint">재개 후 완료할 수 있습니다</span>
          </div>

          <Button label="이력 삭제" icon="pi pi-trash" severity="danger" text
                  @click="deleteDialog = true" />
        </div>
      </template>
    </Card>

    <!-- ══ 시각 선택 다이얼로그 ═════════════════════════ -->
    <Dialog v-model:visible="timeDialog" modal :header="timeDialogHeader"
            :style="{ width: '360px' }" :draggable="false">
      <div class="col g-3">
        <!-- 정지/재개/완료일 때만 — 시작 시각을 참고할 수 있게 readonly로 띄워둔다.
             입력창이 아니라 그냥 정보 표시용이라 InputText가 아니라 읽기 전용 텍스트로 둔다.
             서버가 "직전 기록 시각보다 이후여야 한다"고 검증하는 그 기준을 미리 눈으로 보여주는 것 -->
        <div v-if="timeMode !== 'start'" class="field">
          <label>시작 시각</label>
          <InputText :modelValue="mmddhhmm(activeLog?.startTime)" readonly fluid />
        </div>

        <!-- 일시정지일 때만 사유 선택. 계획정지/비가동 분류가 가동률 계산을 가른다 (§3-4) -->
        <div v-if="timeMode === 'pause'" class="field">
          <label>정지 사유</label>
          <Select v-model="pickedReasonId" :options="pauseReasons" optionValue="id"
                  optionLabel="name" placeholder="사유 선택" fluid
                  emptyMessage="등록된 정지 사유가 없습니다.">
            <template #option="{ option }">
              <div class="row between g-3" style="width: 100%">
                <span>{{ option.name }}</span>
                <span class="cat-badge" :class="option.category === 'Planned' ? 'planned' : 'unplanned'">
                  {{ CATEGORY_LABEL[option.category] }}
                </span>
              </div>
            </template>
          </Select>
        </div>

        <div class="field">
          <label>시각</label>
          <!-- 분은 10분 단위(§3-1), 기본값은 현재 시각을 10분 단위로 내린 값.
               maxDate 로 미래 날짜·시각을 아예 고를 수 없게 막는다(§3-3의 1차 차단).
               manualInput=false: 텍스트로 직접 타이핑하면 10분 단위·미래시각 제한이
               전부 우회되고 형식도 안 맞아 파싱이 안 되므로, 캘린더 클릭으로만 고르게 막는다 -->
          <DatePicker v-model="pickedTime" showTime hourFormat="24" :stepMinute="10"
                      :maxDate="maxTime" showIcon iconDisplay="input" fluid
                      :manualInput="false" />
          <span class="hint">10분 단위로 선택할 수 있으며, 지금보다 이후 시각은 고를 수 없습니다.</span>
        </div>
      </div>

      <template #footer>
        <Button label="취소" text severity="secondary" @click="timeDialog = false" />
        <Button label="확인" :disabled="!canConfirmTime" @click="confirmTime" />
      </template>
    </Dialog>

    <!-- ══ 완료 2단계 — 실적 수량 ══════════════════════ -->
    <Dialog v-model:visible="qtyDialog" modal header="실적 수량 입력"
            :style="{ width: '320px' }" :draggable="false">
      <div class="col g-3">
        <p class="hint" style="margin: 0">
          종료 시각 {{ toLocalIso(pickedTime)?.slice(11, 16) }} 로 완료 처리됩니다.
        </p>

        <!-- 몇 개를 입력해야 하는지 판단하도록 목표·누적·잔여를 함께 보여준다 -->
        <div v-if="activeLog" class="qty-guide">
          <span>목표 <b class="num">{{ activeLog.targetQty }}</b></span>
          <span>누적 <b class="num">{{ activeLog.completedQty }}</b></span>
          <span>잔여 <b class="num">{{ Math.max(0, activeLog.targetQty - activeLog.completedQty) }}</b></span>
        </div>

        <div class="field">
          <label>실적 수량</label>
          <!-- v-model 대신 :model-value + @input 을 쓰는 이유:
               InputNumber 의 v-model 은 포커스가 빠지거나 Enter 를 눌러야 갱신된다.
               그러면 아래 초과 경고가 한 박자 늦게 떠서 "왜 안 뜨지?" 하게 된다.
               @input 은 타이핑할 때마다 {value} 를 주므로 경고가 즉시 반응한다 -->
          <InputNumber :model-value="actualQty" @input="actualQty = $event.value ?? 0"
                       :min="0" showButtons fluid />
        </div>

        <!-- 초과는 막지 않고 알리기만 한다 (불량 포함 계수 등 정상 케이스가 있어서) -->
        <div v-if="qtyOver > 0" class="qty-over">
          <i class="pi pi-exclamation-triangle" />
          <span>목표를 <b class="num">{{ qtyOver }}개</b> 초과합니다. 입력값을 확인해주세요.</span>
        </div>
      </div>

      <template #footer>
        <Button label="뒤로" text severity="secondary"
                @click="qtyDialog = false; timeDialog = true" />
        <Button label="완료 확정" severity="success" @click="confirmComplete" />
      </template>
    </Dialog>

    <!-- ══ 삭제 확인 ═══════════════════════════════════ -->
    <ConfirmDeleteDialog
      v-model:visible="deleteDialog"
      header="작업이력 삭제"
      question="이 작업이력을 삭제하시겠습니까?"
      severe
      :notes="[
        '정지 기록도 함께 지워집니다.',
        '이 작업을 진행한 사람이 본인뿐이면 작업 지시도 함께 지워집니다.',
        '한 번 지우면 되돌릴 수 없고, 가동률에도 반영되지 않습니다.',
      ]"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.continue-toggle {
  font-size: 13.5px;
  font-weight: 500;
  color: var(--text-normal);
  cursor: pointer;
}

.opt { font-weight: 400; color: var(--text-muted); }
.metric .v.sm { font-size: 16px; }

/* 목표 대비 진척 타일 */
.progress-metric { min-width: 190px; }
.progress-metric .remain {
  font-size: 11.5px;
  color: var(--text-muted);
  margin-top: 4px;
}
.progress-metric .progress { margin-top: 6px; }

/* 완료 팝업의 목표/누적/잔여 안내 */
.qty-guide {
  display: flex;
  gap: 14px;
  padding: 9px 12px;
  border-radius: 8px;
  background: var(--surface-muted);
  font-size: 12.5px;
  color: var(--text-muted);
}
.qty-guide b { color: var(--text-strong); margin-left: 3px; }

/* 목표 초과 경고 — 막지 않고 알리기만 한다 */
.qty-over {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
  border-radius: 8px;
  background: #fff5e6;
  border: 1px solid #f0d5a8;
  color: #8a6320;
  font-size: 12.5px;
}

/* ── 이어하기 작업지시 목록 ── */
.order-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 10px;
}

.order-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px 14px;
  border: 1.5px solid var(--surface-border);
  border-radius: 10px;
  background: var(--surface-card);
  cursor: pointer;
  text-align: left;
  font: inherit;
  color: inherit;
  transition: border-color 0.15s, background 0.15s;
}
.order-card:hover { border-color: #c3ccdb; }
.order-card.picked { border-color: var(--brand); background: #f2f6ff; }

.order-id { font-size: 12px; color: var(--text-muted); }
.order-meta { font-size: 12.5px; color: var(--text-muted); }
.order-time { font-size: 12px; color: var(--text-muted); }
.order-time-open { color: var(--brand); font-weight: 600; }
.order-time-worker { color: var(--text-muted); }

.progress {
  height: 6px;
  background: var(--surface-muted);
  border-radius: 3px;
  overflow: hidden;
}
.progress-fill { height: 100%; background: var(--brand); border-radius: 3px; }
.order-qty { font-size: 12px; color: var(--text-normal); }

/* ── 정지중 안내 띠 ── */
.paused-strip {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 10px 13px;
  border-radius: 9px;
  background: #fff5e6;
  border: 1px solid #f0d5a8;
  color: #8a6320;
  font-size: 13px;
}

/* ── 정지 이력 행 ── */
.pause-row {
  display: grid;
  grid-template-columns: 10px 1fr 62px 130px 74px;
  align-items: center;
  gap: 10px;
  padding: 7px 10px;
  border-radius: 7px;
  background: var(--surface-muted);
  font-size: 12.5px;
}

.dot { width: 8px; height: 8px; border-radius: 50%; }
.dot.planned { background: var(--planned); }
.dot.unplanned { background: var(--unplanned); }

.pause-name { font-weight: 600; color: var(--text-strong); }
.pause-cat { color: var(--text-muted); font-size: 11.5px; }
.pause-time { color: var(--text-normal); }
.pause-dur { text-align: right; color: var(--text-strong); font-weight: 600; }

/* 정지사유 드롭다운의 분류 배지 */
.cat-badge {
  font-size: 11px;
  padding: 1px 7px;
  border-radius: 999px;
}
.cat-badge.planned { background: #eaeef7; color: #4b5c80; }
.cat-badge.unplanned { background: #fdeee2; color: #9a5722; }
</style>

<script setup>
/* ════════════════════════════════════════════════════════════════
 * 상세 조회 화면 (설계 §6-3)
 *
 * 【화면 흐름】
 *   기간(시작~종료) 선택 → 그 기간의 작업지시 대표 카드 목록 → 카드 클릭
 *   → 하단에 "작업지시 헤더 1개 + 그 작업지시의 작업이력(세션) 카드 N개"
 *
 *   ID를 직접 입력하는 방식이 아니라 목록에서 고르는 이유는,
 *   사용자가 WorkLog 의 PK를 알아낼 경로가 어디에도 없기 때문이다.
 *   (대시보드는 그룹별 집계만 주고 개별 이력 id는 주지 않는다)
 *
 * 【목록은 "작업지시 단위" 대표 카드다】
 *   카드 하나는 WorkLog 하나가 아니라 WorkOrder 하나를 대표한다. 같은 작업지시를
 *   이어하기로 여러 세션(여러 명, 또는 한 명이 여러 번)에 나눠 수행했다면 그 중
 *   최근 세션 하나만 목록에 노출하고, 나머지는 클릭했을 때 상세에서 한꺼번에 보여준다.
 *   목록에 WorkLog 개수만큼 카드가 늘어나면 "이게 다 다른 작업이었나?" 오해가 생기기 쉽다.
 *
 * 【상세는 "작업지시 헤더 + 세션 카드 목록"이다】
 *   작업지시 헤더(1개) — 지시#, 라인·공정, 목표수량/누적실적
 *   세션 카드(1개 이상) — 이력#, 작업자, 시작~종료, 실적, 시간분해, 정지이력, 삭제 버튼.
 *   세션이 1건이면 카드도 1장, 여러 건이면 그만큼 나열된다 — 버튼을 눌러야
 *   펼쳐지는 구조가 아니라 클릭한 순간 전부 보인다.
 *
 * 【작업지시 수정은 폐기, 세션 삭제로 대체】
 *   라인/공정/목표수량을 고치는 기능은 일단 빼고, 오입력·방치 이력을 지우는
 *   기능으로 바꿨다. 삭제는 세션(WorkLog) 단위로 한다 — 이미 있는
 *   DELETE /api/work-logs/{id}(§8-3, WorkerDashboard.vue의 "이력 삭제"와 동일 API)를
 *   그대로 재사용한다. 형제 세션이 없으면 작업지시도 함께 지워진다.
 *
 * 【진행중 건도 목록에 보여준다】
 *   완료 건만 걸러내지 않는다. 퇴근하며 종료를 잊어 방치된 이력을 찾아
 *   지울 수 있어야 하기 때문이다 (§8-3). 그런 건이 남아 있으면 대시보드의
 *   분모(부하시간)가 계속 커져서 가동률이 실제보다 낮게 나온다.
 *
 * 【완료 전에는 시간 분해 영역을 숨긴다】
 *   ElapsedMinutes / OperatingMinutes / NetOperatingMinutes 세 캐시 컬럼은
 *   Complete() 시점에만 채워진다 (§3-5). 진행중 건은 전부 null 이라
 *   그대로 그리면 0분 / 0% 라는 틀린 값이 표시된다.
 *   그래서 완료 건에만 분해를 보여주고, 진행중이면 경과 시간만 안내한다.
 *
 * 【집계 귀속 기준】
 *   목록은 StartTime 기준이라, 자정을 넘긴 야간 작업은 개시일에 묶인다 (§3-6).
 *
 * ────────────────────────────────────────────────────────────────
 * 【사용 API】
 *   GET    /api/work-logs?startDate=&endDate=       — 기간별 목록(대표 카드용)
 *   GET    /api/work-logs/by-order/{workOrderId}     — 대표 카드 클릭 시, 세션 전체
 *   DELETE /api/work-logs/{id}                       — 세션 삭제, 성공 후 목록+상세 재조회
 * ════════════════════════════════════════════════════════════════ */

import { ref, computed, watch, onMounted } from 'vue';
import { useToast } from 'primevue/usetoast';
import { api } from '../client.js';

/* ── 사용하는 PrimeVue 위젯 ──────────────────────────────────
 * DatePicker   : selectionMode="range" 로 시작~종료 두 날짜를 한 번에 고른다.
 *                showIcon + iconDisplay="input" 이면 입력칸 안에 달력 아이콘이 붙는다.
 *                maxDate 로 오늘 이후는 아예 선택 못 하게 막는다
 * SelectButton : 버튼형 라디오. 목록의 상태 필터(전체/완료/미완료)에 쓴다
 * Card       : 카드 컨테이너 (#title #content 슬롯)
 * Tag        : 상태 배지 (진행중/정지중/완료)
 * Button     : 버튼
 * DataTable  : 표 (정지 이력). Column 으로 열을 정의하고 #body 로 셀을 직접 그린다
 * Column     : DataTable 의 열
 * ConfirmDeleteDialog : 삭제 확인 재사용 컴포넌트(§6-1) — WorkerDashboard.vue와 동일
 * ─────────────────────────────────────────────────────────── */
import DatePicker from 'primevue/datepicker';
import SelectButton from 'primevue/selectbutton';
import Card from 'primevue/card';
import Tag from 'primevue/tag';
import Button from 'primevue/button';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import ConfirmDeleteDialog from './common/ConfirmDeleteDialog.vue';

import {
  hhmm, mmddhhmm, ymd, duration, minutesBetween, rateColor,
  STATUS_LABEL, STATUS_SEVERITY, CATEGORY_LABEL,
} from '../utils/format.js';

const toast = useToast();

/* ── 조회 조건 ──────────────────────────────────────────── */
const today = new Date();
/* PrimeVue range 모드는 [시작, 종료] 배열 하나로 v-model 을 받는다.
 * 종료를 아직 안 고른 중간 상태에선 배열의 두 번째 값이 null 일 수 있다.
 * 기본값은 오늘 하루 — 방치된 이력을 매일 확인하는 용도라 "오늘"이 제일 흔한 조회다 */
const dateRange = ref([new Date(), new Date()]);

const statusFilters = [
  { label: '전체', value: 'all' },
  { label: '완료', value: 'done' },
  { label: '미완료', value: 'open' },   // 진행중 + 정지중. 방치된 건을 찾을 때 쓴다
];
const statusFilter = ref('all');

/* 기간별 목록 — 날짜 범위 필터링은 서버(GetByDateRangeAsync)가 다 하고 내려주므로
 * 여기선 상태 필터(전체/완료/미완료)만 클라이언트에서 한 번 더 거른다 */
const logs = ref([]);

async function fetchLogs() {
  const [start, end] = dateRange.value;
  if (!start) { logs.value = []; return; }
  try {
    logs.value = await api.getWorkLogsByRange(ymd(start), ymd(end ?? start));
  } catch (err) {
    logs.value = [];
    toast.add({
      severity: 'error', summary: '작업이력 목록을 불러오지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

const filteredLogs = computed(() => {
  if (statusFilter.value === 'done') return logs.value.filter((l) => l.status === 'Completed');
  if (statusFilter.value === 'open') return logs.value.filter((l) => l.status !== 'Completed');
  return logs.value;
});

/* 미완료 건수 — 방치된 이력이 있으면 눈에 띄어야 한다 (§8-3). 상태 필터와 무관하게 항상 센다 */
const openCount = computed(() => logs.value.filter((l) => l.status !== 'Completed').length);

/* 같은 작업지시(workOrderId)를 공유하는 이력이 여럿이면 대표 1건만 목록에 남긴다.
 * "대표"는 이 기간 안에서 가장 최근에 시작된 세션 — 최신 진행 상황을 보여주는 게 목적이라서다.
 * 나머지 세션은 카드를 클릭했을 때 상세에서 한꺼번에 보여준다(siblingLogs) */
const representativeLogs = computed(() => {
  const sorted = [...filteredLogs.value].sort((a, b) => new Date(b.startTime) - new Date(a.startTime));
  const seen = new Set();
  const result = [];
  for (const log of sorted) {
    if (seen.has(log.workOrderId)) continue;
    seen.add(log.workOrderId);
    result.push(log);
  }
  return result;
});

/* 목록 카드에 표시할 가동률. 완료 건만 계산할 수 있다.
 * 상세의 breakdownOf()와 같은 공식(가동/부하시간, §3-4) — 분모를 elapsed로 잘못 쓰면
 * 이 카드와 상세 화면이 같은 이력을 두고 서로 다른 %를 보여주게 된다 */
function rateOf(log) {
  if (log.status !== 'Completed' || !log.operatingMinutes) return null;
  return (log.netOperatingMinutes / log.operatingMinutes) * 100;
}

/* ── 선택 / 상세 ────────────────────────────────────────── */
/* WorkLog 가 아니라 WorkOrder 를 선택 단위로 삼는다 — 클릭 한 번으로
 * 그 작업지시에 딸린 세션을 전부 보여주는 게 목표라서다 */
const selectedWorkOrderId = ref(null);

/* 기간이 바뀌면 목록이 달라지므로 선택을 비우고 다시 조회한다 */
watch(dateRange, () => {
  selectedWorkOrderId.value = null;
  fetchLogs();
});

onMounted(fetchLogs);

/* 선택된 작업지시의 모든 세션(WorkLog) — 날짜 범위·상태 필터와 무관하게 전체를 받는다.
 * 이어하기가 날짜 경계를 넘나들 수 있어서, 지금 화면에 걸린 기간 밖의 세션도 "형제"일 수 있다 */
const siblingLogs = ref([]);

async function fetchSiblingLogs(workOrderId) {
  try {
    siblingLogs.value = await api.getWorkLogsByOrder(workOrderId);
  } catch (err) {
    siblingLogs.value = [];
    toast.add({
      severity: 'error', summary: '작업지시 상세를 불러오지 못했습니다.',
      detail: err.message, life: 4000,
    });
  }
}

function selectOrder(workOrderId) {
  selectedWorkOrderId.value = workOrderId;
  fetchSiblingLogs(workOrderId);
}

/* 작업지시 헤더에 쓸 요약 — 라인/공정/목표수량은 형제끼리 항상 같은 값이라
 * 대표로 첫 세션 것을 쓰면 되고, 누적실적만 세션들을 합산한다 */
const orderSummary = computed(() => {
  const first = siblingLogs.value[0];
  if (!first) return null;
  const completedQty = siblingLogs.value
    .filter((l) => l.status === 'Completed')
    .reduce((sum, l) => sum + l.actualQty, 0);
  return {
    workOrderId: first.workOrderId,
    lineName: first.lineName,
    processName: first.processName,
    targetQty: first.targetQty,
    completedQty,
  };
});

/* 완료 전 경과 시간 — 캐시값이 없으니 현재 시각 기준으로 계산해 보여준다 */
function runningMinutesOf(log) {
  return minutesBetween(log.startTime, null);
}

/* ── 시간 분해 (§3-4) ────────────────────────────────────
 * 조업시간 = 가동 + 계획정지 + 비가동. 세션(WorkLog) 하나마다 따로 계산한다 */
function breakdownOf(log) {
  if (log.status !== 'Completed') return null;

  const elapsed = log.elapsedMinutes ?? 0;
  const operating = log.operatingMinutes ?? 0;
  const planned = elapsed - operating;                       // 조업 − 부하
  const unplanned = operating - (log.netOperatingMinutes ?? 0); // 부하 − 가동
  const net = log.netOperatingMinutes ?? 0;
  // 세 조각(netPct/plannedPct/unplannedPct)은 막대 전체(조업시간)를 100으로 보는 "구성비"라 elapsed로 나눈다.
  // 가동률(rate) 자체는 §3-4대로 가동/부하시간 — 계획정지는 애초에 가동 대상이 아니었으므로 분모에서 뺀다
  const pct = (v) => (elapsed === 0 ? 0 : (v / elapsed) * 100);

  return {
    elapsed, planned, unplanned, net,
    netPct: pct(net), plannedPct: pct(planned), unplannedPct: pct(unplanned),
    rate: operating === 0 ? 0 : (net / operating) * 100,
  };
}

/* ══ 세션 삭제 ══════════════════════════════════════════
 * 오입력이나 종료를 잊고 방치된 이력을 지우는 경로 (§8-3).
 * WorkerDashboard.vue의 "이력 삭제"와 같은 API(DELETE /api/work-logs/{id})를 쓴다 —
 * 다만 여기는 "지금 진행중인 내 세션"이 아니라 "과거 아무 세션"을 대상으로 한다는 게 다르다 */
const deleteDialog = ref(false);
const deleteTarget = ref(null);

function askDelete(log) {
  deleteTarget.value = log;
  deleteDialog.value = true;
}

async function confirmDeleteLog() {
  try {
    // 응답 { result: "deleted" | "deleted_with_order" } — 지금은 문구를 안 나누고
    // 둘 다 같은 성공 토스트로 안내한다(§8-3, WorkerDashboard.vue의 삭제와 동일 수준)
    await api.deleteWorkLog(deleteTarget.value.id);

    toast.add({
      severity: 'success',
      summary: '작업이력을 삭제했습니다.',
      life: 2500,
    });

    deleteDialog.value = false;

    // 목록과 상세를 최신 상태로 다시 받아온다 — 부분 병합 대신 재조회(§4-13 근처에서 쓴 것과 같은 패턴)
    await fetchLogs();
    if (selectedWorkOrderId.value) await fetchSiblingLogs(selectedWorkOrderId.value);

    // 이 작업지시에 남은 세션이 없으면 상세를 닫는다(형제 없이 지워졌다면 작업지시도 함께 사라진 것)
    if (!siblingLogs.value.length) selectedWorkOrderId.value = null;
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
    <!-- ══ 1. 조회 조건 ═════════════════════════════════ -->
    <Card>
      <template #content>
        <!-- 기간은 왼쪽 고정, 미완료 경고 + 상태 필터는 오른쪽으로 묶어서 정렬 -->
        <div class="row between wrap g-5">
          <div class="field">
            <label>기간</label>
            <DatePicker v-model="dateRange" selectionMode="range" dateFormat="yy-mm-dd"
                        showIcon iconDisplay="input" :manualInput="false" :maxDate="today"
                        class="range-picker" />
          </div>

          <div class="row wrap g-4" style="align-items: flex-end">
            <!-- 종료를 잊고 방치된 건이 있으면 눈에 띄게 (§8-3) -->
            <div v-if="openCount" class="field">
              <span class="open-warn">
                <i class="pi pi-exclamation-circle" />
                미완료 {{ openCount }}건
              </span>
            </div>

            <div class="field">
              <label>상태</label>
              <SelectButton v-model="statusFilter" :options="statusFilters"
                            optionLabel="label" optionValue="value" :allowEmpty="false" />
            </div>
          </div>
        </div>

        <div class="row g-2 mt-3 hint">
          <i class="pi pi-info-circle" style="font-size: 12px" />
          <span>
            <strong>{{ ymd(dateRange[0]) }} ~ {{ ymd(dateRange[1] ?? dateRange[0]) }}</strong>
            동안 <strong>시작한</strong> 작업 이력을 보여줍니다. 밤을 넘겨 이어진 작업은 시작한 날에 표시됩니다.
            오늘 이후 날짜는 고를 수 없습니다.
          </span>
        </div>
      </template>
    </Card>

    <!-- ══ 2. 목록 (작업지시 대표 카드) ═══════════════════ -->
    <div v-if="!representativeLogs.length" class="empty">
      선택한 기간에 작업 이력이 없습니다.
    </div>

    <div v-else class="log-list">
      <button
        v-for="log in representativeLogs" :key="log.workOrderId"
        class="log-card" :class="{ picked: selectedWorkOrderId === log.workOrderId }"
        @click="selectOrder(log.workOrderId)"
      >
        <div class="row between g-2">
          <strong>{{ log.workerName }}</strong>
          <Tag :value="STATUS_LABEL[log.status]" :severity="STATUS_SEVERITY[log.status]" />
        </div>

        <div class="log-meta">{{ log.lineName }} · {{ log.processName }}</div>

        <div class="row between g-2">
          <span class="log-time num">
            {{ hhmm(log.startTime) }} ~ {{ log.endTime ? hhmm(log.endTime) : '진행 중' }}
          </span>
          <!-- 완료 건만 가동률을 계산할 수 있다 (캐시 컬럼이 그때 채워지므로) -->
          <span v-if="rateOf(log) !== null" class="log-rate num"
                :style="{ color: rateColor(rateOf(log)) }">
            {{ rateOf(log).toFixed(1) }}%
          </span>
          <span v-else class="log-rate pending">—</span>
        </div>

        <!-- 화면 구석 — 이 이력이 어느 작업지시 소속인지 항상 보이게 -->
        <div class="log-id num">지시#{{ log.workOrderId }} · 이력#{{ log.id }}</div>
      </button>
    </div>

    <!-- ══ 3. 상세 ══════════════════════════════════════ -->
    <div v-if="!selectedWorkOrderId" class="empty">
      위 목록에서 작업을 선택해 주세요.
    </div>

    <template v-else-if="orderSummary">
      <!-- 작업지시 헤더 — 세션이 몇 건이든 딱 한 번만 나온다 -->
      <Card>
        <template #title>
          <span class="order-title">작업지시 #{{ orderSummary.workOrderId }}</span>
        </template>

        <template #content>
          <div class="row wrap g-3 between">
            <div class="row wrap g-3">
              <div class="metric"><div class="k">라인</div><div class="v sm">{{ orderSummary.lineName }}</div></div>
              <div class="metric"><div class="k">공정</div><div class="v sm">{{ orderSummary.processName }}</div></div>
              <div class="metric"><div class="k">세션 수</div><div class="v sm num">{{ siblingLogs.length }}건</div></div>
            </div>

            <div class="progress-metric">
              <div class="row between g-2">
                <span class="k">목표 대비 누적 실적</span>
                <span class="v sm num">{{ orderSummary.completedQty }} / {{ orderSummary.targetQty }}개</span>
              </div>
              <div class="progress">
                <div class="progress-fill"
                     :style="{ width: Math.min(100, (orderSummary.completedQty / orderSummary.targetQty) * 100) + '%' }" />
              </div>
            </div>
          </div>
        </template>
      </Card>

      <!-- 세션(WorkLog) 카드 — 있는 만큼 그대로 나열. 형제가 여럿이면 함께 진행했다는 뜻 -->
      <Card v-for="log in siblingLogs" :key="log.id">
        <template #title>
          <div class="row between wrap g-2">
            <div class="row g-2">
              <strong>{{ log.workerName }}</strong>
              <Tag :value="STATUS_LABEL[log.status]" :severity="STATUS_SEVERITY[log.status]" />
            </div>
            <div class="row g-1" style="align-items: center">
              <!-- 화면 구석 — 이 세션의 작업이력 번호 -->
              <span class="session-id num">이력#{{ log.id }}</span>
              <Button icon="pi pi-trash" severity="danger" text size="small"
                      aria-label="작업이력 삭제" @click="askDelete(log)" />
            </div>
          </div>
        </template>

        <template #content>
          <div class="col g-4">
            <div class="row wrap g-3">
              <div class="metric"><div class="k">설비</div><div class="v sm">{{ log.equipmentName ?? '수작업' }}</div></div>
              <div class="metric"><div class="k">시작</div><div class="v sm num">{{ mmddhhmm(log.startTime) }}</div></div>
              <div class="metric">
                <div class="k">종료</div>
                <div class="v sm num">{{ log.endTime ? mmddhhmm(log.endTime) : '-' }}</div>
              </div>
              <div v-if="log.status === 'Completed'" class="metric">
                <div class="k">실적 수량</div><div class="v num">{{ log.actualQty }}<small>개</small></div>
              </div>
            </div>

            <!-- 시간 분해 — 완료 건에만 (§3-5의 캐시 컬럼이 그때 채워지므로) -->
            <div v-if="log.status === 'Completed' && breakdownOf(log)" class="col g-4">
              <div class="row wrap g-4">
                <div class="rate" :style="{ color: rateColor(breakdownOf(log).rate) }">
                  {{ breakdownOf(log).rate.toFixed(1) }}<small>%</small>
                </div>
                <div class="rate-formula hint">
                  가동시간 {{ duration(breakdownOf(log).net) }} ÷ 부하시간 {{ duration(log.operatingMinutes) }}
                </div>
              </div>

              <!-- 조업시간 100% 를 세 조각으로. 폭이 곧 손실의 크기라 원인이 빨리 읽힌다 -->
              <div class="stack">
                <div class="seg net" :style="{ width: breakdownOf(log).netPct + '%' }" />
                <div class="seg planned" :style="{ width: breakdownOf(log).plannedPct + '%' }" />
                <div class="seg unplanned" :style="{ width: breakdownOf(log).unplannedPct + '%' }" />
              </div>

              <div class="row wrap g-4 legend">
                <span><i class="sw net" />가동 {{ duration(breakdownOf(log).net) }}</span>
                <span><i class="sw planned" />계획정지 {{ duration(breakdownOf(log).planned) }}</span>
                <span><i class="sw unplanned" />비가동 {{ duration(breakdownOf(log).unplanned) }}</span>
              </div>

              <div class="formula">
                <div><span>조업시간</span><b class="num">{{ duration(breakdownOf(log).elapsed) }}</b><em>종료 − 시작</em></div>
                <div><span>부하시간</span><b class="num">{{ duration(log.operatingMinutes) }}</b><em>조업 − 계획정지</em></div>
                <div><span>가동시간</span><b class="num">{{ duration(log.netOperatingMinutes) }}</b><em>부하 − 비가동</em></div>
              </div>
            </div>

            <!-- 완료 전: 분해 대신 경과 시간만. 캐시값이 없어 가동률을 확정할 수 없다 -->
            <div v-else class="pending-box">
              <i class="pi pi-clock" />
              <div class="col g-1">
                <strong>아직 진행 중인 세션입니다</strong>
                <span class="hint">
                  가동률은 완료해야 계산됩니다.
                  지금까지 <b class="num">{{ duration(runningMinutesOf(log)) }}</b> 경과했습니다.
                </span>
              </div>
            </div>

            <!-- 정지 이력 -->
            <div class="col g-2">
              <div class="pause-title">정지 이력 ({{ log.pauses.length }}건)</div>
              <div v-if="!log.pauses.length" class="empty">정지 기록이 없습니다.</div>

              <DataTable v-else :value="log.pauses" size="small" stripedRows>
                <Column field="reasonName" header="사유" />

                <Column header="분류" style="width: 110px">
                  <template #body="{ data }">
                    <span class="cat-badge" :class="data.category === 'Planned' ? 'planned' : 'unplanned'">
                      {{ CATEGORY_LABEL[data.category] }}
                    </span>
                  </template>
                </Column>

                <Column header="정지" style="width: 90px">
                  <template #body="{ data }"><span class="num">{{ hhmm(data.pausedAt) }}</span></template>
                </Column>

                <Column header="재개" style="width: 100px">
                  <template #body="{ data }">
                    <!-- 재개되지 않은 "열린 정지" — 현재 정지 중이라는 뜻 -->
                    <span v-if="data.resumedAt" class="num">{{ hhmm(data.resumedAt) }}</span>
                    <span v-else class="open-pause">정지 중</span>
                  </template>
                </Column>

                <Column header="소요" style="width: 100px">
                  <template #body="{ data }">
                    <span class="num">
                      {{ data.resumedAt ? duration(minutesBetween(data.pausedAt, data.resumedAt)) : '-' }}
                    </span>
                  </template>
                </Column>
              </DataTable>
            </div>
          </div>
        </template>
      </Card>
    </template>

    <!-- ══ 4. 삭제 확인 ═══════════════════════════════════ -->
    <ConfirmDeleteDialog
      v-model:visible="deleteDialog"
      header="작업이력 삭제"
      question="이 작업이력을 삭제하시겠습니까?"
      severe
      :notes="[
        '정지 기록도 함께 지워집니다.',
        '이 작업을 진행한 사람이 이 세션뿐이면 작업지시도 함께 지워집니다.',
        '한 번 지우면 되돌릴 수 없고, 가동률에도 반영되지 않습니다.',
      ]"
      @confirm="confirmDeleteLog"
    />
  </div>
</template>

<style scoped>
/* ── 조회 조건 ── */
/* range 모드는 "yyyy-mm-dd - yyyy-mm-dd" 두 날짜를 한 입력칸에 표시해 기본 폭으로는 잘린다 */
.range-picker :deep(input) { width: 250px; }

/* ── 목록 카드 ── */
.log-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(226px, 1fr));
  gap: 10px;
}

.log-card {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 7px;
  padding: 12px 14px 26px;
  border: 1.5px solid var(--surface-border);
  border-radius: 10px;
  background: var(--surface-card);
  cursor: pointer;
  text-align: left;
  font: inherit;
  color: inherit;
  transition: border-color 0.15s, background 0.15s;
}
.log-card:hover { border-color: #c3ccdb; }
.log-card.picked {
  border-color: var(--brand);
  background: #f2f6ff;
}

.log-meta { font-size: 12.5px; color: var(--text-muted); }
.log-time { font-size: 12.5px; color: var(--text-normal); }
.log-rate { font-size: 15px; font-weight: 700; }
.log-rate.pending { color: var(--text-muted); font-weight: 500; }

.log-id {
  position: absolute;
  bottom: 8px; right: 12px;
  font-size: 10.5px;
  color: var(--text-muted);
}

/* 미완료 건수 경고 */
.open-warn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12.5px;
  font-weight: 600;
  color: #8a6320;
  background: #fff6e5;
  border: 1px solid #f3d9a4;
  border-radius: 999px;
  padding: 5px 11px;
}

/* ── 작업지시 헤더 ── */
.order-title { font-size: 15px; font-weight: 700; color: var(--text-strong); }
.progress-metric { min-width: 220px; flex: 1; }
.progress-metric .progress { margin-top: 6px; }

/* 세션 카드 구석의 이력 번호 */
.session-id { font-size: 11px; color: var(--text-muted); }

/* ── 상세 공통 ── */
.metric .v.sm { font-size: 16px; }

.progress {
  height: 6px;
  background: var(--surface-muted);
  border-radius: 3px;
  overflow: hidden;
}
.progress-fill { height: 100%; background: var(--brand); border-radius: 3px; }

.rate {
  font-size: 44px;
  font-weight: 750;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}
.rate small { font-size: 20px; margin-left: 2px; }
.rate-formula { align-self: flex-end; padding-bottom: 6px; }

.stack {
  display: flex;
  height: 26px;
  border-radius: 7px;
  overflow: hidden;
  background: var(--surface-muted);
}
.seg { height: 100%; transition: width 0.25s ease; }
.seg.net { background: var(--ok); }
.seg.planned { background: var(--planned); }
.seg.unplanned { background: var(--unplanned); }

.legend { font-size: 12.5px; color: var(--text-normal); }
.legend span { display: inline-flex; align-items: center; gap: 6px; }
.sw { width: 10px; height: 10px; border-radius: 2px; display: inline-block; }
.sw.net { background: var(--ok); }
.sw.planned { background: var(--planned); }
.sw.unplanned { background: var(--unplanned); }

.formula {
  display: flex;
  flex-direction: column;
  gap: 1px;
  background: var(--surface-border);
  border-radius: 8px;
  overflow: hidden;
}
.formula > div {
  display: grid;
  grid-template-columns: 110px 120px 1fr;
  align-items: center;
  gap: 10px;
  padding: 9px 13px;
  background: var(--surface-card);
  font-size: 13px;
}
.formula span { color: var(--text-muted); }
.formula b { color: var(--text-strong); }
.formula em { font-style: normal; font-size: 12px; color: var(--text-muted); }

/* 완료 전 안내 박스 */
.pending-box {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px 18px;
  border-radius: 10px;
  background: var(--surface-muted);
  color: var(--text-normal);
}
.pending-box > i { font-size: 18px; color: var(--text-muted); margin-top: 2px; }
.pending-box strong { color: var(--text-strong); font-size: 14px; }

.pause-title { font-size: 13px; font-weight: 600; color: var(--text-strong); }

.cat-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 999px;
  white-space: nowrap;
}
.cat-badge.planned { background: #eaeef7; color: #4b5c80; }
.cat-badge.unplanned { background: #fdeee2; color: #9a5722; }

.open-pause {
  font-size: 11.5px;
  font-weight: 600;
  color: #9a5722;
}

.opt { font-weight: 400; color: var(--text-muted); }
</style>

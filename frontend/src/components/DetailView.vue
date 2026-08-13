<script setup>
/* ════════════════════════════════════════════════════════════════
 * 상세 조회 화면 (설계 §6-3)
 *
 * 【화면 흐름】
 *   날짜 선택 → 그 날짜의 작업이력 목록(카드) → 카드 클릭 → 하단에 상세
 *
 *   ID를 직접 입력하는 방식이 아니라 목록에서 고르는 이유는,
 *   사용자가 WorkLog 의 PK를 알아낼 경로가 어디에도 없기 때문이다.
 *   (대시보드는 그룹별 집계만 주고 개별 이력 id는 주지 않는다)
 *
 * 【진행중 건도 목록에 보여준다】
 *   완료 건만 걸러내지 않는다. 퇴근하며 종료를 잊어 방치된 이력을 찾아
 *   지울 수 있어야 하기 때문이다 (§8-3). 그런 건이 남아 있으면 대시보드의
 *   분모(조업시간)가 계속 커져서 가동률이 실제보다 낮게 나온다.
 *
 * 【완료 전에는 시간 분해 영역을 숨긴다】
 *   ElapsedMinutes / OperatingMinutes / NetOperatingMinutes 세 캐시 컬럼은
 *   Complete() 시점에만 채워진다 (§3-5). 진행중 건은 전부 null 이라
 *   그대로 그리면 0분 / 0% 라는 틀린 값이 표시된다.
 *   그래서 완료 건에만 분해를 보여주고, 진행중이면 경과 시간만 안내한다.
 *   (서버가 NOW() 기준 잠정값을 내려주게 되면 그때 이 영역을 열면 된다)
 *
 * 【집계 귀속 기준】
 *   목록은 StartTime 기준이라, 자정을 넘긴 야간 작업은 개시일에 묶인다 (§3-6).
 *
 * ────────────────────────────────────────────────────────────────
 * TODO(연동) — 이 화면이 사용할 API
 *   GET   /api/work-logs?date={yyyy-MM-dd}     ★ 신규 필요 (설계 §5에 추가해야 함)
 *         → [{ id, workerId, workerName, lineName, processName, equipmentName,
 *              status, startTime, endTime, actualQty,
 *              elapsedMinutes, operatingMinutes, netOperatingMinutes }]
 *            진행중 건은 시간 3개가 null 로 온다.
 *
 *   GET   /api/work-logs/{id}                  → 상세 (정지 이력 pauses 포함)
 *   GET   /api/lines                           → 수정 다이얼로그의 라인 목록
 *   GET   /api/processes?lineId=               → 선택 라인의 공정 (캐스케이딩)
 *   GET   /api/equipment                       → 설비 목록
 *   PATCH /api/work-orders/{workOrderId}       → {lineId, processId, equipmentId?}
 *                                                라인-공정 조합 위반 시 409
 *
 *   날짜가 바뀌면 목록을 다시 조회하고 선택을 비운다 →
 *     watch(date, () => { fetchList(); selectedId.value = null; })
 * ════════════════════════════════════════════════════════════════ */

import { ref, computed, watch } from 'vue';
import { useToast } from 'primevue/usetoast';

/* ── 사용하는 PrimeVue 위젯 ──────────────────────────────────
 * DatePicker : 달력. 여기서는 날짜만 고르므로 showTime 없이 쓴다
 *              showIcon + iconDisplay="input" 이면 입력칸 안에 달력 아이콘이 붙는다
 * SelectButton : 버튼형 라디오. 목록의 상태 필터(전체/완료/미완료)에 쓴다
 * Card       : 카드 컨테이너 (#title #content 슬롯)
 * Tag        : 상태 배지 (진행중/정지중/완료)
 * Button     : 버튼
 * DataTable  : 표 (정지 이력). Column 으로 열을 정의하고 #body 로 셀을 직접 그린다
 * Column     : DataTable 의 열
 * Dialog     : 모달 팝업 (작업지시 수정)
 * Select     : 드롭다운. 수정 팝업에서도 등록 화면과 같은 캐스케이딩 규칙을 적용한다
 * ─────────────────────────────────────────────────────────── */
import DatePicker from 'primevue/datepicker';
import SelectButton from 'primevue/selectbutton';
import Card from 'primevue/card';
import Tag from 'primevue/tag';
import Button from 'primevue/button';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Dialog from 'primevue/dialog';
import Select from 'primevue/select';

import {
  hhmm, mmddhhmm, duration, minutesBetween, rateColor,
  STATUS_LABEL, STATUS_SEVERITY, CATEGORY_LABEL,
} from '../utils/format.js';
import * as mock from '../mock/index.js';

const toast = useToast();

/* ── 목록 조회 ──────────────────────────────────────────── */
const date = ref(new Date('2026-08-09'));

const statusFilters = [
  { label: '전체', value: 'all' },
  { label: '완료', value: 'done' },
  { label: '미완료', value: 'open' },   // 진행중 + 정지중. 방치된 건을 찾을 때 쓴다
];
const statusFilter = ref('all');

/* TODO(연동) GET /api/work-logs?date= 응답으로 교체 */
const logs = ref([...mock.workLogsByDate]);

const filteredLogs = computed(() => {
  if (statusFilter.value === 'done') return logs.value.filter((l) => l.status === 'Completed');
  if (statusFilter.value === 'open') return logs.value.filter((l) => l.status !== 'Completed');
  return logs.value;
});

/* 미완료 건수 — 방치된 이력이 있으면 눈에 띄어야 한다 (§8-3) */
const openCount = computed(() => logs.value.filter((l) => l.status !== 'Completed').length);

/* 목록 카드에 표시할 가동률. 완료 건만 계산할 수 있다 */
function rateOf(log) {
  if (log.status !== 'Completed' || !log.elapsedMinutes) return null;
  return (log.netOperatingMinutes / log.elapsedMinutes) * 100;
}

/* ── 선택 / 상세 ────────────────────────────────────────── */
const selectedId = ref(null);

/* 날짜를 바꾸면 목록이 달라지므로 선택을 비운다 */
watch(date, () => {
  selectedId.value = null;
  // TODO(연동) 여기서 목록 재조회
});

/* 목록 행 + 상세(정지 이력)를 합친 것이 화면에 그릴 최종 데이터.
 * TODO(연동) 실제로는 카드 클릭 시 GET /api/work-logs/{id} 를 호출해 통째로 받는다 */
const detail = computed(() => {
  if (!selectedId.value) return null;
  const row = logs.value.find((l) => l.id === selectedId.value);
  if (!row) return null;
  return { ...row, ...(mock.workLogDetails[selectedId.value] ?? { pauses: [] }) };
});

const isCompleted = computed(() => detail.value?.status === 'Completed');

/* 같은 작업지시에 붙어 있는 다른 작업이력 수.
 *
 * ⚠ 중요한 구분 — 아래 "작업지시 수정"은 WorkLog 가 아니라 WorkOrder 를 고친다.
 *   라인·공정·설비는 WorkOrder 가 소유하고 WorkLog 는 WorkOrderId FK 하나만 갖기 때문(§4-4).
 *   따라서 이어하기로 여러 명이 붙은 작업지시라면, 한 사람의 카드에서 수정해도
 *   나머지 작업자들의 이력에 표시되는 라인·공정까지 함께 바뀐다.
 *   사용자는 "이 이력만 고친다"고 오해하기 쉬우므로 건수를 미리 알려준다.
 *
 * TODO(연동) 목록 응답에 workOrderId 가 포함되면 그걸로 세면 된다.
 *   서버가 siblingCount 를 내려주는 편이 더 정확하다(다른 날짜의 형제 이력까지 포함되므로) */
const siblingCount = computed(() => {
  if (!detail.value?.workOrderId) return 0;
  return logs.value.filter(
    (l) => (mock.workLogDetails[l.id]?.workOrderId) === detail.value.workOrderId
      && l.id !== detail.value.id,
  ).length;
});

/* 완료 전 경과 시간 — 캐시값이 없으니 현재 시각 기준으로 계산해 보여준다 */
const runningMinutes = computed(() =>
  detail.value ? minutesBetween(detail.value.startTime, null) : 0,
);

/* ── 시간 분해 (§3-4) ────────────────────────────────────
 * 조업시간 = 실가동 + 계획정지 + 비가동
 * 완료 시점에 서버가 캐시해둔 3개 값에서 역산한다 */
const breakdown = computed(() => {
  const d = detail.value;
  if (!d || d.status !== 'Completed') return null;

  const elapsed = d.elapsedMinutes ?? 0;
  const planned = elapsed - (d.operatingMinutes ?? 0);                       // 조업 − 가동
  const unplanned = (d.operatingMinutes ?? 0) - (d.netOperatingMinutes ?? 0); // 가동 − 실가동
  const net = d.netOperatingMinutes ?? 0;
  const pct = (v) => (elapsed === 0 ? 0 : (v / elapsed) * 100);

  return {
    elapsed, planned, unplanned, net,
    netPct: pct(net), plannedPct: pct(planned), unplannedPct: pct(unplanned),
    rate: pct(net),
  };
});

/* ── 작업지시 수정 다이얼로그 (§4-8, §4-9) ────────────────
 * 라인이 파생값에서 사용자 입력값이 되면서 오선택을 시스템이 탐지할 수 없게 됐다.
 * 그래서 대응을 "검증"이 아니라 "정정 경로"로 잡았다. */
const editDialog = ref(false);
const editLineId = ref(null);
const editProcessId = ref(null);
const editEquipmentId = ref(null);

const lines = ref(mock.lines.filter((l) => l.isActive));
const allProcesses = ref(mock.processes);
const equipments = ref(mock.equipments.filter((e) => e.isActive));

/* 등록 화면과 동일한 캐스케이딩 — 라인을 골라야 공정이 열린다 */
const editProcessOptions = computed(() => {
  if (!editLineId.value) return [];
  return allProcesses.value.filter(
    (p) => p.isActive && p.lineIds.includes(editLineId.value),
  );
});

function openEdit() {
  editLineId.value = detail.value.lineId;
  editProcessId.value = detail.value.processId;
  editEquipmentId.value = detail.value.equipmentId;
  editDialog.value = true;
}

/* 라인이 바뀌면 기존 공정이 새 라인에 속하지 않을 수 있으므로 비운다 */
function onEditLineChange() {
  editProcessId.value = null;
}

const canSaveEdit = computed(() => !!(editLineId.value && editProcessId.value));

/* 저장 확인 팝업.
 *
 * §6-1 은 "수정/저장은 되돌릴 수 있는 작업이라 확인창을 붙이지 않는다"가 원칙이지만,
 * 이 수정은 예외다 — WorkOrder 를 바꾸는 것이라 같은 작업지시를 함께 수행한
 * 다른 작업자들의 이력에도 반영된다. 사용자는 "이 이력만 고친다"고 오해하기 쉽다.
 *
 * 다만 확인창을 남발하면 경고력이 떨어지므로(§6-1),
 * 형제 이력이 있을 때만 띄우고 혼자 수행한 건은 바로 저장한다. */
const confirmDialog = ref(false);

function requestSave() {
  if (siblingCount.value > 0) {
    confirmDialog.value = true;
    return;
  }
  saveEdit();
}

function saveEdit() {
  // TODO(연동) PATCH /api/work-orders/{detail.workOrderId}
  //   {lineId: editLineId, processId: editProcessId, equipmentId: editEquipmentId}
  //   성공하면 목록과 상세를 다시 조회한다. 조합 위반 시 409 + ProblemDetails
  toast.add({
    severity: 'success',
    summary: '작업 정보를 변경했습니다.',
    detail: siblingCount.value > 0
      ? `함께 진행한 기록 ${siblingCount.value}건에도 반영되었습니다.`
      : undefined,
    life: 3000,
  });
  confirmDialog.value = false;
  editDialog.value = false;
}
</script>

<template>
  <div class="col g-4">
    <div class="mock-banner">
      <i class="pi pi-info-circle" />
      <span>화면 확인용 임시 데이터입니다. API 연동 시 <code>src/mock</code> 을 제거하세요.</span>
    </div>

    <!-- ══ 1. 조회 조건 ═════════════════════════════════ -->
    <Card>
      <template #content>
        <div class="row wrap g-5">
          <div class="field">
            <label>날짜</label>
            <DatePicker v-model="date" dateFormat="yy-mm-dd" showIcon iconDisplay="input" :manualInput="false" />
          </div>

          <div class="field">
            <label>상태</label>
            <SelectButton v-model="statusFilter" :options="statusFilters"
                          optionLabel="label" optionValue="value" :allowEmpty="false" />
          </div>

          <!-- 종료를 잊고 방치된 건이 있으면 눈에 띄게 (§8-3) -->
          <div v-if="openCount" class="field" style="justify-content: flex-end">
            <span class="open-warn">
              <i class="pi pi-exclamation-circle" />
              미완료 {{ openCount }}건
            </span>
          </div>
        </div>

        <div class="row g-2 mt-3 hint">
          <i class="pi pi-info-circle" style="font-size: 12px" />
          <span>작업을 <strong>시작한 날짜</strong>를 기준으로 보여줍니다. 밤을 넘겨 이어진 작업은 시작한 날에 표시됩니다.</span>
        </div>
      </template>
    </Card>

    <!-- ══ 2. 목록 (카드) ═══════════════════════════════ -->
    <div v-if="!filteredLogs.length" class="empty">
      선택한 날짜에 작업 이력이 없습니다.
    </div>

    <div v-else class="log-list">
      <button
        v-for="log in filteredLogs" :key="log.id"
        class="log-card" :class="{ picked: selectedId === log.id }"
        @click="selectedId = log.id"
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

        <div class="log-id num">#{{ log.id }}</div>
      </button>
    </div>

    <!-- ══ 3. 상세 ══════════════════════════════════════ -->
    <div v-if="!selectedId" class="empty">
      위 목록에서 작업을 선택해 주세요.
    </div>

    <template v-else-if="detail">
      <!-- 요약 -->
      <Card>
        <template #title>
          <div class="row between wrap g-2">
            <div class="row g-2">
              <span>작업이력 #{{ detail.id }}</span>
              <Tag :value="STATUS_LABEL[detail.status]" :severity="STATUS_SEVERITY[detail.status]" />
            </div>
            <Button label="작업지시 수정" icon="pi pi-pencil" severity="secondary" outlined
                    size="small" @click="openEdit" />
          </div>
        </template>

        <template #content>
          <div class="row wrap g-3">
            <div class="metric"><div class="k">라인</div><div class="v sm">{{ detail.lineName }}</div></div>
            <div class="metric"><div class="k">공정</div><div class="v sm">{{ detail.processName }}</div></div>
            <div class="metric"><div class="k">설비</div><div class="v sm">{{ detail.equipmentName ?? '수작업' }}</div></div>
            <div class="metric"><div class="k">작업자</div><div class="v sm">{{ detail.workerName }}</div></div>
            <div class="metric"><div class="k">실제 시작</div><div class="v sm num">{{ mmddhhmm(detail.startTime) }}</div></div>
            <div class="metric">
              <div class="k">실제 종료</div>
              <div class="v sm num">{{ detail.endTime ? mmddhhmm(detail.endTime) : '-' }}</div>
            </div>
            <div v-if="isCompleted" class="metric">
              <div class="k">실적 수량</div><div class="v num">{{ detail.actualQty }}<small>개</small></div>
            </div>
          </div>
        </template>
      </Card>

      <!-- 시간 분해 — 완료 건에만 (§3-5의 캐시 컬럼이 그때 채워지므로) -->
      <Card v-if="isCompleted && breakdown">
        <template #title>시간 분해 · 가동률</template>
        <template #content>
          <div class="col g-4">
            <div class="row wrap g-4">
              <div class="rate" :style="{ color: rateColor(breakdown.rate) }">
                {{ breakdown.rate.toFixed(1) }}<small>%</small>
              </div>
              <div class="rate-formula hint">
                실가동시간 {{ duration(breakdown.net) }} ÷ 조업시간 {{ duration(breakdown.elapsed) }}
              </div>
            </div>

            <!-- 조업시간 100% 를 세 조각으로. 폭이 곧 손실의 크기라 원인이 빨리 읽힌다 -->
            <div class="stack">
              <div class="seg net" :style="{ width: breakdown.netPct + '%' }" />
              <div class="seg planned" :style="{ width: breakdown.plannedPct + '%' }" />
              <div class="seg unplanned" :style="{ width: breakdown.unplannedPct + '%' }" />
            </div>

            <div class="row wrap g-4 legend">
              <span><i class="sw net" />실가동 {{ duration(breakdown.net) }}</span>
              <span><i class="sw planned" />계획정지 {{ duration(breakdown.planned) }}</span>
              <span><i class="sw unplanned" />비가동 {{ duration(breakdown.unplanned) }}</span>
            </div>

            <div class="formula">
              <div><span>조업시간</span><b class="num">{{ duration(breakdown.elapsed) }}</b><em>종료 − 시작</em></div>
              <div><span>가동시간</span><b class="num">{{ duration(detail.operatingMinutes) }}</b><em>조업 − 계획정지</em></div>
              <div><span>실가동시간</span><b class="num">{{ duration(detail.netOperatingMinutes) }}</b><em>가동 − 비가동</em></div>
            </div>
          </div>
        </template>
      </Card>

      <!-- 완료 전: 분해 대신 경과 시간만. 캐시값이 없어 가동률을 확정할 수 없다 -->
      <Card v-else>
        <template #title>시간 분해 · 가동률</template>
        <template #content>
          <div class="pending-box">
            <i class="pi pi-clock" />
            <div class="col g-1">
              <strong>아직 진행 중인 작업입니다</strong>
              <span class="hint">
                가동률은 작업을 완료해야 계산됩니다.
                지금까지 <b class="num">{{ duration(runningMinutes) }}</b> 경과했습니다.
              </span>
            </div>
          </div>
        </template>
      </Card>

      <!-- 정지 이력 -->
      <Card>
        <template #title>정지 이력 ({{ detail.pauses.length }}건)</template>
        <template #content>
          <div v-if="!detail.pauses.length" class="empty">정지 기록이 없습니다.</div>

          <DataTable v-else :value="detail.pauses" size="small" stripedRows>
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
        </template>
      </Card>

      <!-- ══ 4. 작업지시 수정 다이얼로그 ═════════════════ -->
      <Dialog v-model:visible="editDialog" modal header="작업지시 수정"
              :style="{ width: '420px' }" :draggable="false">
        <div class="col g-3">
          <p class="hint" style="margin: 0">
            라인이나 공정을 잘못 선택한 경우 여기서 바로잡을 수 있습니다.
            기록된 작업 시간과 가동률은 그대로 유지됩니다.
          </p>

          <!-- 이 수정은 WorkLog 가 아니라 WorkOrder 를 바꾼다.
               같은 작업지시에 다른 작업자의 이력이 붙어 있으면 그쪽도 함께 바뀐다 -->
          <div v-if="siblingCount > 0" class="sibling-warn">
            <i class="pi pi-exclamation-triangle" />
            <div class="col g-1">
              <strong>같은 작업을 진행한 {{ siblingCount }}건도 함께 바뀝니다</strong>
              <span>
                라인 · 공정 · 설비는 작업 단위로 관리되어, 이 작업을 함께 진행한
                다른 작업자의 기록에도 똑같이 반영됩니다.
              </span>
            </div>
          </div>

          <div class="field">
            <label>라인</label>
            <Select v-model="editLineId" :options="lines" optionLabel="name" optionValue="id"
                    placeholder="라인 선택" fluid @change="onEditLineChange"
                    emptyMessage="등록된 라인이 없습니다." />
          </div>

          <div class="field">
            <label>공정</label>
            <Select v-model="editProcessId" :options="editProcessOptions" optionLabel="name" optionValue="id"
                    placeholder="공정 선택" :disabled="!editLineId" fluid
                    emptyMessage="선택 가능한 공정이 없습니다." />
          </div>

          <div class="field">
            <label>설비 <span class="opt">(선택)</span></label>
            <Select v-model="editEquipmentId" :options="equipments" optionLabel="name" optionValue="id"
                    placeholder="수작업" showClear fluid
                    emptyMessage="등록된 설비가 없습니다." />
          </div>
        </div>

        <template #footer>
          <Button label="취소" text severity="secondary" @click="editDialog = false" />
          <Button label="저장" :disabled="!canSaveEdit" @click="requestSave" />
        </template>
      </Dialog>

      <!-- ══ 5. 저장 확인 (형제 이력이 있을 때만) ═══════ -->
      <Dialog v-model:visible="confirmDialog" modal header="작업지시 변경 확인"
              :style="{ width: '420px' }" :draggable="false">
        <div class="col g-3">
          <div class="severe-strip">
            <i class="pi pi-exclamation-triangle" />
            <span>나 혼자만의 기록이 아닙니다.</span>
          </div>

          <p class="confirm-question">
            이 작업의 라인 · 공정 · 설비를 바꾸시겠습니까?
          </p>

          <ul class="confirm-notes">
            <li>같은 작업을 함께 진행한 <b>다른 작업자의 기록 {{ siblingCount }}건</b>도 함께 바뀝니다.</li>
            <li>이미 기록된 작업 시간과 가동률은 그대로 유지됩니다.</li>
            <li>바꾼 뒤에도 이 화면에서 다시 수정할 수 있습니다.</li>
          </ul>
        </div>

        <template #footer>
          <Button label="취소" text severity="secondary" autofocus @click="confirmDialog = false" />
          <Button label="변경" severity="warn" @click="saveEdit" />
        </template>
      </Dialog>
    </template>
  </div>
</template>

<style scoped>
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
  top: 10px; right: 12px;
  font-size: 10.5px;
  color: var(--text-muted);
}
.log-card :deep(.p-tag) { margin-right: 34px; }

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

/* ── 상세 ── */
.metric .v.sm { font-size: 16px; }

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

/* 저장 확인 팝업 */
.severe-strip {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 11px;
  border-radius: 8px;
  background: #fff5e6;
  color: #8a6320;
  font-size: 12.5px;
  font-weight: 600;
}

.confirm-question {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-strong);
  line-height: 1.45;
}

.confirm-notes {
  margin: 0;
  padding-left: 18px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: var(--text-normal);
  line-height: 1.5;
}
.confirm-notes b { color: var(--text-strong); }

/* 형제 작업이력 동시 변경 경고 */
.sibling-warn {
  display: flex;
  align-items: flex-start;
  gap: 9px;
  padding: 10px 12px;
  border-radius: 8px;
  background: #fff5e6;
  border: 1px solid #f0d5a8;
  color: #8a6320;
  font-size: 12.5px;
  line-height: 1.5;
}
.sibling-warn > i { margin-top: 2px; }
.sibling-warn strong { font-size: 13px; }

.opt { font-weight: 400; color: var(--text-muted); }
</style>

<script setup>
/* ════════════════════════════════════════════════════════════════
 * 마스터데이터 관리 화면 (설계 §6-4)
 *
 * [라인 | 공정 | 작업자 | 설비] 4개 탭. 각 탭은 표 + 등록/수정 팝업 구성.
 *
 * 【탭마다 다른 점】
 *  · 라인 / 설비 : 이름 + 활성 여부만
 *  · 공정        : + 소속 라인 멀티셀렉트 (Line ↔ Process N:M, §4-8)
 *  · 작업자      : + 소속 공정 멀티셀렉트 (Worker ↔ Process N:M, §4-7)
 *
 * 【"비활성 포함" 체크박스가 반드시 있어야 하는 이유】 (§4-12)
 *  작업 이력이 걸린 마스터는 삭제 대신 IsActive=false 로 비활성화된다.
 *  그런데 목록에서 아예 숨겨버리면 그 항목의 id 를 알 방법이 없어져
 *  되살릴 수단이 사라진다. 이 체크박스가 복구의 유일한 진입점이다.
 *
 * 【삭제 결과가 두 갈래인 이유】 (§4-12)
 *  · 작업지시/작업이력 참조가 있음 → IsActive=false (행 보존, 화면에서만 숨김)
 *  · 참조 없음                     → 실제 DELETE
 *  어느 쪽이 일어났는지 화면이 알아야 토스트 문구를 고를 수 있어서,
 *  DELETE 응답이 204 가 아니라 {result, historyCount} 형태다 (§5).
 *
 * 【조인 체크 해제에 확인 팝업을 붙이지 않는 이유】 (§6-1)
 *  "1라인에 검사공정이 매핑돼 있었다"는 관계 데이터라 잃는 정보가 없다.
 *  확인창을 남발하면 정작 위험한 삭제의 경고력이 떨어진다.
 *
 * ────────────────────────────────────────────────────────────────
 * TODO(연동) — 이 화면이 사용할 API (탭마다 동일한 패턴)
 *   GET    /api/lines?includeInactive={bool}
 *   POST   /api/lines                 {name}
 *   PUT    /api/lines/{id}            {name, isActive}     ← isActive:true 로 보내면 복구
 *   DELETE /api/lines/{id}            → {result:'deleted'} 또는 {result:'deactivated', historyCount}
 *
 *   processes / workers / equipment 도 경로만 다르고 형태는 같다.
 *   공정은 요청/응답에 lineIds, 작업자는 processIds 가 추가된다.
 *   조인 갱신은 체크된 id 목록 전체를 보내면 서버가 diff 로 변경분만 반영한다 (§4-11).
 *
 *   에러: 이름 중복이면 409 + ProblemDetails. body.detail 을 그대로 토스트에 띄우면 된다.
 * ════════════════════════════════════════════════════════════════ */

import { ref, computed } from 'vue';
import { useToast } from 'primevue/usetoast';

/* ── 사용하는 PrimeVue 위젯 ──────────────────────────────────
 * Tabs/TabList/Tab/TabPanels/TabPanel
 *              : 탭 묶음. v-model:value 에 현재 탭 값을 넣는다.
 *                Tab 의 value 와 TabPanel 의 value 가 짝을 이룬다
 * DataTable    : 표. dataKey 로 행 식별자를 지정하면 갱신 시 깜빡임이 줄어든다
 *                size="small"=행 높이 축소, stripedRows=줄무늬
 * Column       : 열. #body 슬롯으로 셀을 직접 그린다(배지, 버튼 등)
 * Checkbox     : binary 로 true/false 토글 ("비활성 포함")
 * InputText    : 한 줄 텍스트 입력 (이름)
 * MultiSelect  : 여러 개를 고르는 드롭다운. N:M 조합 편집에 쓴다
 *                display="chip" 이면 고른 항목이 칩으로 보이고,
 *                filter 를 켜면 항목이 많을 때 검색창이 생긴다
 *                maxSelectedLabels 는 칩 대신 "N개 선택"으로 접히는 기준
 * Dialog       : 등록/수정 모달 팝업
 * Tag          : 활성/비활성 배지
 * ─────────────────────────────────────────────────────────── */
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';
import TabPanels from 'primevue/tabpanels';
import TabPanel from 'primevue/tabpanel';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Checkbox from 'primevue/checkbox';
import InputText from 'primevue/inputtext';
import MultiSelect from 'primevue/multiselect';
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Card from 'primevue/card';

import ConfirmDeleteDialog from './common/ConfirmDeleteDialog.vue';
import * as mock from '../mock/index.js';

const toast = useToast();

/* ── 탭 정의 ────────────────────────────────────────────
 * join 이 있는 탭만 멀티셀렉트 열이 생긴다 */
const TABS = [
  { key: 'line', label: '라인', unit: '라인', join: null },
  { key: 'process', label: '공정', unit: '공정', join: { field: 'lineIds', label: '소속 라인' } },
  { key: 'worker', label: '작업자', unit: '작업자', join: { field: 'processIds', label: '소속 공정' } },
  { key: 'equipment', label: '설비', unit: '설비', join: null },
];

const activeTab = ref('line');
const currentTab = computed(() => TABS.find((t) => t.key === activeTab.value));

/* ── 데이터 ──────────────────────────────────────────────
 * TODO(연동) 탭 전환 / includeInactive 변경 시 해당 목록 API 를 다시 호출한다 */
const rows = ref({
  line: [...mock.lines],
  process: [...mock.processes],
  worker: [...mock.workers],
  equipment: [...mock.equipments],
});

/* 멀티셀렉트의 선택지 — 공정 탭은 라인 목록, 작업자 탭은 공정 목록이 필요하다 */
const joinOptions = computed(() => {
  if (activeTab.value === 'process') return rows.value.line.filter((l) => l.isActive);
  if (activeTab.value === 'worker') return rows.value.process.filter((p) => p.isActive);
  return [];
});

/* 비활성 포함 여부 — 기본은 숨김, 체크하면 전부 보인다 (§4-12) */
const includeInactive = ref(false);

const visibleRows = computed(() => {
  const list = rows.value[activeTab.value] ?? [];
  return includeInactive.value ? list : list.filter((r) => r.isActive);
});

const inactiveCount = computed(
  () => (rows.value[activeTab.value] ?? []).filter((r) => !r.isActive).length,
);

/* 조인된 id 목록을 이름 문자열로 — 표에서 "1라인, 2라인" 처럼 보여주기 위해 */
function joinNames(row) {
  const join = currentTab.value?.join;
  if (!join) return '';
  const source = activeTab.value === 'process' ? rows.value.line : rows.value.process;
  return (row[join.field] ?? [])
    .map((id) => source.find((s) => s.id === id)?.name)
    .filter(Boolean)
    .join(', ');
}

/* ── 등록 / 수정 팝업 ───────────────────────────────────── */
const editDialog = ref(false);
const editing = ref(null);      // null 이면 신규 등록, 값이 있으면 수정
const form = ref({ name: '', isActive: true, joinIds: [] });

const dialogHeader = computed(() =>
  `${currentTab.value?.unit} ${editing.value ? '수정' : '등록'}`,
);

function openCreate() {
  editing.value = null;
  form.value = { name: '', isActive: true, joinIds: [] };
  editDialog.value = true;
}

function openEdit(row) {
  editing.value = row;
  form.value = {
    name: row.name,
    isActive: row.isActive,
    joinIds: currentTab.value?.join ? [...(row[currentTab.value.join.field] ?? [])] : [],
  };
  editDialog.value = true;
}

/* 이름은 필수. 서버도 [Required]/[MaxLength(100)] 로 다시 검증한다 */
const canSave = computed(() => form.value.name.trim().length > 0);

/* 이번 저장에서 해제되는 조인 id 들 (기존에는 있었는데 지금 체크가 풀린 것).
 * 해제 자체는 관계 데이터라 잃는 정보가 없지만(§6-1), 부작용이 하나 있다 —
 * 그 조합으로 이미 만들어진 WorkOrder 는 그대로 남는데,
 * 이후 그 작업지시를 수정하려 하면 라인-공정 조합 검증(§4-8)에 걸려 409 가 난다.
 * 과거엔 유효했던 조합이 정정조차 불가능해지는 것이라 저장 전에 알려준다. */
const removedJoinIds = computed(() => {
  const join = currentTab.value?.join;
  if (!join || !editing.value) return [];
  const before = editing.value[join.field] ?? [];
  return before.filter((id) => !form.value.joinIds.includes(id));
});

const removedJoinNames = computed(() => {
  const source = activeTab.value === 'process' ? rows.value.line : rows.value.process;
  return removedJoinIds.value
    .map((id) => source.find((s) => s.id === id)?.name)
    .filter(Boolean);
});

function save() {
  // TODO(연동)
  //   신규: POST /api/{tab}          {name, ...joinIds}
  //   수정: PUT  /api/{tab}/{id}     {name, isActive, ...joinIds}
  //   조인 갱신은 체크된 전체 목록을 보내면 서버가 diff 로 처리한다 (§4-11)
  //   409(이름 중복)면 body.detail 을 토스트 severity:'error' 로 띄운다
  toast.add({
    severity: 'success',
    summary: `'${form.value.name}'을(를) 저장했습니다.`,
    life: 2500,
  });
  editDialog.value = false;
}

/* ── 삭제 ────────────────────────────────────────────────
 * 결과가 삭제/비활성화 두 갈래라 팝업에서 미리 조건을 알려준다 (§6-1) */
const deleteDialog = ref(false);
const deleting = ref(null);

function askDelete(row) {
  deleting.value = row;
  deleteDialog.value = true;
}

function confirmDelete() {
  // TODO(연동) DELETE /api/{tab}/{id}
  //   응답 {result:'deleted'}                     → 아래 "삭제했습니다" 쪽
  //   응답 {result:'deactivated', historyCount:N} → 아래 "숨김 처리했습니다" 쪽
  //   두 갈래를 구분해 알려주기 위해 DELETE 응답이 204 가 아니라 본문을 담는다(§5).
  //   문구는 연동 후에도 그대로 쓰면 된다.
  const name = deleting.value?.name;
  const hasHistory = false;   // TODO(연동) 응답의 result === 'deactivated' 로 교체

  toast.add(hasHistory
    ? {
      severity: 'info',
      summary: `'${name}'을(를) 목록에서 숨겼습니다.`,
      detail: '작업 기록이 있어 삭제하지 않았습니다. 지난 실적은 그대로 유지됩니다.',
      life: 4000,
    }
    : {
      severity: 'success',
      summary: `'${name}'을(를) 삭제했습니다.`,
      life: 2500,
    });

  deleteDialog.value = false;
  deleting.value = null;
}

/* 비활성 항목을 되살리는 경로 — 전용 API 없이 일반 수정(PUT)으로 처리한다 */
function reactivate(row) {
  // TODO(연동) PUT /api/{tab}/{id}  {name: row.name, isActive: true}
  toast.add({
    severity: 'success',
    summary: `'${row.name}'을(를) 다시 사용할 수 있습니다.`,
    life: 2500,
  });
}
</script>

<template>
  <div class="col g-4">
    <div class="mock-banner">
      <i class="pi pi-info-circle" />
      <span>화면 확인용 임시 데이터입니다. API 연동 시 <code>src/mock</code> 을 제거하세요.</span>
    </div>

    <Tabs v-model:value="activeTab">
      <TabList>
        <Tab v-for="t in TABS" :key="t.key" :value="t.key">{{ t.label }}</Tab>
      </TabList>

      <TabPanels>
        <TabPanel v-for="t in TABS" :key="t.key" :value="t.key">
          <Card>
            <template #content>
              <!-- ── 툴바 ── -->
              <div class="row between wrap g-3 mb-3">
                <div class="row g-4">
                  <Button :label="`${t.unit} 등록`" icon="pi pi-plus" size="small" @click="openCreate" />

                  <!-- 비활성 항목을 확인/복구할 수 있는 유일한 경로 (§4-12) -->
                  <label class="row g-2 toggle">
                    <Checkbox v-model="includeInactive" binary :inputId="`inc-${t.key}`" />
                    <span>비활성 포함</span>
                    <span v-if="inactiveCount" class="count">{{ inactiveCount }}</span>
                  </label>
                </div>

                <span class="hint">{{ visibleRows.length }}건</span>
              </div>

              <!-- ── 표 ── -->
              <div v-if="!visibleRows.length" class="empty">
                등록된 {{ t.unit }}이(가) 없습니다.
              </div>

              <DataTable v-else :value="visibleRows" dataKey="id" size="small" stripedRows>
                <Column field="id" header="ID" style="width: 70px">
                  <template #body="{ data }"><span class="num">{{ data.id }}</span></template>
                </Column>

                <Column field="name" :header="`${t.unit}명`">
                  <template #body="{ data }">
                    <!-- 비활성 항목은 흐리게 — 목록에서 바로 구분되게 -->
                    <span :class="{ dim: !data.isActive }">{{ data.name }}</span>
                  </template>
                </Column>

                <!-- 공정/작업자 탭에만 생기는 N:M 열 -->
                <Column v-if="t.join" :header="t.join.label">
                  <template #body="{ data }">
                    <span v-if="joinNames(data)">{{ joinNames(data) }}</span>
                    <!-- 배정이 하나도 없는 것도 정상 상태다 (§6-4) -->
                    <span v-else class="hint">미지정</span>
                  </template>
                </Column>

                <Column header="상태" style="width: 100px">
                  <template #body="{ data }">
                    <Tag :value="data.isActive ? '활성' : '비활성'"
                         :severity="data.isActive ? 'success' : 'secondary'" />
                  </template>
                </Column>

                <Column header="" style="width: 190px">
                  <template #body="{ data }">
                    <div class="row g-1 end">
                      <Button icon="pi pi-pencil" text rounded size="small"
                              severity="secondary" @click="openEdit(data)" />
                      <!-- 비활성 항목에만 복구 버튼 -->
                      <Button v-if="!data.isActive" label="활성화" text size="small"
                              @click="reactivate(data)" />
                      <Button v-else icon="pi pi-trash" text rounded size="small"
                              severity="danger" @click="askDelete(data)" />
                    </div>
                  </template>
                </Column>
              </DataTable>
            </template>
          </Card>
        </TabPanel>
      </TabPanels>
    </Tabs>

    <!-- ══ 등록 / 수정 팝업 ═════════════════════════════ -->
    <Dialog v-model:visible="editDialog" modal :header="dialogHeader"
            :style="{ width: '440px' }" :draggable="false">
      <div class="col g-3">
        <div class="field">
          <label>{{ currentTab?.unit }}명</label>
          <InputText v-model="form.name" :placeholder="`예: 1${currentTab?.unit}`"
                     maxlength="100" fluid autofocus />
          <span class="hint">100자 이내로 입력해 주세요. 이미 등록된 이름은 사용할 수 없습니다.</span>
        </div>

        <!-- N:M 조합 편집 — 체크된 전체 목록을 보내면 서버가 diff 로 반영한다 (§4-11) -->
        <div v-if="currentTab?.join" class="field">
          <label>{{ currentTab.join.label }}</label>
          <MultiSelect v-model="form.joinIds" :options="joinOptions"
                       optionLabel="name" optionValue="id"
                       display="chip" filter :maxSelectedLabels="4"
                       placeholder="선택 안 함" fluid />
          <span class="hint">연결만 해제될 뿐, 등록된 정보나 지난 작업 기록은 지워지지 않습니다.</span>
        </div>

        <!-- 해제된 조합이 있을 때만 뜨는 경고.
             그 조합으로 이미 등록된 작업지시는 남아 있어서, 나중에 수정하려 하면
             조합 검증(§4-8)에 걸려 정정이 막힐 수 있다 -->
        <div v-if="removedJoinNames.length" class="join-warn">
          <i class="pi pi-exclamation-triangle" />
          <div class="col g-1">
            <strong>{{ removedJoinNames.join(', ') }} 연결이 해제됩니다</strong>
            <span>
              지난 작업 기록은 그대로 남지만, 해제한 뒤에는 이 조합으로 새 작업을 시작할 수 없습니다.
              기존 작업의 라인 · 공정을 수정할 때도 선택하지 못하게 됩니다.
            </span>
          </div>
        </div>

        <!-- 수정일 때만: 비활성 항목을 되살리는 스위치 -->
        <label v-if="editing" class="row g-2 toggle">
          <Checkbox v-model="form.isActive" binary inputId="form-active" />
          <span>활성 상태</span>
        </label>
      </div>

      <template #footer>
        <Button label="취소" text severity="secondary" @click="editDialog = false" />
        <Button label="저장" :disabled="!canSave" @click="save" />
      </template>
    </Dialog>

    <!-- ══ 삭제 확인 ════════════════════════════════════ -->
    <ConfirmDeleteDialog
      v-model:visible="deleteDialog"
      :header="`${currentTab?.unit} 삭제`"
      :question="`'${deleting?.name}'을(를) 삭제하시겠습니까?`"
      :notes="[
        '작업 기록이 있으면 삭제되지 않고 목록에서만 숨겨집니다. 지난 실적은 그대로 유지됩니다.',
        '작업 기록이 없으면 완전히 삭제됩니다.',
        '숨겨진 항목은 「비활성 포함」을 켜서 다시 되살릴 수 있습니다.',
      ]"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.toggle {
  font-size: 13.5px;
  color: var(--text-normal);
  cursor: pointer;
  user-select: none;
}

/* 비활성 건수 배지 — 숨겨진 게 몇 개인지 알려준다 */
.count {
  font-size: 11px;
  font-weight: 700;
  background: var(--surface-muted);
  color: var(--text-muted);
  border-radius: 999px;
  padding: 1px 7px;
}

.dim {
  color: var(--text-muted);
  text-decoration: line-through;
  text-decoration-color: #cfd5de;
}

/* 조인 해제 경고 */
.join-warn {
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
.join-warn > i { margin-top: 2px; }
.join-warn strong { font-size: 13px; }
</style>

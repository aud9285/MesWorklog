<script setup>
/* ════════════════════════════════════════════════════════════════
 * 대시보드 — OEE 시간가동률 (설계 §6-2)
 *
 * 이 프로젝트의 목적 화면. 기간 × 그룹 두 축으로 가동률을 본다.
 *
 * 【UI 구성】
 *  1) 필터 바 — 기간(일/주/월/연) · 기준 날짜 · 그룹(작업자/공정/라인/설비)
 *  2) 요약 지표 — 전체 가동률, 조업시간 합, 실가동시간 합, 집계 대상 수
 *  3) 차트 — 그룹에 따라 막대 방향이 달라진다
 *       · 공정 / 라인   : 항목이 적어 세로 막대
 *       · 작업자 / 설비 : 항목이 많아 가로 막대 + 스크롤
 *
 * 【집계 기준 안내를 화면에 띄우는 이유】 (§3-6)
 *  자정을 넘기는 야간조는 "개시일"에 전부 귀속된다.
 *  (8/4 22시 시작 ~ 8/5 06시 종료 → 8/4 조회 시 표시)
 *  이건 버그가 아니라 제조업의 생산일자 개념에 맞춘 의도된 결정이라,
 *  보는 사람이 오해하지 않도록 화면에 기준을 명시한다.
 *
 * 【가동률 계산 방식】 (§3-4)
 *  그룹 내 개별 비율의 평균이 아니라, 분자/분모를 각각 합산한 뒤 나눈다.
 *  → 화면의 "전체 가동률"도 Σ실가동 / Σ조업 으로 계산해야 서버와 값이 맞는다.
 *
 * ────────────────────────────────────────────────────────────────
 * TODO(연동) — 이 화면이 사용할 API
 *   GET /api/work-logs/efficiency?period={day|month|year}&date={yyyy-MM-dd}&groupBy={worker|process|line|equipment}
 *       ※ 설계 §5 는 week 도 포함하지만 화면에서 폐기했다 (아래 periods 주석 참고).
 *         서버가 week 를 받아도 무방하나 호출하는 쪽이 없다.
 *
 *   응답: [{ groupId, groupName, totalElapsedMinutes, totalNetOperatingMinutes, availabilityPercent }]
 *
 *   period / date / groupBy 셋 중 하나라도 바뀌면 다시 조회하면 된다 →
 *     watch([period, baseDate, groupBy], fetchEfficiency, { immediate: true })
 *
 *   비활성(IsActive=false) 마스터도 집계에 포함된다. 퇴사자·폐기 설비의 과거 실적이
 *   사라지면 안 되기 때문이라 프론트에서 따로 거르지 않는다 (§4-12)
 * ════════════════════════════════════════════════════════════════ */

import { ref, computed } from 'vue';

/* ── 사용하는 PrimeVue 위젯 ──────────────────────────────────
 * SelectButton : 버튼 묶음형 라디오. 선택지가 적고 항상 보여야 할 때 드롭다운보다 낫다
 *                options=목록, optionLabel/optionValue 는 Select 와 동일,
 *                allowEmpty=false 로 두면 이미 선택된 버튼을 다시 눌러도 해제되지 않는다
 *                (해제되면 조회 조건이 사라져 화면이 비어버리므로 반드시 필요)
 * DatePicker   : 달력. 여기서는 시각이 필요 없어 showTime 없이 날짜만 고른다
 *                view 속성으로 달력 단위를 바꾼다 → month 면 월 단위, year 면 연 단위 선택기
 *                dateFormat 은 입력칸에 보이는 형식 (yy=4자리 연도, mm=월, dd=일)
 * Card         : 카드 컨테이너
 * ─────────────────────────────────────────────────────────── */
import SelectButton from 'primevue/selectbutton';
import DatePicker from 'primevue/datepicker';
import Card from 'primevue/card';
import Button from 'primevue/button';

import RateBarChart from './common/RateBarChart.vue';
import { duration, periodRangeLabel } from '../utils/format.js';
import * as mock from '../mock/index.js';

/* ── 필터 상태 ──────────────────────────────────────────── */
/* 주 단위는 폐기했다.
 * PrimeVue DatePicker 의 view 는 date/month/year 뿐이라 주 선택기가 없어서,
 * 날짜를 고른 뒤 "그 날이 속한 주"로 해석해야 했는데
 * 8/9 를 고르면 8/3~8/9 인지 8/9~8/15 인지 화면만 봐서는 알 수 없었다.
 * 범위 문구로 보완할 수는 있었지만, 일/월/연으로도 조회 목적이 충족되어 뺐다. */
const periods = [
  { label: '일', value: 'day' },
  { label: '월', value: 'month' },
  { label: '연', value: 'year' },
];

const groups = [
  { label: '작업자', value: 'worker' },
  { label: '공정', value: 'process' },
  { label: '라인', value: 'line' },
  { label: '설비', value: 'equipment' },
];

const period = ref('day');
const groupBy = ref('worker');
const baseDate = ref(new Date());

/* 기간 단위에 따라 달력의 선택 단위를 바꾼다.
 * 월 집계인데 일 단위 달력을 띄우면 "며칠을 골라야 하지?"라는 혼란이 생긴다 */
const datePickerView = computed(() => {
  if (period.value === 'month') return 'month';
  if (period.value === 'year') return 'year';
  return 'date';
});

const dateFormat = computed(() => ({
  day: 'yy-mm-dd',
  month: 'yy-mm',
  year: 'yy',
}[period.value]));

/* 실제로 집계되는 범위를 문구로 보여준다.
 * 월/연은 달력이 "2026-08" 처럼만 보여줘서 실제 시작/끝 날짜가 드러나지 않는다 */
const rangeLabel = computed(() => periodRangeLabel(period.value, baseDate.value));

/* 기간 단위만큼 앞뒤로 이동. 달력을 매번 여는 것보다 빠르다 */
function shift(direction) {
  const d = new Date(baseDate.value);
  if (period.value === 'day') d.setDate(d.getDate() + direction);
  else if (period.value === 'month') d.setMonth(d.getMonth() + direction);
  else d.setFullYear(d.getFullYear() + direction);
  baseDate.value = d;
}

/* 항목 수가 적은 축은 세로, 많은 축은 가로 (§6-2) */
const orientation = computed(() =>
  groupBy.value === 'process' || groupBy.value === 'line' ? 'vertical' : 'horizontal',
);

/* ── 집계 결과 ──────────────────────────────────────────
 * TODO(연동) 위 TODO 블록의 efficiency API 응답으로 교체.
 *   지금은 groupBy 에 맞는 목 데이터를 골라 보여준다 */
const rows = computed(() => mock.efficiency[groupBy.value] ?? []);

/* 요약 지표 — 개별 비율의 평균이 아니라 합계끼리 나눈다 (§3-4) */
const totals = computed(() => {
  const elapsed = rows.value.reduce((s, r) => s + r.totalElapsedMinutes, 0);
  const net = rows.value.reduce((s, r) => s + r.totalNetOperatingMinutes, 0);
  return {
    elapsed,
    net,
    percent: elapsed === 0 ? 0 : (net / elapsed) * 100,
    count: rows.value.length,
  };
});

const groupLabel = computed(
  () => groups.find((g) => g.value === groupBy.value)?.label ?? '',
);
</script>

<template>
  <div class="col g-4">
    <div class="mock-banner">
      <i class="pi pi-info-circle" />
      <span>화면 확인용 임시 데이터입니다. API 연동 시 <code>src/mock</code> 을 제거하세요.</span>
    </div>

    <!-- ══ 1. 필터 바 ═══════════════════════════════════ -->
    <Card>
      <template #content>
        <div class="row wrap g-5">
          <div class="field">
            <label>기간</label>
            <!-- allowEmpty=false : 이미 눌린 버튼을 다시 눌러도 해제되지 않게 한다 -->
            <SelectButton v-model="period" :options="periods"
                          optionLabel="label" optionValue="value" :allowEmpty="false" />
          </div>

          <div class="field">
            <label>기준 날짜</label>
            <div class="row g-1">
              <!-- 기간 단위만큼 앞뒤로 이동 -->
              <Button icon="pi pi-chevron-left" text severity="secondary" size="small"
                      aria-label="이전" @click="shift(-1)" />
              <!-- 기간 단위에 따라 달력이 일/월/연 선택기로 바뀐다 -->
              <DatePicker v-model="baseDate" :view="datePickerView" :dateFormat="dateFormat"
                          showIcon iconDisplay="input" />
              <Button icon="pi pi-chevron-right" text severity="secondary" size="small"
                      aria-label="다음" @click="shift(1)" />
            </div>
          </div>

          <!-- 실제 집계 범위. "주" 조회에서 어느 주인지 모호해지는 걸 막는다 -->
          <div class="field">
            <label>집계 범위</label>
            <div class="range-label num">{{ rangeLabel }}</div>
          </div>

          <div class="field">
            <label>그룹</label>
            <SelectButton v-model="groupBy" :options="groups"
                          optionLabel="label" optionValue="value" :allowEmpty="false" />
          </div>
        </div>

        <!-- 야간조 귀속 기준 안내 (§3-6). 없으면 "8/5 새벽 작업이 왜 8/4에 있지?"라는 오해가 생긴다 -->
        <div class="row g-2 mt-3 hint">
          <i class="pi pi-info-circle" style="font-size: 12px" />
          <span>작업을 <strong>시작한 날짜</strong>를 기준으로 집계합니다. 밤을 넘겨 이어진 작업은 시작한 날에 포함됩니다.</span>
        </div>
      </template>
    </Card>

    <!-- ══ 2. 요약 지표 ═════════════════════════════════ -->
    <div class="row wrap g-3">
      <div class="metric summary">
        <div class="k">전체 가동률</div>
        <div class="v">{{ totals.percent.toFixed(1) }}<small>%</small></div>
      </div>
      <div class="metric">
        <div class="k">총 조업시간</div>
        <div class="v">{{ duration(totals.elapsed) }}</div>
      </div>
      <div class="metric">
        <div class="k">총 실가동시간</div>
        <div class="v">{{ duration(totals.net) }}</div>
      </div>
      <div class="metric">
        <div class="k">{{ groupLabel }}</div>
        <div class="v">{{ totals.count }}<small>{{ groupLabel === '작업자' ? '명' : '개' }}</small></div>
      </div>
    </div>

    <!-- ══ 3. 차트 ══════════════════════════════════════ -->
    <Card>
      <template #title>
        <div class="row between wrap g-2">
          <span>{{ groupLabel }}별 가동률</span>
          <!-- 색이 무엇을 뜻하는지 -->
          <div class="row g-3 legend">
            <span><i class="sw" style="background: var(--ok)" />85% 이상</span>
            <span><i class="sw" style="background: var(--brand)" />70~85%</span>
            <span><i class="sw" style="background: var(--warn)" />55~70%</span>
            <span><i class="sw" style="background: var(--danger)" />55% 미만</span>
          </div>
        </div>
      </template>

      <template #content>
        <RateBarChart :items="rows" :orientation="orientation" />
      </template>
    </Card>
  </div>
</template>

<style scoped>
/* 전체 가동률만 강조 — 이 화면에서 가장 먼저 봐야 할 숫자 */
.summary {
  background: #eef4ff;
  border: 1px solid #d3e0fb;
}
.summary .v { color: var(--brand); }

/* 집계 범위 문구 — 입력칸이 아니라 읽기 전용 표시라 배경만 깔았다 */
.range-label {
  display: flex;
  align-items: center;
  height: 40px;
  padding: 0 12px;
  border-radius: 7px;
  background: var(--surface-muted);
  font-size: 13px;
  color: var(--text-strong);
  white-space: nowrap;
}

.legend {
  font-size: 11.5px;
  color: var(--text-muted);
}
.legend span {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.sw {
  width: 9px;
  height: 9px;
  border-radius: 2px;
  display: inline-block;
}
</style>

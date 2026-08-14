<script setup>
/* 가동률 막대 차트 (설계 §6-2)
 *
 * chart.js를 설치하지 않고 CSS만으로 그린다. 표현할 게 "0~100% 막대 하나"뿐이라
 * 라이브러리를 얹을 이유가 없고, 의존성이 없어 빌드도 가벼워진다.
 *
 * orientation:
 *   'vertical'   — 공정 / 라인. 항목 수가 적어 가로로 나열해도 다 보인다
 *   'horizontal' — 작업자 / 설비. 항목이 많아 세로로 쌓고 스크롤한다
 *
 * 이 컴포넌트는 순수 표시 전용이다. 데이터 조회는 부모(Dashboard)가 담당한다.
 */
import { computed } from 'vue';
import { duration, rateColor } from '../../utils/format.js';

const props = defineProps({
  /** [{ groupId, groupName, availabilityPercent, totalElapsedMinutes, totalNetOperatingMinutes }] */
  items: { type: Array, default: () => [] },
  orientation: { type: String, default: 'vertical' },
});

const isVertical = computed(() => props.orientation === 'vertical');
</script>

<template>
  <!-- 항목이 없을 때: 조회 결과가 0건인 것도 정상 상태이므로 에러가 아닌 안내로 표시 -->
  <div v-if="!items.length" class="empty">
    선택한 기간에 기록된 작업이 없습니다.
  </div>

  <!-- 세로 막대: 공정 / 라인 -->
  <div v-else-if="isVertical" class="v-chart">
    <div v-for="item in items" :key="item.groupId" class="v-item">
      <span class="v-value num">{{ item.availabilityPercent.toFixed(1) }}%</span>
      <div class="v-track">
        <!-- 높이를 가동률(%)에 그대로 매핑. 색은 구간별로 달라진다 -->
        <div
          class="v-fill"
          :style="{
            height: item.availabilityPercent + '%',
            background: rateColor(item.availabilityPercent),
          }"
        />
      </div>
      <span class="v-label">{{ item.groupName }}</span>
      <span class="v-sub num">{{ duration(item.totalElapsedMinutes) }}</span>
    </div>
  </div>

  <!-- 가로 막대 + 스크롤: 작업자 / 설비 -->
  <div v-else class="h-chart">
    <div v-for="item in items" :key="item.groupId" class="h-item">
      <span class="h-label" :title="item.groupName">{{ item.groupName }}</span>
      <div class="h-track">
        <div
          class="h-fill"
          :style="{
            width: item.availabilityPercent + '%',
            background: rateColor(item.availabilityPercent),
          }"
        />
      </div>
      <span class="h-value num">{{ item.availabilityPercent.toFixed(1) }}%</span>
      <span class="h-sub num">{{ duration(item.totalNetOperatingMinutes) }} / {{ duration(item.totalElapsedMinutes) }}</span>
    </div>
  </div>
</template>

<style scoped>
/* ── 세로 막대 ─────────────────────────────── */
.v-chart {
  display: flex;
  align-items: flex-end;
  gap: 28px;
  padding: 8px 4px 0;
  overflow-x: auto;
}

.v-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  min-width: 76px;
}

.v-value {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-strong);
}

/* 트랙(회색 배경)이 100% 기준선 역할을 한다 */
.v-track {
  width: 46px;
  height: 190px;
  background: var(--surface-muted);
  border-radius: 7px;
  display: flex;
  align-items: flex-end;
  overflow: hidden;
}

.v-fill {
  width: 100%;
  border-radius: 7px 7px 0 0;
  transition: height 0.25s ease;
}

.v-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-strong);
}

.v-sub {
  font-size: 11.5px;
  color: var(--text-muted);
}

/* ── 가로 막대 ─────────────────────────────── */
.h-chart {
  display: flex;
  flex-direction: column;
  gap: 12px;
  /* 항목이 많아질 수 있어 높이를 제한하고 스크롤 (§6-2) */
  max-height: 340px;
  overflow-y: auto;
  padding-right: 6px;
}

.h-item {
  display: grid;
  grid-template-columns: 92px 1fr 54px;
  grid-template-rows: auto auto;
  align-items: center;
  column-gap: 10px;
  row-gap: 2px;
}

.h-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-strong);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.h-track {
  height: 20px;
  background: var(--surface-muted);
  border-radius: 6px;
  overflow: hidden;
}

.h-fill {
  height: 100%;
  border-radius: 6px;
  transition: width 0.25s ease;
}

.h-value {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-strong);
  text-align: right;
}

/* 가동 / 조업 을 막대 아래 작게 — 비율만으로는 규모를 알 수 없어서 */
.h-sub {
  grid-column: 2 / 4;
  font-size: 11px;
  color: var(--text-muted);
}
</style>

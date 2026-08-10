<script setup>
/* 삭제 확인 팝업 (설계 §6-1)
 *
 * PrimeVue의 ConfirmDialog는 message가 문자열 한 줄이라 \n이 줄바꿈으로 렌더링되지 않는다.
 * 경고 항목을 목록으로 보여줘야 하므로 Dialog 기반 재사용 컴포넌트로 만들었다.
 *
 * 안전한 쪽이 쉬운 쪽이 되도록: ESC / X / 배경 클릭은 모두 "취소"로 동작하고,
 * 기본 포커스도 취소에 둔다. 삭제만 danger 색을 쓴다.
 */
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';

defineProps({
  visible: { type: Boolean, default: false },
  /** 팝업 제목 — 예: "작업자 삭제" */
  header: { type: String, default: '삭제 확인' },
  /** 본문 첫 줄 질문 — 예: "'김철수'을(를) 삭제하시겠습니까?" */
  question: { type: String, required: true },
  /** 경고 항목들. 한 줄씩 목록으로 표시된다 */
  notes: { type: Array, default: () => [] },
  /** 되돌릴 수 없는 삭제일 때 true — 상단에 강조 띠가 붙는다 */
  severe: { type: Boolean, default: false },
  confirmLabel: { type: String, default: '삭제' },
});

defineEmits(['update:visible', 'confirm']);
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="header"
    :style="{ width: '420px' }"
    :draggable="false"
    @update:visible="$emit('update:visible', $event)"
  >
    <div class="col g-3">
      <div v-if="severe" class="severe-strip">
        <i class="pi pi-exclamation-triangle" />
        <span>이 작업은 되돌릴 수 없습니다.</span>
      </div>

      <p class="question">{{ question }}</p>

      <ul v-if="notes.length" class="notes">
        <li v-for="(note, i) in notes" :key="i">{{ note }}</li>
      </ul>
    </div>

    <template #footer>
      <Button label="취소" text severity="secondary" autofocus
              @click="$emit('update:visible', false)" />
      <Button :label="confirmLabel" severity="danger" @click="$emit('confirm')" />
    </template>
  </Dialog>
</template>

<style scoped>
.severe-strip {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 11px;
  border-radius: 8px;
  background: #fdeaea;
  color: #a53535;
  font-size: 12.5px;
  font-weight: 600;
}

.question {
  margin: 0;
  font-size: 15px;
  color: var(--text-strong);
  font-weight: 600;
  line-height: 1.45;
}

.notes {
  margin: 0;
  padding-left: 18px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: var(--text-normal);
  line-height: 1.5;
}
</style>

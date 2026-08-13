<script setup>
/* ════════════════════════════════════════════════════════════════
 * 앱 셸 — 헤더 + 화면 4개 탭 (설계 §6)
 *
 * 라우터를 쓰지 않고 탭으로 전환한다. 화면이 4개뿐이고 서로 독립적이라
 * URL 별 진입이 필요해지기 전까지는 vue-router 를 얹을 이유가 없다.
 * (나중에 "상세조회 링크 공유" 같은 요구가 생기면 그때 router 로 바꾸면 된다)
 *
 * <Toast /> 는 앱 전체에서 하나만 있으면 되므로 여기에 둔다.
 * 각 화면은 useToast() 로 알림을 띄운다. main.js 의 ToastService 등록이 전제다.
 * ════════════════════════════════════════════════════════════════ */

import { ref, watch } from 'vue';

/* Tabs 계열 : 탭 전환. Tab 의 value 와 TabPanel 의 value 가 짝을 이룬다
 * Toast      : 화면 우상단에 뜨는 알림. 위치/개수는 여기 하나로 통제된다 */
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';
import TabPanels from 'primevue/tabpanels';
import TabPanel from 'primevue/tabpanel';
import Toast from 'primevue/toast';

import WorkerDashboard from './components/WorkerDashboard.vue';
import Dashboard from './components/Dashboard.vue';
import DetailView from './components/DetailView.vue';
import MasterData from './components/MasterData.vue';

const activeTab = ref('work');

/* PrimeVue TabPanel은 탭을 나갔다 들어와도 컴포넌트를 없애지 않고 숨겼다 보여주기만 한다.
 * 그래서 WorkerDashboard의 onMounted(마스터데이터 로딩)는 앱 시작 시 딱 한 번만 실행되고,
 * 마스터데이터 화면에서 새로 등록해도 현장작업 화면은 그걸 모른다.
 * key를 바꿔주면 Vue가 기존 인스턴스를 버리고 새로 만들어서, 그때 onMounted가 다시 돈다 —
 * 즉 "현장작업 탭에 들어올 때마다 새로고침"을 흉내 내는 것 */
const workerDashboardKey = ref(0);
watch(activeTab, (tab) => {
  if (tab === 'work') workerDashboardKey.value++;
});
</script>

<template>
  <Toast position="top-right" />

  <header class="app-header">
    <div class="inner">
      <div class="brand">
        <span class="mark">MES</span>
        <div class="col">
          <h1>Worklog</h1>
          <span class="tagline">작업자별 작업 추적 · OEE 시간가동률</span>
        </div>
      </div>
    </div>
  </header>

  <main class="app-main">
    <Tabs v-model:value="activeTab">
      <TabList>
        <Tab value="work">현장 작업</Tab>
        <Tab value="dashboard">대시보드</Tab>
        <Tab value="detail">상세 조회</Tab>
        <Tab value="master">마스터데이터</Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="work"><WorkerDashboard :key="workerDashboardKey" /></TabPanel>
        <TabPanel value="dashboard"><Dashboard /></TabPanel>
        <TabPanel value="detail"><DetailView /></TabPanel>
        <TabPanel value="master"><MasterData /></TabPanel>
      </TabPanels>
    </Tabs>
  </main>
</template>

<style scoped>
.app-header {
  background: var(--surface-card);
  border-bottom: 1px solid var(--surface-border);
}

.inner {
  max-width: var(--app-max-width);
  margin: 0 auto;
  padding: 14px 20px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* 로고 자리 — 이미지 없이 텍스트 마크로 처리 */
.mark {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: 10px;
  background: var(--brand);
  color: #fff;
  font-size: 12.5px;
  font-weight: 800;
  letter-spacing: 0.02em;
}

h1 {
  font-size: 18px;
  line-height: 1.15;
}

.tagline {
  font-size: 12px;
  color: var(--text-muted);
}

.app-main {
  max-width: var(--app-max-width);
  margin: 0 auto;
  padding: 18px 20px 56px;
}

/* 탭 패널 기본 패딩을 줄이고 카드 간격은 각 화면이 관리하게 둔다 */
.app-main :deep(.p-tabpanels) {
  background: transparent;
  padding: 18px 0 0;
}
.app-main :deep(.p-tablist) {
  background: transparent;
}
</style>

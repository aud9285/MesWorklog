<script setup>
import { ref } from 'vue';
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';
import TabPanels from 'primevue/tabpanels';
import TabPanel from 'primevue/tabpanel';
import WorkerDashboard from './components/WorkerDashboard.vue';
import Dashboard from './components/Dashboard.vue';
import DetailView from './components/DetailView.vue';

const activeTab = ref('0');
</script>

<template>
  <div style="max-width: 1100px; margin: 0 auto; padding: 20px">
    <Tabs v-model:value="activeTab">
      <TabList>
        <Tab value="0">현장 작업 화면</Tab>
        <Tab value="1">대시보드</Tab>
        <Tab value="2">상세 조회</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="0">
          <WorkerDashboard />
        </TabPanel>
        <TabPanel value="1">
          <Dashboard />
        </TabPanel>
        <TabPanel value="2">
          <DetailView />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>
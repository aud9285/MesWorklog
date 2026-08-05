<script setup>
import { ref, watch, onMounted } from "vue";
import { api } from "../client.js";
import Select from "primevue/select";
import Card from "primevue/card";
import Tag from "primevue/tag";
import Button from "primevue/button";
import Dialog from "primevue/dialog";
import DatePicker from "primevue/datepicker";
import InputNumber from "primevue/inputnumber";

const STATUS_LABEL = {
    WAITING: "대기",
    IN_PROGRESS: "진행중",
    PAUSED: "일시정지",
    COMPLETED: "완료",
};
const STATUS_SEVERITY = {
    WAITING: "secondary",
    IN_PROGRESS: "info",
    PAUSED: "warn",
    COMPLETED: "success",
};

function todayStr() {
    return new Date().toISOString().slice(0, 10);
}

const processes = ref([]);              // 공정목록
const workers = ref([]);                // 공정의 작업자목록
const processId = ref(null);            // 선택된 공정
const workerId = ref(null);             // 선택된 작업자
const orders = ref([]);                 // 배정된 작업지시 목록
const logByOrder = ref({});             // 작업지시의 현재 진행 상태
const error = ref("");                  // 에러 메시지용

// 시간 선택 다이얼로그 상태
const pickerVisible = ref(false);       // 시간 선택 팝업이 열렸는지
const pickerMode = ref("");             // 지금 상태 (start, pause, resume, complete)
const pickerOrder = ref(null);          // 어떤 작업지시 동작인지
const pickerLog = ref(null);            // 해당 작업지시의 현재 로그
const pickerDateTime = ref(new Date()); // 선택한 시각
const actualQty = ref(0);               // 완료시 수량
const qtyDialogVisible = ref(false);    // 수량 팝업이 열렸는지

// 화면 처음뜰 때 실행
onMounted(() => {
    // 공정목록 api
    api
    .getProcesses()
    .then((v) => (processes.value = v))
    .catch((e) => (error.value = e.message));
});

// 공정을 선택하면(processId) 실행
watch(processId, (pid) => {
    workerId.value = null;
    workers.value = [];
    if (!pid) return;
    // 작업자목록 api
    api
        .getWorkers(pid)
        .then((v) => (workers.value = v))
        .catch((e) => (error.value = e.message));
});

// 작업지시 진행 상태 다시불러오기
function refresh() {
    if (!processId.value || !workerId.value) return;
    const date = todayStr();
    // 오늘의 작업지시 목록 오늘의 전체 타임라인 조회
    Promise.all([api.getWorkOrders(date, processId.value), api.getTimeline(date)])
    .then(([orderList, timeline]) => {
        orders.value = orderList;
        const mine = timeline.find(
        (t) => String(t.workerId) === String(workerId.value),
        );
        const map = {};
        (mine?.segments || []).forEach((seg) => {
        map[seg.workOrderId] = {
            workLogId: seg.workLogId,
            status: seg.status,
            start: seg.start,
        };
        });
        logByOrder.value = map;
    })
    .catch((e) => (error.value = e.message));
}
watch(workerId, refresh);

function openPicker(mode, order) {
    error.value = "";
    pickerMode.value = mode;
    pickerOrder.value = order;
    pickerLog.value = logByOrder.value[order.id];
    pickerDateTime.value = new Date();
    pickerVisible.value = true;
}

function toIso(date) {
    const pad = (n) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:00`;
}

async function confirmPicker() {
    const iso = toIso(pickerDateTime.value);
    try {
    if (pickerMode.value === "start") {
        await api.startWorkLog(pickerOrder.value.id, workerId.value, iso);
        pickerVisible.value = false;
        refresh();
    } else if (pickerMode.value === "pause") {
        await api.pauseWorkLog(pickerLog.value.workLogId, iso);
        pickerVisible.value = false;
        refresh();
    } else if (pickerMode.value === "resume") {
        await api.resumeWorkLog(pickerLog.value.workLogId, iso);
        pickerVisible.value = false;
        refresh();
    } else if (pickerMode.value === "complete") {
        pickerVisible.value = false;
        qtyDialogVisible.value = true; // 종료시각 확정 후 실적수량 입력 단계로
        pickerDateTime.value = new Date(pickerDateTime.value); // 값 유지
    }
    } catch (e) {
    error.value = e.message;
    pickerVisible.value = false;
    }
}

async function confirmComplete() {
    try {
    await api.completeWorkLog(
        pickerLog.value.workLogId,
        toIso(pickerDateTime.value),
        actualQty.value,
    );
    qtyDialogVisible.value = false;
    actualQty.value = 0;
    refresh();
    } catch (e) {
    error.value = e.message;
    qtyDialogVisible.value = false;
    }
}
</script>

<template>
    <div class="flex flex-column gap-3">
    <Card>
        <template #content>
        <div class="flex gap-2">
            <Select
            v-model="processId"
            :options="processes"
            optionLabel="name"
            optionValue="id"
            placeholder="공정 선택"
            />
            <Select
            v-model="workerId"
            :options="workers"
            optionLabel="name"
            optionValue="id"
            placeholder="작업자 선택"
            :disabled="!processId"
            />
        </div>
        </template>
    </Card>

    <p v-if="error" style="color: #e57373">{{ error }}</p>

    <Card v-for="order in orders" :key="order.id">
        <template #title>
        <div class="flex justify-content-between align-items-center">
            <span>작업지시 #{{ order.id }}</span>
            <Tag
            :value="STATUS_LABEL[logByOrder[order.id]?.status || 'WAITING']"
            :severity="
                STATUS_SEVERITY[logByOrder[order.id]?.status || 'WAITING']
            "
            />
        </div>
        </template>
        <template #content>
        <p>
            목표 {{ order.targetQty }}개 · 계획
            {{ order.plannedStart?.slice(11, 16) }} ~
            {{ order.plannedEnd?.slice(11, 16) }}
        </p>

        <div
            class="flex gap-2"
            v-if="
            !logByOrder[order.id] || logByOrder[order.id].status === 'WAITING'
            "
        >
            <Button label="시작시간 선택" @click="openPicker('start', order)" />
        </div>
        <div
            class="flex gap-2"
            v-else-if="logByOrder[order.id].status === 'IN_PROGRESS'"
        >
            <Button
            label="일시정지"
            severity="warn"
            @click="openPicker('pause', order)"
            />
            <Button
            label="종료"
            severity="success"
            @click="openPicker('complete', order)"
            />
        </div>
        <div
            class="flex gap-2"
            v-else-if="logByOrder[order.id].status === 'PAUSED'"
        >
            <Button label="재개" @click="openPicker('resume', order)" />
            <Button
            label="종료"
            severity="success"
            @click="openPicker('complete', order)"
            />
        </div>
        <Button v-else label="처리 완료됨" disabled />
        </template>
    </Card>

    <!-- 시간 선택 다이얼로그 -->
    <Dialog
        v-model:visible="pickerVisible"
        header="시간 선택"
        modal
        :style="{ width: '320px' }"
    >
        <DatePicker
        v-model="pickerDateTime"
        showTime
        hourFormat="24"
        stepMinute="10"
        showIcon
        fluid
        />
        <template #footer>
        <Button
            label="취소"
            severity="secondary"
            @click="pickerVisible = false"
        />
        <Button label="확인" @click="confirmPicker" />
        </template>
    </Dialog>

    <!-- 완료 시 실적수량 입력 -->
    <Dialog
        v-model:visible="qtyDialogVisible"
        header="실적 수량 입력"
        modal
        :style="{ width: '280px' }"
    >
        <InputNumber v-model="actualQty" fluid />
        <template #footer>
        <Button
            label="취소"
            severity="secondary"
            @click="qtyDialogVisible = false"
        />
        <Button label="완료 확정" @click="confirmComplete" />
        </template>
    </Dialog>
    </div>
</template>

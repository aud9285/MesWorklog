const BASE = "/api";

async function request(path, options = {}) {
    const res = await fetch(`${BASE}${path}`, {
        headers: { "Content-Type": "application/json" },
        ...options,
    });
    if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message || `요청 실패 (${res.status})`);
    }
    if (res.status === 204) return null;
    return res.json();
}

export const api = {
    getProcesses: () => request("/processes"),
    getWorkers: (processId) =>
        request(`/workers${processId ? `?processId=${processId}` : ""}`),
    getWorkOrders: (date, processId) =>
        request(
            `/work-orders?date=${date}${processId ? `&processId=${processId}` : ""}`,
        ),

    startWorkLog: (workOrderId, workerId, startTime) =>
        request("/work-logs/start", {
            method: "POST",
            body: JSON.stringify({ workOrderId, workerId, startTime }),
        }),
    pauseWorkLog: (id, pausedAt) =>
        request(`/work-logs/${id}/pause`, {
            method: "POST",
            body: JSON.stringify({ pausedAt }),
        }),
    resumeWorkLog: (id, resumedAt) =>
        request(`/work-logs/${id}/resume`, {
            method: "POST",
            body: JSON.stringify({ resumedAt }),
        }),
    completeWorkLog: (id, endTime, actualQty) =>
        request(`/work-logs/${id}/complete`, {
        method: "POST",
        body: JSON.stringify({ endTime, actualQty }),
        }),

    getWorkLogDetail: (id) => request(`/work-logs/${id}`),
    getTimeline: (date) => request(`/work-logs/timeline?date=${date}`),
    getUtilization: (date) => request(`/work-logs/utilization?date=${date}`),
};

const BASE = "/api";

// api 요청
async function request(path, options = {}) {
    const res = await fetch(`${BASE}${path}`, {
        headers: { "Content-Type": "application/json" },
        ...options,
    });
    if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        
        // 400 에러 메시지
        // 409, 404 에러 메시지
        const message = body.errors
            ? Object.values(body.errors).flat().join(" / ") // 필드별 에러 배열을 한 문장으로 합침
            : body.detail || `요청 실패 (${res.status})`;

        throw new Error(message);
    }
    if (res.status === 204) return null;
    return res.json();
}

export const api = {

    // 마스터데이터 — 라인 
    
    // 목록조회
    // GET /api/lines?includeInactive=false → [{id, name, isActive}]
    getLines: (includeInactive = false) =>
        request(`/lines?includeInactive=${includeInactive}`),

    // 단건조회
    // GET /api/lines/{id} → {id, name, isActive}
    getLine: (id) => 
        request(`/lines/${id}`),

    // 등록
    // POST /api/lines {name} → 201 + {id, name, isActive}
    createLine: (name) =>
        request("/lines", {
            method: "POST",
            body: JSON.stringify({ name }),
        }),

    // 수정
    // PUT /api/lines/{id} {name, isActive} → 200 + {id, name, isActive}
    updateLine: (id, name, isActive) =>
        request(`/lines/${id}`, {
            method: "PUT",
            body: JSON.stringify({ name, isActive }),
        }),
        
    //삭제
    // DELETE /api/lines/{id} → {result:"deleted"} 또는 {result:"deactivated", historyCount}
    deleteLine: (id) => 
        request(`/lines/${id}`, { 
            method: "DELETE" 
        }),

    // 마스터데이터 - 공정

    // 목록조회
    // GET /api/processes?includeInactive=false → [{id, name, isActive, lineIds}]
    getProcesses: (includeInactive = false) =>
        request(`/processes?includeInactive=${includeInactive}`),

    // 단건조회
    // GET /api/processes/{id} → {id, name, isActive, lineIds}
    getProcess: (id) => request(`/processes/${id}`),

    // 등록
    // POST /api/processes {name, lineIds} → 201
    createProcess: (name, lineIds) =>
        request("/processes", {
            method: "POST",
            body: JSON.stringify({ name, lineIds }),
        }),

    // 수정
    // PUT /api/processes/{id} {name, isActive, lineIds} → 200
    // lineIds는 체크된 전체 목록을 보내면 서버가 diff로 변경분만 반영 (JoinTableSync)
    updateProcess: (id, name, isActive, lineIds) =>
        request(`/processes/${id}`, {
            method: "PUT",
            body: JSON.stringify({ name, isActive, lineIds }),
        }),

    //삭제
    // DELETE /api/processes/{id} → {result:"deleted"} 또는 {result:"deactivated", historyCount}
    deleteProcess: (id) => 
        request(`/processes/${id}`, { method: "DELETE" }),

    // 마스터 데이터 - 작업자
    
    // 목록조회
    // GET /api/workers?includeInactive=false → [{id, name, isActive, processIds}]
    getWorkers: (includeInactive = false) =>
        request(`/workers?includeInactive=${includeInactive}`),

    // 단건조회
    // GET /api/workers/{id} → {id, name, isActive, processIds}
    getWorker: (id) => 
        request(`/workers/${id}`),

    // 등록
    // POST /api/workers {name, processIds} → 201 + {id, name, isActive, processIds}
    createWorker: (name, processIds) =>
        request("/workers", {
            method: "POST",
            body: JSON.stringify({ name, processIds }),
        }),

    // 수정
    // PUT /api/workers/{id} {name, isActive, processIds} → 200 + {id, name, isActive, processIds}
    updateWorker: (id, name, isActive, processIds) =>
        request(`/workers/${id}`, {
            method: "PUT",
            body: JSON.stringify({ name, isActive, processIds }),
        }),

    // 삭제
    // DELETE /api/workers/{id} → {result:"deleted"} 또는 {result:"deactivated", historyCount}
    deleteWorker: (id) => 
        request(`/workers/${id}`, { method: "DELETE" }),


    // 마스터 데이터 - 설비

    // 목록조회
    // GET /api/equipment?includeInactive=false → [{id, name, isActive}]
    getEquipments: (includeInactive = false) =>
        request(`/equipments?includeInactive=${includeInactive}`),

    // 단건조회
    // GET /api/equipment/{id} → {id, name, isActive}
    getEquipment: (id) => 
        request(`/equipments/${id}`),

    // 등록
    // POST /api/equipment {name} → 201 + {id, name, isActive}
    createEquipment: (name) =>
        request("/equipments", {
            method: "POST",
            body: JSON.stringify({ name }),
        }),

    // 수정
    // PUT /api/equipment/{id} {name, isActive} → 200 + {id, name, isActive}
    updateEquipment: (id, name, isActive) =>
        request(`/equipments/${id}`, {
            method: "PUT",
            body: JSON.stringify({ name, isActive }),
        }),

    // 삭제
    // DELETE /api/equipment/{id} → {result:"deleted"} 또는 {result:"deactivated", historyCount}
    deleteEquipment: (id) => 
        request(`/equipments/${id}`, { method: "DELETE" }),

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

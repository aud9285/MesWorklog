/* 화면 표시용 포맷 함수 모음. 순수 함수라 어디서든 재사용 가능하다. */

/** "2026-08-09T09:00:00" → "09:00" */
export function hhmm(iso) {
  if (!iso) return '-';
  return String(iso).slice(11, 16);
}

/** "2026-08-09T09:00:00" → "08-09 09:00" */
export function mmddhhmm(iso) {
  if (!iso) return '-';
  const s = String(iso);
  return `${s.slice(5, 10)} ${s.slice(11, 16)}`;
}

/** 분 → "9시간 0분" (0이면 "0분") */
export function duration(minutes) {
  if (minutes == null) return '-';
  const m = Math.max(0, Math.round(minutes));
  const h = Math.floor(m / 60);
  const rest = m % 60;
  if (h === 0) return `${rest}분`;
  return rest === 0 ? `${h}시간` : `${h}시간 ${rest}분`;
}

/** 두 시각 사이 분 단위 차이. 끝이 없으면 now 기준 */
export function minutesBetween(fromIso, toIso) {
  if (!fromIso) return 0;
  const from = new Date(fromIso).getTime();
  const to = toIso ? new Date(toIso).getTime() : Date.now();
  return Math.max(0, Math.round((to - from) / 60000));
}

/** 분을 10분 단위로 내림한다. 14:37 → 14:30
 *
 * 설계 §3-1 은 분을 00/10/20/30/40/50 으로 제한하는데,
 * DatePicker 의 stepMinute=10 은 "증감 단위"일 뿐 기본값까지 맞춰주지 않는다.
 * 초기값을 new Date() 로 두면 14:37 로 열리고, 사용자가 분을 안 건드리면 그대로 전송된다.
 * 그래서 팝업을 열 때 이 함수로 내려서 넣는다.
 *
 * 올림이 아니라 내림인 이유: 올리면 현재보다 미래가 되어 서버의 "미래 시각 금지"에 걸린다. */
export function floorTo10Minutes(date = new Date()) {
  const d = new Date(date);
  d.setMinutes(Math.floor(d.getMinutes() / 10) * 10, 0, 0);
  return d;
}

/** Date → "2026-08-09" */
export function ymd(date) {
  if (!date) return '';
  const p = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}`;
}

/** 기간 단위 + 기준 날짜 → 실제 집계 범위 문구
 *  월/연은 달력이 "2026-08" 처럼만 보여줘서 실제 범위가 드러나지 않으므로 함께 표시한다.
 *  (주 단위는 폐기했다 — PrimeVue DatePicker 에 주 선택기가 없어 날짜로 대신 골라야 했는데,
 *   8/9 를 고르면 어느 주인지 모호해지는 문제가 있었다) */
export function periodRangeLabel(period, date) {
  if (!date) return '';
  const y = date.getFullYear();
  const m = date.getMonth() + 1;

  if (period === 'day') return ymd(date);
  if (period === 'month') {
    // 말일은 다음 달 0일 = 이번 달 마지막 날
    const last = new Date(y, m, 0).getDate();
    return `${y}-${String(m).padStart(2, '0')}-01 ~ ${y}-${String(m).padStart(2, '0')}-${last}`;
  }
  return `${y}-01-01 ~ ${y}-12-31`;
}

/** Date → "2026-08-09T14:30:00" (서버가 기대하는 로컬 시각 문자열) */
export function toLocalIso(date) {
  if (!date) return null;
  const p = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}` +
    `T${p(date.getHours())}:${p(date.getMinutes())}:00`;
}

/** 가동률 구간별 색 — 낮을수록 붉게 */
export function rateColor(percent) {
  if (percent >= 85) return 'var(--ok)';
  if (percent >= 70) return 'var(--brand)';
  if (percent >= 55) return 'var(--warn)';
  return 'var(--danger)';
}

export const STATUS_LABEL = {
  InProgress: '진행중',
  Paused: '정지중',
  Completed: '완료',
};

export const STATUS_SEVERITY = {
  InProgress: 'info',
  Paused: 'warn',
  Completed: 'success',
};

export const CATEGORY_LABEL = {
  Planned: '계획정지',
  Unplanned: '비가동',
};

import { setDeviceOrientation } from '@apps-in-toss/web-framework';

const state = window.gate0Smoke;
const show = window.gate0OrientationStatus;
let revision = 0;

async function orient(type) {
  const request = ++revision;
  state.orientation = 'pending';
  try {
    await Promise.race([
      setDeviceOrientation({ type }),
      new Promise((_, reject) => setTimeout(() => reject(new Error('orientation timeout')), 5000)),
    ]);
    if (request !== revision) return;
    state.orientation = type;
    show('가로 화면 요청 완료 · 실제 기기 방향·소리는 직접 확인해 주세요');
  } catch (error) {
    if (request !== revision) return;
    state.orientation = 'unavailable';
    show('토스 가로 화면 설정 미확인 · 탭하여 재시도 · 일반 브라우저는 진단용입니다');
    console.warn('T2 Toss orientation unavailable:', error.message);
  }
}

orient('landscape');
document.addEventListener('pointerdown', () => {
  if (state.orientation === 'unavailable') orient('landscape');
});
window.addEventListener('pageshow', () => {
  if (state.orientation !== 'landscape' && state.orientation !== 'pending') orient('landscape');
});
window.addEventListener('pagehide', () => {
  ++revision;
  state.orientation = 'not-checked';
  setDeviceOrientation({ type:'portrait' }).catch(() => {});
});

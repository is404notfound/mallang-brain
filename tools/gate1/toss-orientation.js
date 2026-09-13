import { setDeviceOrientation } from '@apps-in-toss/web-framework';

const state = window.gate1Prototype;
const show = window.gate1OrientationStatus;
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
    show('가로 화면 요청 완료 · 시작을 눌러 첫 연주를 시험해 보세요');
  } catch (error) {
    if (request !== revision) return;
    state.orientation = 'unavailable';
    show('토스 가로 화면 설정을 확인하지 못했어요 · 화면을 눌러 다시 시도합니다');
    console.warn('Gate 1 Toss orientation unavailable:', error.message);
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

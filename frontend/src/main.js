import { createApp } from 'vue';
import PrimeVue from 'primevue/config';
import Aura from '@primevue/themes/aura';
import ToastService from 'primevue/toastservice';
import 'primeicons/primeicons.css';

import App from './App.vue';
import './styles/app.css';

const app = createApp(App);

app.use(PrimeVue, {
  theme: { preset: Aura, options: { darkModeSelector: '.dark-mode' } },
});

/* ToastService: useToast() 로 알림을 띄우려면 반드시 등록해야 한다.
 * 등록을 빠뜨리면 useToast() 가 undefined 를 반환해 호출 시점에 터진다.
 * 실제 렌더링은 App.vue 의 <Toast /> 가 담당한다 (서비스=발행, 컴포넌트=표시) */
app.use(ToastService);

app.mount('#app');

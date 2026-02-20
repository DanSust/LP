import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { register as registerSwiperElements } from 'swiper/element/bundle';

document.body.classList.add('no-scroll');

registerSwiperElements();

bootstrapApplication(App, appConfig)
  .then(() => {
    // 🔥 Снимаем класс ПОСЛЕ успешной загрузки
    document.body.classList.remove('no-scroll');
  })
  .catch((err) => console.error(err));

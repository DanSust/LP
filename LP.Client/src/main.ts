import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

document.body.classList.add('no-scroll');

bootstrapApplication(App, appConfig)
  .then(() => {
    // 🔥 Снимаем класс ПОСЛЕ успешной загрузки
    document.body.classList.remove('no-scroll');
  })
  .catch((err) => console.error(err));

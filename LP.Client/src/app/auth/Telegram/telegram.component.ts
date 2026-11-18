import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, inject, ViewChild } from "@angular/core";
import { TelegramAuthService } from "../../services/TelegramService";

@Component({
  selector: 'telegram-login',
  //template: '<div #telegramContainer id="telegram-login-container" style="min-height: 52px;"></div>'
  template: `<div [style.display]="isVisible ? 'block' : 'none'" #telegramContainer id="telegram-login-container"></div>`
  
})
export class TelegrmaComponent implements AfterViewInit {
  private tgAuth = inject(TelegramAuthService);
  private cdr = inject(ChangeDetectorRef);
  @ViewChild('telegramContainer', { static: true }) container!: ElementRef<HTMLElement>;
  public isVisible = false;

  async ngAfterViewInit() {
    const canAccessTelegram = await this.tgAuth.checkConnection();

    if (!canAccessTelegram) {
      this.isVisible = false;
      console.warn('Telegram blocked by network provider');
      return;
    }

    // Имя бота приходит с сервера: если бота переименовали,
    // клиент не нужно пересобирать.
    const botName = await this.tgAuth.getBotName();
    if (!botName) {
      this.isVisible = false;
      this.tgAuth.notifyNotConfigured();
      return;
    }

    // Контейнер должен быть видим ДО вставки скрипта:
    // виджет создаёт iframe внутри него и без layout может не отрисоваться.
    this.isVisible = true;
    this.cdr.detectChanges();

    try {
      await this.tgAuth.loadScript(this.container.nativeElement, botName);

      // Даём виджету создать iframe
      await new Promise((resolve) => setTimeout(resolve, 300));

      if (!this.container.nativeElement.querySelector('iframe')) {
        this.isVisible = false;
        this.tgAuth.notifyWidgetFailed();
      }
    } catch (e) {
      this.isVisible = false;
      this.tgAuth.notifyWidgetFailed();
      console.error('Telegram script failed to render', e);
    }
  }
}

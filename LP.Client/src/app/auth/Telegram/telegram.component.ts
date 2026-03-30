import { AfterViewInit, Component, ElementRef, inject, OnInit, ViewChild } from "@angular/core";
import { TelegramAuthService } from "../../services/TelegramService";

@Component({
  selector: 'telegram-login',
  //template: '<div #telegramContainer id="telegram-login-container" style="min-height: 52px;"></div>'
  template: `<div [style.display]="isVisible ? 'block' : 'none'" #telegramContainer id="telegram-login-container"></div>`
  
})
export class TelegrmaComponent implements AfterViewInit {
  private tgAuth = inject(TelegramAuthService);
  @ViewChild('telegramContainer', { static: true }) container!: ElementRef;
  public isVisible = false;

  async ngAfterViewInit() {
    const canAccessTelegram = await this.tgAuth.checkConnection();

    if (canAccessTelegram) {
      try {
        // Сначала пытаемся загрузить скрипт, и только если успешно — показываем
        await this.tgAuth.loadScript(this.container.nativeElement);
        this.isVisible = true; // Показываем только если скрипт РЕАЛЬНО загрузился
      } catch (e) {
        this.isVisible = false;
        console.error('Telegram script failed to render');
      }
    } else {
      this.isVisible = false;
      console.warn('Telegram blocked by network provider');
    }
  }
}

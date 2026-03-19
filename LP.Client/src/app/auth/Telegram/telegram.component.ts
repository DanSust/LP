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
        // Показываем блок и загружаем скрипт
        this.isVisible = true;
        await this.tgAuth.loadScript(this.container.nativeElement);
      } catch (e) {
        this.isVisible = false;
        console.warn('Telegram script failed after ping success');
      }
    }
  }
}

import { AfterViewInit, Component, ElementRef, inject, OnInit, ViewChild } from "@angular/core";
import { TelegramAuthService } from "../../services/TelegramService";

@Component({
  selector: 'telegram-login',
  template: '<div #telegramContainer id="telegram-login-container" style="min-height: 52px;"></div>'
  
})
export class TelegrmaComponent implements AfterViewInit {
  private tgAuth = inject(TelegramAuthService);
  @ViewChild('telegramContainer', { static: true }) container!: ElementRef;

  async ngAfterViewInit() {
    console.log('Container:', this.container?.nativeElement);
    if (this.container?.nativeElement) {
      await this.tgAuth.loadScript(this.container.nativeElement);
    }
  }
}

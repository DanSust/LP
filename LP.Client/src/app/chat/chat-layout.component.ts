import { Component, DestroyRef, HostListener, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatListComponent } from './chat-list.component';
import { ChatService } from './../services/ChatService';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-chat-layout',
  standalone: true,
  imports: [CommonModule, ChatListComponent, RouterOutlet],
  template: `
    <div class="container">
      <!-- На десктопе: всегда показываем оба блока -->
      <!-- На мобильном: показываем либо список чатов, либо чат -->
      @if (!isMobile() || !isChatOpen()) {
        <div class="sidebar">
          <app-chat-list></app-chat-list>
        </div>
      }

      @if (!isMobile() || isChatOpen()) {
        <div class="content">
          <router-outlet></router-outlet>
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      height: 100vh;
      height: 100dvh;
      overflow: hidden;
    }

    .container {
      width: 100%;
      height: calc(100vh - 130px);
      height: calc(100dvh - 130px);
      display: flex;
      overflow: hidden;
    }

    .sidebar {
      width: 30%;
      flex-shrink: 0;
      height: 100%;
      min-height: 0;
      box-sizing: border-box;
      overflow: hidden;
      display: flex;
      flex-direction: column;
    }

    .content {
      flex: 1;
      min-width: 0;
      height: 100%;
      min-height: 0;
      overflow: hidden;
      box-sizing: border-box;
    }

    @media (max-width: 768px) {
      .container {
        height: calc(100vh - var(--bottom-nav-h, 70px) - env(safe-area-inset-bottom, 0px));
        height: calc(100dvh - var(--bottom-nav-h, 70px) - env(safe-area-inset-bottom, 0px));
      }

      .sidebar {
        width: 100%;
        height: 100%;
      }

      .content {
        width: 100%;
        height: 100%;
      }
    }
  `]
})
export class ChatLayoutComponent {
  private chatService = inject(ChatService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  isMobile = signal(window.innerWidth <= 768);
  isChatOpen = signal(false);

  constructor() {
    void this.chatService.loadChats();

    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.checkChatOpen());

    this.checkChatOpen();
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: UIEvent): void {
    this.isMobile.set((event.target as Window).innerWidth <= 768);
  }

  private checkChatOpen(): void {
    const path = this.router.url.split(/[?#]/, 1)[0] || '';
    this.isChatOpen.set(/^\/chat\/[^/]+$/.test(path));
  }
}

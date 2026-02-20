import { Component, inject, OnInit, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatListComponent } from './chat-list.component';
import { ChatService } from './../services/ChatService';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

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
      height: 100vh;           /* На всю высоту родителя */
    }
    .container {
      width: 100%;
      height: calc(100% - 130px);
      display: flex;
      overflow: hidden;
    }

    .sidebar {
      width: 30%;
      flex-shrink: 0;
      height: 100%;
      box-sizing: border-box;
      overflow: hidden;
      display: flex;
      flex-direction: column;
    }

    .content {
      flex: 1;
      height: 100%;
      overflow: hidden;
      box-sizing: border-box;
    }

    @media (max-width: 768px) {
      .container {
        height: calc(100vh - 70px);
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
export class ChatLayoutComponent implements OnInit {
  chatService = inject(ChatService);
  router = inject(Router);

  isMobile = signal(window.innerWidth <= 768);
  isChatOpen = signal(false);

  ngOnInit(): void {
    this.chatService.loadChats();

    // Отслеживаем открытие чата по маршруту
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.checkChatOpen();
    });

    this.checkChatOpen();
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.isMobile.set(event.target.innerWidth <= 768);
  }

  private checkChatOpen(): void {
    const url = this.router.url;
    this.isChatOpen.set(/^\/chat\/[^/]+$/.test(url));
  }
}

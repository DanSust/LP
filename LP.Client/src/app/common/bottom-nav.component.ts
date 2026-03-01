import { Component, OnInit, OnDestroy, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil, filter } from 'rxjs/operators';
import { ChatService } from '../services/ChatService';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  badge?: number; // Для уведомлений (например, новые симпатии)
}

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatMenuModule,
    MatButtonModule,
    MatIconModule],
  templateUrl: './bottom-nav.component.html',
  styleUrls: ['./bottom-nav.component.scss']
})
export class BottomNavComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private chatService = inject(ChatService);

  activeRoute = '/';
  totalUnreadCount = computed(() => this.chatService.unreadMessageCount());

  navItems = computed<NavItem[]>(() => [
    { label: 'Знакомства', icon: 'people', route: '/match' },
    { label: 'Поиск', icon: 'search', route: '/search' },
    { label: 'Симпатии', icon: 'favorite', route: '/vote'  },    
    { label: 'Чат', icon: 'chat', route: '/chat', badge: this.totalUnreadCount() },
    { label: 'Профиль', icon: 'person', route: '/profile' },
    { label: 'Ещё', icon: 'more_vert', route: '' }
  ]);

  constructor(private router: Router) { }

  ngOnInit(): void {
    // Отслеживаем текущий роут для активной кнопки
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntil(this.destroy$)
    ).subscribe((event: any) => {
      this.activeRoute = event.url;
    });

    // Устанавливаем начальное состояние
    this.activeRoute = this.router.url;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  isActive(route: string): boolean {
    return this.activeRoute.startsWith(route);
  }

  onItemClick(item: NavItem, event: Event): void {
    console.log('onItemClick', item);
    if (this.isMenuItem(item)) {
      event.stopPropagation();
      return;
    }
    this.router.navigate([item.route]);
  }

  // Проверка — это элемент меню или обычная навигация
  isMenuItem(item: NavItem): boolean {
    return item.route === '';
  }

  onAbout(): void {
    this.router.navigate(['/about']);
  }

  onLogout(): void {
    console.log('Выход');
    this.router.navigate(['/login']);
  }
}

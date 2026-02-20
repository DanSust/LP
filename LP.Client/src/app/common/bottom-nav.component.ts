import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil, filter } from 'rxjs/operators';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  badge?: number; // Для уведомлений (например, новые симпатии)
}

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './bottom-nav.component.html',
  styleUrls: ['./bottom-nav.component.scss']
})
export class BottomNavComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  activeRoute = '/';

  navItems: NavItem[] = [
    { label: 'Поиск', icon: 'search', route: '/search' },
    { label: 'Симпатии', icon: 'favorite', route: '/vote', badge: 3 },
    { label: 'Знакомства', icon: 'people', route: '/match' },
    { label: 'Чат', icon: 'chat', route: '/chat', badge: 7 },
    { label: 'Профиль', icon: 'person', route: '/profile' }
  ];

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

  onNavClick(item: NavItem): void {
    // Можно добавить аналитику или логику перед переходом
    console.log('Переход на:', item.route);
  }
}

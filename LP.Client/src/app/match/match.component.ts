import { Component, ElementRef, Inject, OnInit, viewChild, OnDestroy, HostListener, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL, DEFAULT_AVATAR_URL } from './../app.config';
import { RouterLink } from '@angular/router';
import { ImageData } from './../Interfaces/ImageData';
import { formatDistance, getPeopleWord } from './../common/usefull.utils';
import { PageStateService } from './../services/PageStateService';
import { PageState } from './../Interfaces/PageState';
import { generateGUID } from '../common/GUID';
import { SplashScreenComponent } from '../splash/splash.component';

type TabType = 'mutual' | 'viewed' | 'likesMe' | 'iLike';

interface PaginationState {
  currentPage: number;
  hasMore: boolean;
  isLoading: boolean;
}

@Component({
  selector: 'match-component',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatMenuModule, MatIconModule, MatProgressSpinnerModule, SplashScreenComponent],
  templateUrl: './match.component.html',
  styleUrl: './match.component.scss'
})
export class MatchComponent implements OnInit, AfterViewInit, OnDestroy {
  galleryContainerRef = viewChild.required<ElementRef>('galleryContainer');
  baseUrl: string;
  defaultAvatarUrl: string;
  activeTab: TabType = 'likesMe';
  private currentScrollPosition: number = 0;

  protected readonly formatDistance = formatDistance;
  protected readonly getPeopleWord = getPeopleWord;

  allImages: ImageData[] = [];
  isLoading = true;
  isLoadingMore = false;
  /** Splash показываем только при первом открытии страницы, не при смене вкладок. */
  showSplash = true;

  currentPage: number = 1;
  pageSize: number = 20; // Количество элементов на странице
  sortGUID: string = '';
  hasMore: boolean = true; // Есть ли еще данные для загрузки

  private scrollDebounceTimer: any;
  private scrollListener?: () => void;

  // Пагинация для каждой вкладки отдельно
  private paginationMap: Map<TabType, PaginationState> = new Map([
    ['mutual', { currentPage: 1, hasMore: true, isLoading: false }],
    ['viewed', { currentPage: 1, hasMore: true, isLoading: false }],
    ['likesMe', { currentPage: 1, hasMore: true, isLoading: false }],
    ['iLike', { currentPage: 1, hasMore: true, isLoading: false }]    
  ]);

  private isRestoringState = false;
  private shouldRestoreScroll = false;
  private savedScrollPosition: number = 0;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string,
    @Inject(DEFAULT_AVATAR_URL) private avatarUrl: string,
    private pageStateService: PageStateService
  ) {
    this.baseUrl = base;
    this.defaultAvatarUrl = avatarUrl;
  }

  ngOnInit() {
    const savedState = this.pageStateService.getState();
    if (savedState && savedState.category === this.activeTab) {
      this.restoreState(savedState);
    } else {
      // Обычная инициализация
      this.sortGUID = generateGUID();
      this.isLoading = true; // 🔥 Явно устанавливаем
      this.setActiveTab('mutual');
    }
  }

  ngAfterViewInit(): void {
    if (this.shouldRestoreScroll) {
      this.restoreScrollPosition();
    }
    this.attachScrollListener();
  }

  ngOnDestroy() {
    this.saveStateBeforeLeave();
    // Убираем слушатель при уничтожении компонента
    if (this.scrollListener) {
      const container = this.galleryContainerRef()?.nativeElement;
      if (container) {
        container.removeEventListener('scroll', this.scrollListener);
      }
    }
    clearTimeout(this.scrollDebounceTimer);
  }

  getImageUrl(photoId: string | null | undefined): string {
    if (!photoId || photoId === '00000000-0000-0000-0000-000000000000') {
      return 'assets/default-avatar.svg';//this.defaultAvatarUrl;
    }
    return `${this.baseUrl}/Photos/image/${photoId}`;
  }

  private restoreState(state: PageState): void {
    if (!state || !state.images || !Array.isArray(state.images)) {
      console.warn('Invalid state received, loading fresh data');
      this.loadImages(true);
      return;
    }
    this.isRestoringState = true;

    // Восстанавливаем пагинацию
    this.currentPage = state.pagination.currentPage;
    this.hasMore = state.pagination.hasMore;
    this.savedScrollPosition = state.scrollPosition;
    this.activeTab = state.category as TabType;

    // Восстанавливаем изображения
    this.allImages = [...state.images];
    this.isLoading = false;
    this.showSplash = false;
    
    // Отмечаем, что нужно восстановить скролл    
    this.shouldRestoreScroll = true;
    this.isRestoringState = false;
    this.sortGUID = state.sortGUID;
  }

  private restoreScrollPosition(): void {
    if (!this.shouldRestoreScroll) return;

    // 🔥 Пробуем получить контейнер разными способами
    let container = this.galleryContainerRef()?.nativeElement;

    if (!container) {
      container = document.querySelector('.gallery-match-container') as HTMLElement;
    }

    if (container) {
      container.scrollTop = this.savedScrollPosition;

      // 🔥 Проверяем, действительно ли установилось
      setTimeout(() => {
        console.log('Actual scrollTop after restore:', container.scrollTop);
      }, 50);

      this.shouldRestoreScroll = false;
    } else {
      console.log('Cannot restore scroll: container=', !!container, 'position=', this.savedScrollPosition);
    }
  }

  // 🔥 Метод сохранения состояния
  private saveStateBeforeLeave(): void {
    const container = this.galleryContainerRef()?.nativeElement;
    const scrollPosition = container?.scrollTop || 0;

    this.pageStateService.saveState({
      images: this.allImages,
      filters: {
        ageMin: 0,
        ageMax: 0,
        selectedCityId: '',
        useGeolocation: false,
        radiusKm: 0,
        cityControlValue: ''
      },
      pagination: {
        currentPage: this.currentPage,
        hasMore: this.hasMore
      },
      sortGUID: this.sortGUID,
      scrollPosition: this.currentScrollPosition,
      showFilters: false,
      timestamp: Date.now(),
      category: this.activeTab
    });
  }

  // Метод для подписки на скролл после загрузки данных
  private attachScrollListener(): void {
    setTimeout(() => {
      const galleryContainer = this.galleryContainerRef()?.nativeElement;
      if (galleryContainer) {
        galleryContainer.addEventListener('scroll', this.onGalleryScroll.bind(this));
      } else {
        console.log('Gallery container not found');
      }
    }, 0);
  }

  loadImages(isInitialLoad: boolean = false): void {
    if (isInitialLoad) {
      this.isLoading = true;
      this.currentPage = 1;
      this.hasMore = true;
    } else {
      if (this.isLoadingMore || !this.hasMore) return;
      this.isLoadingMore = true;
    }

    this.http.get<any>(this.baseUrl + '/Votes/match', {
      withCredentials: true,
      params: {
        category: this.activeTab,
        page: this.currentPage,
        pageSize: '20'
      } })
      .subscribe({
        next: (data) => {
          if (isInitialLoad) {
            this.allImages = data;
            this.isLoading = false;
            this.showSplash = false;
            this.attachScrollListener();
          } else {
            if (data.length > 0) {
              this.allImages = [...this.allImages, ...data];
              //this.currentPage++;
            }

            // Если пришло меньше данных, чем запрошено, значит это последняя страница
            if (data.length < this.pageSize) {
              this.hasMore = false;
            }

            this.isLoadingMore = false;
          }

          // Проверяем, если это первая загрузка и данных мало
          if (isInitialLoad && data.length === this.pageSize) {
            this.hasMore = true;
          }
        },
        error: (err) => {
          //console.error('Failed to load images:', err);
          if (isInitialLoad) {
            this.isLoading = false;
            this.showSplash = false;
          } else {
            this.isLoadingMore = false;
          }
        }
      });
  }

  loadMoreImages(): void {
    if (!this.isLoadingMore && this.hasMore) {
      this.currentPage++;
      this.loadImages();
    }
  }

  onDelete(userId: string): void {
    // 1. Сначала удаляем из локального списка (мгновенный UI feedback)
    const index = this.allImages.findIndex(img => img.userId === userId);
    if (index !== -1) {
      this.allImages.splice(index, 1);
    }

    // 2. Затем отправляем запрос на backend
    this.http.post(`${this.baseUrl}/Votes/dislike/${userId}`, {}, {
      withCredentials: true
    }).subscribe({
      next: () => {
        console.log(`User ${userId} disliked successfully`);
      },
      error: (err) => {
        console.error('Failed to dislike user:', err);
        // При ошибке можно вернуть элемент обратно или показать уведомление
        // Но обычно лучше оставить как есть - пользователь уже не видит карточку
      }
    });
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {    
    // Используем дебаунс для оптимизации
    clearTimeout(this.scrollDebounceTimer);
    this.scrollDebounceTimer = setTimeout(() => {
      this.checkScrollPosition();
    }, 100);
  }

  onGalleryScroll(): void {
    const container = this.galleryContainerRef()?.nativeElement;
    if (container) {
      this.currentScrollPosition = container.scrollTop;
    }

    if (this.isLoadingMore || !this.hasMore) return;

    clearTimeout(this.scrollDebounceTimer);
    this.scrollDebounceTimer = setTimeout(() => {
      this.checkScrollPosition();
    }, 100);
  }

  checkScrollPosition(): void {
    // Получаем контейнер галереи
    const galleryContainer = document.querySelector('.gallery-match-container') as HTMLElement;
    if (!galleryContainer) return;

    const scrollTop = galleryContainer.scrollTop;
    const scrollHeight = galleryContainer.scrollHeight;
    const clientHeight = galleryContainer.clientHeight;

    // Порог срабатывания - 100px от конца
    const threshold = 100;

    if (scrollHeight - (scrollTop + clientHeight) < threshold) {
      this.loadMoreImages();
    }
  }  

  setActiveTab(tab: TabType): void {
    if (this.activeTab === tab) return;    

    this.activeTab = tab;

    const savedState = this.pageStateService.getState();
    if (savedState && savedState.category === tab) {
      this.restoreState(savedState);
    } else {
      this.allImages = [];
      this.loadImages(true);
    }    
  }

  filteredImages(): ImageData[] {
    if (!Array.isArray(this.allImages)) {
      return [];
    }
    return this.allImages.filter(img => img.category === this.activeTab);
  }

  
}
